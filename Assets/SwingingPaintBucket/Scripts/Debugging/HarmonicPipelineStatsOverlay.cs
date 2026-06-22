using System;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace SwingingPaintBucket.Debugging
{
    /// <summary>
    /// Deprecated — use <see cref="HarmonicEngine.Diagnostics.HarmonicPipelineDiagnosticsController"/> on
    /// <c>HarmonicPipelineRoot</c> for the unified diagnostics HUD.
    /// </summary>
    [Obsolete("Use HarmonicPipelineDiagnosticsController on HarmonicPipelineRoot for the unified diagnostics HUD.")]
    [DisallowMultipleComponent]
    public class HarmonicPipelineStatsOverlay : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;

        public bool IsVisible => false;

        public void SetVisible(bool value)
        {
        }

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }
        }
    }
}
