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
        [SerializeField, Range(0.5f, 3f)] private float splatRadiusMultiplier = 2.2f;
        [SerializeField, Range(0.001f, 0.5f)] private float thicknessWeight = 0.15f;
        [SerializeField] private Color fluidColor = new Color(0.1f, 0.4f, 0.8f, 1f);
        [SerializeField] private bool useParticleColor = true;
        [SerializeField, Min(0.001f)] private float blurFalloff = 0.05f;
        [SerializeField, Min(0.5f)] private float blurRadius = 3f;
        [SerializeField] private float normalScale = 100f;
        [SerializeField, Min(1f)] private float specularPower = 250f;
        [SerializeField, Range(0f, 2f)] private float specularIntensity = 1.5f;
        [SerializeField, Min(0.1f)] private float thicknessAbsorption = 40f;
        [SerializeField] private bool halfResolutionFluid = true;

        private Camera _camera;
        private Material _material;
        private CommandBuffer _commandBuffer;
        private RenderTexture _fluidDepth;
        private RenderTexture _fluidThickness;
        private RenderTexture _fluidDepthBlur;
        private int _lastWidth;
        private int _lastHeight;

        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int PackedColorsId = Shader.PropertyToID("_PackedColors");
        private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
        private static readonly int SplatRadiusId = Shader.PropertyToID("_SplatRadius");
        private static readonly int FluidColorId = Shader.PropertyToID("_FluidColor");
        private static readonly int ThicknessWeightId = Shader.PropertyToID("_ThicknessWeight");
        private static readonly int UseParticleColorId = Shader.PropertyToID("_UseParticleColor");
        private static readonly int FluidDepthId = Shader.PropertyToID("_FluidDepth");
        private static readonly int FluidThicknessTextureId = Shader.PropertyToID("_FluidThicknessTexture");
        private static readonly int BlurFalloffId = Shader.PropertyToID("_BlurFalloff");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private static readonly int SpecularPowerId = Shader.PropertyToID("_SpecularPower");
        private static readonly int SpecularIntensityId = Shader.PropertyToID("_SpecularIntensity");
        private static readonly int ThicknessAbsorptionId = Shader.PropertyToID("_ThicknessAbsorption");

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

            Shader shader = Resources.Load<Shader>("HarmonicEngineV4/V4SSFluidRender");
            if (shader == null)
            {
                shader = Shader.Find("HarmonicEngineV4/SSFluidRender");
            }

            if (shader != null)
            {
                _material = new Material(shader);
            }
        }

        private void OnEnable()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

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

        private void LateUpdate()
        {
            if (_commandBuffer == null)
            {
                return;
            }

            _commandBuffer.Clear();
            if (_material == null || pipeline == null || !pipeline.Initialized || pipeline.ActiveParticleCount <= 0)
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

            var depthDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.RHalf, 24)
            {
                msaaSamples = 1, useMipMap = false, sRGB = false
            };
            var thicknessDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1, useMipMap = false, sRGB = false
            };

            _fluidDepth = new RenderTexture(depthDesc) { name = "V4.FluidDepth" };
            _fluidDepth.Create();
            _fluidThickness = new RenderTexture(thicknessDesc) { name = "V4.FluidThickness" };
            _fluidThickness.Create();
            _fluidDepthBlur = new RenderTexture(depthDesc) { name = "V4.FluidDepthBlur" };
            _fluidDepthBlur.Create();
        }

        private void ReleaseTargets()
        {
            ReleaseTarget(ref _fluidDepth);
            ReleaseTarget(ref _fluidThickness);
            ReleaseTarget(ref _fluidDepthBlur);
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

        private void BuildCommands()
        {
            var depthTarget = new RenderTargetIdentifier(_fluidDepth);
            var thicknessTarget = new RenderTargetIdentifier(_fluidThickness);
            _commandBuffer.SetRenderTarget(new[] { depthTarget, thicknessTarget }, depthTarget);
            _commandBuffer.ClearRenderTarget(true, true, Color.clear);

            _material.SetFloat(SplatRadiusId, pipeline.ParticleRadius * splatRadiusMultiplier);
            _material.SetColor(FluidColorId, fluidColor);
            _material.SetFloat(ThicknessWeightId, thicknessWeight);
            _material.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
            _material.SetBuffer(Block0Id, pipeline.Soa.ReadBlock0);
            _material.SetBuffer(PackedColorsId, pipeline.Soa.ReadColors);
            _material.SetInt(ParticleCountId, pipeline.ActiveParticleCount);

            _commandBuffer.DrawProcedural(
                Matrix4x4.identity, _material, 0, MeshTopology.Points, pipeline.ActiveParticleCount, 1);

            _commandBuffer.SetGlobalTexture(FluidDepthId, _fluidDepth);
            _material.SetTexture(FluidDepthId, _fluidDepth);
            _material.SetFloat(BlurFalloffId, blurFalloff);
            _material.SetFloat(BlurRadiusId, blurRadius);
            _commandBuffer.Blit(_fluidDepth, _fluidDepthBlur, _material, 1);

            _commandBuffer.SetGlobalTexture(FluidThicknessTextureId, _fluidThickness);
            _material.SetFloat(NormalScaleId, normalScale);
            _material.SetFloat(SpecularPowerId, specularPower);
            _material.SetFloat(SpecularIntensityId, specularIntensity);
            _material.SetFloat(ThicknessAbsorptionId, thicknessAbsorption);
            _commandBuffer.Blit(_fluidDepthBlur, BuiltinRenderTextureType.CameraTarget, _material, 2);
        }
    }
}
