using HarmonicEngine.Infrastructure.Management;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarmonicEngine.Infrastructure.Rendering
{
    /// <summary>
    /// Built-in pipeline screen-space fluid renderer. Reads live SPH particle buffers and
    /// composites a blurred depth/thickness field over the camera color target.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class HarmonicScreenSpaceFluidRenderer : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private Material fluidMaterial;
        [SerializeField] private bool drawInternalParticles = true;
        [SerializeField] private bool drawFallingParticles = true;
        [SerializeField, Range(0.5f, 2f)] private float splatRadiusMultiplier = 1.05f;
        [SerializeField, Range(0.001f, 0.2f)] private float thicknessWeight = 0.035f;
        [SerializeField] private Color fluidColor = new(0.1f, 0.4f, 0.8f, 1f);
        [SerializeField] private bool useParticleColor = true;
        [SerializeField, Min(0.001f)] private float blurFalloff = 0.05f;
        [SerializeField, Min(0.5f)] private float blurRadius = 3f;
        [SerializeField] private float normalScale = 100f;
        [SerializeField, Min(1f)] private float specularPower = 250f;
        [SerializeField, Range(0f, 2f)] private float specularIntensity = 1.5f;
        [SerializeField, Min(0.1f)] private float thicknessAbsorption = 2f;
        [SerializeField] private bool halfResolutionFluid = true;
        [SerializeField] private bool debugPass0Only = false;
        [SerializeField, Min(0.1f)] private float debugMaxEyeDepth = 12f;

        private Camera _camera;
        private CommandBuffer _commandBuffer;
        private static readonly ProfilerMarker MarkerSsfr = new("Harmonic.SSFR");
        private RenderTexture _fluidDepth;
        private RenderTexture _fluidThickness;
        private RenderTexture _fluidDepthBlur;
        private int _lastWidth;
        private int _lastHeight;

        private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
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
        private static readonly int MaxEyeDepthId = Shader.PropertyToID("_MaxEyeDepth");

        private const int DebugDepthVisPass = 3;

        public bool RenderingEnabled { get; set; } = true;

        public void SetPipeline(PipelineExecutionController controller) => pipeline = controller;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
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
            RebuildCommandBuffer();
        }

        private void OnDisable()
        {
            ReleaseCommandBuffer();
            ReleaseRenderTargets();
        }

        private void OnDestroy()
        {
            ReleaseCommandBuffer();
            ReleaseRenderTargets();
        }

        private void OnValidate()
        {
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (fluidMaterial == null)
            {
                Shader shader = Shader.Find("HarmonicEngine/SSFluidRender");
                if (shader != null)
                {
                    fluidMaterial = new Material(shader);
                }
            }
        }

        private void RebuildCommandBuffer()
        {
            ReleaseCommandBuffer();
            _commandBuffer = new CommandBuffer { name = "Harmonic.SSFR" };
            _camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);
        }

        private void ReleaseCommandBuffer()
        {
            if (_commandBuffer == null || _camera == null)
            {
                _commandBuffer = null;
                return;
            }

            _camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _commandBuffer);
            _commandBuffer.Release();
            _commandBuffer = null;
        }

        private void LateUpdate()
        {
            if (!RenderingEnabled || _commandBuffer == null || fluidMaterial == null || pipeline == null)
            {
                if (_commandBuffer != null)
                {
                    _commandBuffer.Clear();
                }

                return;
            }

            EnsureRenderTargets();
            using (MarkerSsfr.Auto())
            {
                BuildCommands();
            }
        }

        private void EnsureRenderTargets()
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

            ReleaseRenderTargets();
            _lastWidth = width;
            _lastHeight = height;

            var depthDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.RHalf, 24)
            {
                msaaSamples = 1,
                useMipMap = false,
                sRGB = false
            };
            var thicknessDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                sRGB = false
            };

            _fluidDepth = new RenderTexture(depthDesc) { name = "Harmonic.FluidDepth" };
            _fluidDepth.Create();
            _fluidThickness = new RenderTexture(thicknessDesc) { name = "Harmonic.FluidThickness" };
            _fluidThickness.Create();
            _fluidDepthBlur = new RenderTexture(depthDesc) { name = "Harmonic.FluidDepthBlur" };
            _fluidDepthBlur.Create();
        }

        private void ReleaseRenderTargets()
        {
            if (_fluidDepth != null)
            {
                _fluidDepth.Release();
                if (Application.isPlaying)
                {
                    Destroy(_fluidDepth);
                }
                else
                {
                    DestroyImmediate(_fluidDepth);
                }

                _fluidDepth = null;
            }

            if (_fluidThickness != null)
            {
                _fluidThickness.Release();
                if (Application.isPlaying)
                {
                    Destroy(_fluidThickness);
                }
                else
                {
                    DestroyImmediate(_fluidThickness);
                }

                _fluidThickness = null;
            }

            if (_fluidDepthBlur != null)
            {
                _fluidDepthBlur.Release();
                if (Application.isPlaying)
                {
                    Destroy(_fluidDepthBlur);
                }
                else
                {
                    DestroyImmediate(_fluidDepthBlur);
                }

                _fluidDepthBlur = null;
            }

            _lastWidth = 0;
            _lastHeight = 0;
        }

        private void BuildCommands()
        {
            _commandBuffer.Clear();

            var depthTarget = new RenderTargetIdentifier(_fluidDepth);
            var thicknessTarget = new RenderTargetIdentifier(_fluidThickness);
            var mrt = new[] { depthTarget, thicknessTarget };

            _commandBuffer.SetRenderTarget(mrt, depthTarget);
            _commandBuffer.ClearRenderTarget(true, true, Color.clear);

            float splatRadius = pipeline.SmoothingRadius * splatRadiusMultiplier;
            ApplySharedMaterialProperties(splatRadius);

            DrawParticleBuffers();

            _commandBuffer.SetGlobalTexture(FluidDepthId, _fluidDepth);
            fluidMaterial.SetTexture(FluidDepthId, _fluidDepth);

            if (debugPass0Only)
            {
                fluidMaterial.SetFloat(MaxEyeDepthId, debugMaxEyeDepth);
                _commandBuffer.Blit(_fluidDepth, BuiltinRenderTextureType.CameraTarget, fluidMaterial, DebugDepthVisPass);
                return;
            }

            fluidMaterial.SetFloat(BlurFalloffId, blurFalloff);
            fluidMaterial.SetFloat(BlurRadiusId, blurRadius);
            _commandBuffer.Blit(_fluidDepth, _fluidDepthBlur, fluidMaterial, 1);

            _commandBuffer.SetGlobalTexture(FluidThicknessTextureId, _fluidThickness);
            fluidMaterial.SetFloat(NormalScaleId, normalScale);
            fluidMaterial.SetFloat(SpecularPowerId, specularPower);
            fluidMaterial.SetFloat(SpecularIntensityId, specularIntensity);
            fluidMaterial.SetFloat(ThicknessAbsorptionId, thicknessAbsorption);
            fluidMaterial.SetColor(FluidColorId, fluidColor);
            _commandBuffer.Blit(_fluidDepthBlur, BuiltinRenderTextureType.CameraTarget, fluidMaterial, 2);
        }

        private void ApplySharedMaterialProperties(float splatRadius)
        {
            fluidMaterial.SetFloat(SplatRadiusId, splatRadius);
            fluidMaterial.SetColor(FluidColorId, fluidColor);
            fluidMaterial.SetFloat(ThicknessWeightId, thicknessWeight);
            fluidMaterial.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
        }

        private void DrawParticleBuffers()
        {
            if (pipeline.WorldFallingOnly)
            {
                if (drawFallingParticles
                    && pipeline.TryGetFallingParticleBuffer(out ComputeBuffer worldBuffer, out uint worldCount)
                    && worldCount > 0)
                {
                    DrawBuffer(worldBuffer, worldCount);
                }

                return;
            }

            if (pipeline.ContainerFluidEnabled)
            {
                if (drawInternalParticles
                    && pipeline.TryGetInternalParticleBuffer(out ComputeBuffer containerBuffer, out uint containerCount)
                    && containerCount > 0)
                {
                    DrawBuffer(containerBuffer, containerCount);
                }

                return;
            }

            if (drawInternalParticles
                && pipeline.TryGetInternalParticleBuffer(out ComputeBuffer internalBuffer, out uint internalCount)
                && internalCount > 0)
            {
                DrawBuffer(internalBuffer, internalCount);
            }

            if (drawFallingParticles
                && pipeline.TryGetFallingParticleBuffer(out ComputeBuffer fallingBuffer, out uint fallingCount)
                && fallingCount > 0)
            {
                DrawBuffer(fallingBuffer, fallingCount);
            }
        }

        private void DrawBuffer(ComputeBuffer buffer, uint count)
        {
            fluidMaterial.SetBuffer(ParticlesId, buffer);
            fluidMaterial.SetInt(ParticleCountId, (int)count);
            _commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                fluidMaterial,
                0,
                MeshTopology.Points,
                (int)count,
                1);
        }
    }
}

