using HarmonicEngineV4.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarmonicEngineV4.Rendering
{
    /// <summary>
    /// SSFR adapter (plan Phase 6): binds the V4 particle SOA buffers to the copied
    /// V3 splat/blur/composite shader. Camera component; no dependency on any V3 code.
    /// Pass 0 renders sphere impostors into depth+thickness MRT, pass 1 blurs depth,
    /// pass 2 composites with normals/specular/absorption over the camera target.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class V4ScreenSpaceFluidRenderer : MonoBehaviour
    {
        [SerializeField] private V4PipelineRoot pipeline;
        [SerializeField, Range(0.5f, 3f)] private float splatRadiusMultiplier = 3f;
        [SerializeField, Range(0.001f, 0.5f)] private float thicknessWeight = 0.15f;
        [SerializeField] private Color fluidColor = new Color(0.1f, 0.4f, 0.8f, 1f);
        [SerializeField] private bool useParticleColor = true;
        [SerializeField] private float velocityStretchScale = 1f;
        [SerializeField, Min(1f)] private float velocityStretchMax = 3f;
        [SerializeField, Min(0.001f)] private float blurFalloff = 0.05f;
        [SerializeField, Min(0.5f)] private float blurRadius = 3f;
        [SerializeField, Min(1)] private int blurIterations = 4;
        [SerializeField] private float normalScale = 100f;
        [SerializeField, Min(1f)] private float specularPower = 250f;
        [SerializeField, Range(0f, 2f)] private float specularIntensity = 1.5f;
        [SerializeField, Min(0.1f)] private float thicknessAbsorption = 40f;
        [SerializeField] private bool halfResolutionFluid = true;
        [SerializeField] private bool debugPass0Only;
        [SerializeField, Min(0.1f)] private float debugMaxEyeDepth = 12f;

        private Camera _camera;
        private Material _material;
        private CommandBuffer _commandBuffer;
        private int _compositePass = -1;
        private RenderTexture _fluidDepth;
        private RenderTexture _fluidThickness;
        private RenderTexture _fluidThicknessBlur;
        private RenderTexture _fluidThicknessBlurB;
        private RenderTexture _fluidDepthBlur;
        private RenderTexture _fluidDepthBlurB;
        private int _lastWidth;
        private int _lastHeight;

        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int Block1Id = Shader.PropertyToID("_Block1");
        private static readonly int FlagsId = Shader.PropertyToID("_Flags");
        private static readonly int PackedColorsId = Shader.PropertyToID("_PackedColors");
        private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
        private static readonly int SplatRadiusId = Shader.PropertyToID("_SplatRadius");
        private static readonly int VelocityStretchScaleId = Shader.PropertyToID("_VelocityStretchScale");
        private static readonly int VelocityStretchMaxId = Shader.PropertyToID("_VelocityStretchMax");
        private static readonly int FluidColorId = Shader.PropertyToID("_FluidColor");
        private static readonly int ThicknessWeightId = Shader.PropertyToID("_ThicknessWeight");
        private static readonly int UseParticleColorId = Shader.PropertyToID("_UseParticleColor");
        private static readonly int FluidDepthId = Shader.PropertyToID("_FluidDepth");
        private static readonly int FluidThicknessTextureId = Shader.PropertyToID("_FluidThicknessTexture");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");
        private static readonly int BlurFalloffId = Shader.PropertyToID("_BlurFalloff");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int BlurCoveragePassId = Shader.PropertyToID("_BlurCoveragePass");
        private static readonly int BlurDepthPassId = Shader.PropertyToID("_BlurDepthPass");
        private static readonly int BlurThicknessOutputId = Shader.PropertyToID("_BlurThicknessOutput");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private static readonly int SpecularPowerId = Shader.PropertyToID("_SpecularPower");
        private static readonly int SpecularIntensityId = Shader.PropertyToID("_SpecularIntensity");
        private static readonly int ThicknessAbsorptionId = Shader.PropertyToID("_ThicknessAbsorption");
        private static readonly int MaxEyeDepthId = Shader.PropertyToID("_MaxEyeDepth");

        private const int DebugDepthVisPass = 3;

        public void SetPipeline(V4PipelineRoot root) => pipeline = root;

        public void ApplyOptions(V4ScreenSpaceFluidOptions options)
        {
            bool resolutionChanged = halfResolutionFluid != options.halfResolutionFluid;
            splatRadiusMultiplier = options.splatRadiusMultiplier;
            thicknessWeight = options.thicknessWeight;
            fluidColor = options.fluidColor;
            useParticleColor = options.useParticleColor;
            blurFalloff = options.blurFalloff;
            blurRadius = options.blurRadius;
            blurIterations = options.blurIterations;
            normalScale = options.normalScale;
            specularPower = options.specularPower;
            specularIntensity = options.specularIntensity;
            thicknessAbsorption = options.thicknessAbsorption;
            halfResolutionFluid = options.halfResolutionFluid;

            if (resolutionChanged)
            {
                ReleaseTargets();
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            EnsureMaterial();
        }

        private void OnEnable()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            EnsureMaterial();
            _commandBuffer = new CommandBuffer { name = "V4.SSFR" };
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);
        }

        private void OnDisable()
        {
            if (_commandBuffer != null && _camera != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);
            }

            _commandBuffer?.Release();
            _commandBuffer = null;
            ReleaseTargets();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void EnsureMaterial()
        {
            if (_material != null)
            {
                return;
            }

            Shader shader = Resources.Load<Shader>("HarmonicEngineV4/V4SSFluidRender");
            if (shader == null)
            {
                shader = Shader.Find("HarmonicEngineV4/SSFluidRender");
            }

            if (shader == null)
            {
                Debug.LogError("V4ScreenSpaceFluidRenderer: V4SSFluidRender shader not found.");
                return;
            }

            _material = new Material(shader);
            _compositePass = _material.FindPass("Composite");
            if (_compositePass < 0)
            {
                Debug.LogError("V4ScreenSpaceFluidRenderer: Composite pass failed to compile.");
            }
        }

        private void LateUpdate()
        {
            if (_commandBuffer == null)
            {
                return;
            }

            _commandBuffer.Clear();
            if (_material == null || _compositePass < 0 || pipeline == null
                || !pipeline.Initialized || pipeline.ActiveParticleCount <= 0)
            {
                return;
            }

            EnsureTargets();
            BuildCommands();
        }

        private void EnsureTargets()
        {
            int width = _camera.pixelWidth;
            int height = _camera.pixelHeight;
            if (halfResolutionFluid)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
            }

            if (_fluidDepth != null && _lastWidth == width && _lastHeight == height)
            {
                return;
            }

            ReleaseTargets();
            _lastWidth = width;
            _lastHeight = height;

            var depthFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf
                : RenderTextureFormat.RGHalf;
            var splatDepthDesc = new RenderTextureDescriptor(width, height, depthFormat, 24)
            {
                msaaSamples = 1, useMipMap = false, sRGB = false
            };
            var depthBlurDesc = new RenderTextureDescriptor(width, height, depthFormat, 0)
            {
                msaaSamples = 1, useMipMap = false, sRGB = false
            };
            var thicknessDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1, useMipMap = false, sRGB = false
            };

            _fluidDepth = new RenderTexture(splatDepthDesc) { name = "V4.FluidDepth" };
            _fluidDepth.Create();
            _fluidThickness = new RenderTexture(thicknessDesc) { name = "V4.FluidThickness" };
            _fluidThickness.Create();
            _fluidThicknessBlur = new RenderTexture(thicknessDesc) { name = "V4.FluidThicknessBlur" };
            _fluidThicknessBlur.Create();
            _fluidThicknessBlurB = new RenderTexture(thicknessDesc) { name = "V4.FluidThicknessBlurB" };
            _fluidThicknessBlurB.Create();
            _fluidDepthBlur = new RenderTexture(depthBlurDesc) { name = "V4.FluidDepthBlur" };
            _fluidDepthBlur.Create();
            _fluidDepthBlurB = new RenderTexture(depthBlurDesc) { name = "V4.FluidDepthBlurB" };
            _fluidDepthBlurB.Create();
        }

        private void ReleaseTargets()
        {
            ReleaseTarget(ref _fluidDepth);
            ReleaseTarget(ref _fluidThickness);
            ReleaseTarget(ref _fluidThicknessBlur);
            ReleaseTarget(ref _fluidThicknessBlurB);
            ReleaseTarget(ref _fluidDepthBlur);
            ReleaseTarget(ref _fluidDepthBlurB);
            _lastWidth = 0;
            _lastHeight = 0;
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null)
            {
                return;
            }

            target.Release();
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }

            target = null;
        }

        private void SetBlurMode(float coveragePass, float depthPass, float thicknessOutput)
        {
            _material.SetFloat(BlurCoveragePassId, coveragePass);
            _material.SetFloat(BlurDepthPassId, depthPass);
            _material.SetFloat(BlurThicknessOutputId, thicknessOutput);
        }

        private void BuildCommands()
        {
            var depthTarget = new RenderTargetIdentifier(_fluidDepth);
            var thicknessTarget = new RenderTargetIdentifier(_fluidThickness);
            _commandBuffer.SetRenderTarget(new[] { depthTarget, thicknessTarget }, depthTarget);
            _commandBuffer.SetViewport(new Rect(0f, 0f, _fluidDepth.width, _fluidDepth.height));
            _commandBuffer.ClearRenderTarget(true, true, Color.clear);

            _material.SetFloat(SplatRadiusId, pipeline.ParticleRadius * splatRadiusMultiplier);
            _material.SetFloat(VelocityStretchScaleId, velocityStretchScale);
            _material.SetFloat(VelocityStretchMaxId, velocityStretchMax);
            _material.SetColor(FluidColorId, fluidColor);
            _material.SetFloat(ThicknessWeightId, thicknessWeight);
            _material.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
            _material.SetBuffer(Block0Id, pipeline.Soa.ReadBlock0);
            _material.SetBuffer(Block1Id, pipeline.Soa.ReadBlock1);
            _material.SetBuffer(FlagsId, pipeline.Soa.ReadFlags);
            _material.SetBuffer(PackedColorsId, pipeline.Soa.ReadColors);
            _material.SetInt(ParticleCountId, pipeline.ActiveParticleCount);

            _commandBuffer.DrawProcedural(
                Matrix4x4.identity, _material, 0, MeshTopology.Points, pipeline.ActiveParticleCount, 1);

            _commandBuffer.SetGlobalTexture(FluidDepthId, _fluidDepth);
            _material.SetTexture(FluidDepthId, _fluidDepth);
            _material.SetFloat(BlurFalloffId, blurFalloff);
            _material.SetFloat(BlurRadiusId, blurRadius);

            int iterations = Mathf.Max(1, blurIterations);
            RenderTexture depthBlurSrc = _fluidDepth;
            RenderTexture depthBlurDst = _fluidDepthBlur;
            RenderTexture thicknessBlurSrc = _fluidThickness;
            RenderTexture thicknessBlurDst = _fluidThicknessBlur;
            for (int i = 0; i < iterations; i++)
            {
                bool lastPass = i == iterations - 1;
                bool runThicknessBlur = !lastPass || iterations == 1;
                bool runDepthBlurInCoveragePass = !lastPass || iterations == 1;

                SetBlurMode(lastPass ? 1f : 0f, runDepthBlurInCoveragePass ? 1f : 0f, 0f);
                _commandBuffer.Blit(depthBlurSrc, depthBlurDst, _material, 1);

                if (runThicknessBlur)
                {
                    SetBlurMode(0f, 0f, 1f);
                    _material.SetTexture(FluidDepthId, depthBlurSrc);
                    _commandBuffer.Blit(thicknessBlurSrc, thicknessBlurDst, _material, 1);

                    thicknessBlurSrc = thicknessBlurDst;
                    thicknessBlurDst = thicknessBlurSrc == _fluidThicknessBlur
                        ? _fluidThicknessBlurB
                        : _fluidThicknessBlur;
                }

                depthBlurSrc = depthBlurDst;
                depthBlurDst = depthBlurSrc == _fluidDepthBlur ? _fluidDepthBlurB : _fluidDepthBlur;
            }

            RenderTexture finalBlurDepth = depthBlurSrc;
            RenderTexture finalBlurThickness = thicknessBlurSrc;

            if (debugPass0Only)
            {
                _material.SetFloat(MaxEyeDepthId, debugMaxEyeDepth);
                _material.SetTexture(FluidDepthId, finalBlurDepth);
                SetFluidDepthTexelSize(finalBlurDepth);
                DrawFullscreenProcedural(DebugDepthVisPass, loadCameraColor: false);
                return;
            }

            SetBlurMode(0f, 0f, 0f);
            _material.SetTexture(MainTexId, finalBlurDepth);
            SetMainTexTexelSize(finalBlurDepth);
            _commandBuffer.SetGlobalTexture(FluidThicknessTextureId, finalBlurThickness);
            _material.SetTexture(FluidThicknessTextureId, finalBlurThickness);
            _material.SetFloat(NormalScaleId, normalScale);
            _material.SetFloat(SpecularPowerId, specularPower);
            _material.SetFloat(SpecularIntensityId, specularIntensity);
            _material.SetFloat(ThicknessAbsorptionId, thicknessAbsorption);
            _material.SetColor(FluidColorId, fluidColor);
            _material.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
            DrawFullscreenProcedural(_compositePass, loadCameraColor: true);
        }

        private void DrawFullscreenProcedural(int shaderPass, bool loadCameraColor)
        {
            var cameraTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            _commandBuffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            if (loadCameraColor)
            {
                _commandBuffer.SetRenderTarget(
                    cameraTarget,
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store);
            }
            else
            {
                _commandBuffer.SetRenderTarget(cameraTarget);
            }

            _commandBuffer.DrawProcedural(
                Matrix4x4.identity, _material, shaderPass, MeshTopology.Triangles, 3, 1);
            _commandBuffer.SetViewProjectionMatrices(_camera.worldToCameraMatrix, _camera.projectionMatrix);
        }

        private void SetMainTexTexelSize(RenderTexture source)
        {
            _material.SetVector(MainTexTexelSizeId, new Vector4(
                1f / source.width,
                -1f / source.height,
                source.width,
                source.height));
        }

        private void SetFluidDepthTexelSize(RenderTexture source)
        {
            _material.SetVector(Shader.PropertyToID("_FluidDepth_TexelSize"), new Vector4(
                1f / source.width,
                -1f / source.height,
                source.width,
                source.height));
        }
    }
}
