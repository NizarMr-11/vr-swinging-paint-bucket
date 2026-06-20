using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Rendering
{
    public enum HarmonicFluidVisualMode
    {
        ScreenSpaceFluid = 0,
        DebugPoints = 1
    }

    /// <summary>
    /// Selects between SSFR fluid rendering and legacy debug billboards on the same pipeline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarmonicFluidVisualController : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private HarmonicScreenSpaceFluidRenderer screenSpaceRenderer;
        [SerializeField] private HarmonicParticleDebugRenderer debugRenderer;
        [SerializeField] private HarmonicFluidVisualMode visualMode = HarmonicFluidVisualMode.ScreenSpaceFluid;
        [SerializeField] private bool useScreenSpaceFluidInPlayMode = true;

        public HarmonicFluidVisualMode VisualMode
        {
            get => visualMode;
            set
            {
                visualMode = value;
                ApplyMode();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            if (Application.isPlaying
                && useScreenSpaceFluidInPlayMode
                && FindFirstObjectByType<HarmonicLabViewController>() == null)
            {
                visualMode = HarmonicFluidVisualMode.ScreenSpaceFluid;
            }

            ApplyMode();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyMode();
        }

        private void ResolveReferences()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (screenSpaceRenderer == null)
            {
                screenSpaceRenderer = GetComponent<HarmonicScreenSpaceFluidRenderer>();
                if (screenSpaceRenderer == null)
                {
                    screenSpaceRenderer = FindFirstObjectByType<HarmonicScreenSpaceFluidRenderer>();
                }
            }

            if (debugRenderer == null)
            {
                debugRenderer = FindFirstObjectByType<HarmonicParticleDebugRenderer>();
            }

            if (screenSpaceRenderer != null && pipeline != null)
            {
                screenSpaceRenderer.SetPipeline(pipeline);
            }

            if (debugRenderer != null && pipeline != null)
            {
                debugRenderer.SetPipeline(pipeline);
            }
        }

        private void ApplyMode()
        {
            bool ssfr = visualMode == HarmonicFluidVisualMode.ScreenSpaceFluid;

            if (screenSpaceRenderer != null)
            {
                screenSpaceRenderer.RenderingEnabled = ssfr;
                screenSpaceRenderer.enabled = ssfr;
            }

            if (debugRenderer != null)
            {
                debugRenderer.SuppressDrawing = ssfr;
            }
        }
    }
}
