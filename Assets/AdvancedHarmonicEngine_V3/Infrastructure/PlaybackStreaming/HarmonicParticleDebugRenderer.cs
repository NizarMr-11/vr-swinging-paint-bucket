using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.Rendering;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Draws GPU particle buffers as procedural points for Scene/Game debug visualization.
    /// </summary>
    [ExecuteAlways]
    public class HarmonicParticleDebugRenderer : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private Material particleDebugMaterial;
        [SerializeField] private bool drawInternalParticles = true;
        [SerializeField] private bool drawFallingParticles = true;
        [Tooltip("When enabled, point radius tracks the SPH smoothing length so particles match the simulated fluid scale.")]
        [SerializeField] private bool autoSizeFromSph = true;
        [SerializeField, Range(0.5f, 3f)] private float pointSizeMultiplier = 1.05f;
        [SerializeField, Min(0.001f)] private float pointSize = 0.18f;
        [SerializeField] private Color internalColor = new(0.35f, 0.65f, 0.98f, 0.48f);
        [SerializeField] private Color fallingColor = new(1f, 0.45f, 0.1f, 0.9f);
        [SerializeField] private bool useParticleColor = true;

        /// <summary>When true, <see cref="OnRenderObject"/> skips drawing (e.g. SSFR active).</summary>
        public bool SuppressDrawing { get; set; }

        private static readonly int PointSizeId = Shader.PropertyToID("_PointSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int UseParticleColorId = Shader.PropertyToID("_UseParticleColor");

        public void SetPipeline(PipelineExecutionController controller) => pipeline = controller;

        private void OnValidate()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (particleDebugMaterial == null)
            {
                Shader shader = Shader.Find("HarmonicEngine/ParticleDebugPoints");
                if (shader != null)
                {
                    particleDebugMaterial = new Material(shader);
                }
            }
        }

        private void OnRenderObject()
        {
            if (SuppressDrawing || pipeline == null || particleDebugMaterial == null)
            {
                return;
            }

            if (pipeline.WorldFallingOnly)
            {
                if (drawFallingParticles
                    && pipeline.TryGetFallingParticleSoa(out ParticleSoaBuffers worldSoa, out uint worldCount)
                    && worldCount > 0)
                {
                    DrawSoa(worldSoa, worldCount, fallingColor);
                }

                return;
            }

            if (pipeline.ContainerFluidEnabled)
            {
                if (drawInternalParticles
                    && pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers containerSoa, out uint containerCount)
                    && containerCount > 0)
                {
                    DrawSoa(containerSoa, containerCount, internalColor);
                }

                return;
            }

            if (drawInternalParticles
                && pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers internalSoa, out uint internalCount)
                && internalCount > 0)
            {
                DrawSoa(internalSoa, internalCount, internalColor);
            }

            if (drawFallingParticles
                && pipeline.TryGetFallingParticleSoa(out ParticleSoaBuffers fallingSoa, out uint fallingCount)
                && fallingCount > 0)
            {
                DrawSoa(fallingSoa, fallingCount, fallingColor);
            }
        }

        private float ResolvePointRadius()
        {
            return HarmonicParticleDebugSizing.ResolvePointRadius(
                pipeline, autoSizeFromSph, pointSizeMultiplier, pointSize);
        }

        private void DrawSoa(ParticleSoaBuffers soa, uint count, Color color)
        {
            HarmonicParticleSoaShaderBindings.BindSoa(particleDebugMaterial, soa, (int)count);
            particleDebugMaterial.SetFloat(PointSizeId, ResolvePointRadius());
            particleDebugMaterial.SetColor(ColorId, color);
            particleDebugMaterial.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
            particleDebugMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, (int)count, 1);
        }
    }
}
