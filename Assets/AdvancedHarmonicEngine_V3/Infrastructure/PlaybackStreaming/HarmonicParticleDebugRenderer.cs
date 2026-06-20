using HarmonicEngine.Infrastructure.Management;
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

        private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
        private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
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
                    && pipeline.TryGetFallingParticleBuffer(out ComputeBuffer worldBuffer, out uint worldCount)
                    && worldCount > 0)
                {
                    DrawBuffer(worldBuffer, worldCount, fallingColor);
                }

                return;
            }

            if (pipeline.ContainerFluidEnabled)
            {
                if (drawInternalParticles
                    && pipeline.TryGetInternalParticleBuffer(out ComputeBuffer containerBuffer, out uint containerCount)
                    && containerCount > 0)
                {
                    DrawBuffer(containerBuffer, containerCount, internalColor);
                }

                return;
            }

            if (drawInternalParticles && pipeline.TryGetInternalParticleBuffer(out ComputeBuffer internalBuffer, out uint internalCount) && internalCount > 0)
            {
                DrawBuffer(internalBuffer, internalCount, internalColor);
            }

            if (drawFallingParticles && pipeline.TryGetFallingParticleBuffer(out ComputeBuffer fallingBuffer, out uint fallingCount) && fallingCount > 0)
            {
                DrawBuffer(fallingBuffer, fallingCount, fallingColor);
            }
        }

        private float ResolvePointRadius()
        {
            return HarmonicParticleDebugSizing.ResolvePointRadius(
                pipeline, autoSizeFromSph, pointSizeMultiplier, pointSize);
        }

        private void DrawBuffer(ComputeBuffer buffer, uint count, Color color)
        {
            particleDebugMaterial.SetBuffer(ParticlesId, buffer);
            particleDebugMaterial.SetInt(ParticleCountId, (int)count);
            particleDebugMaterial.SetFloat(PointSizeId, ResolvePointRadius());
            particleDebugMaterial.SetColor(ColorId, color);
            particleDebugMaterial.SetFloat(UseParticleColorId, useParticleColor ? 1f : 0f);
            particleDebugMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, (int)count, 1);
        }
    }
}
