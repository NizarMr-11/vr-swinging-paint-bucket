using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// Single inspector entry point on <c>HarmonicPipelineRoot</c> for logging, pipeline diagnostics,
    /// profile telemetry (<c>perf.log</c>), and the unified on-screen HUD.
    /// </summary>
    [DefaultExecutionOrder(-510)]
    [DisallowMultipleComponent]
    public sealed class HarmonicPipelineDiagnosticsController : MonoBehaviour
    {
        [SerializeField] private HarmonicPipelineDiagnosticsSettings settings =
            HarmonicPipelineDiagnosticsSettings.CreateDefault();

        public HarmonicPipelineDiagnosticsSettings Settings => settings;

        private void Reset() => settings = HarmonicPipelineDiagnosticsSettings.CreateDefault();

        private void Awake()
        {
            var pipeline = GetComponent<PipelineExecutionController>();
            var host = GetComponent<HarmonicDiagnosticHost>() ?? gameObject.AddComponent<HarmonicDiagnosticHost>();

            if (pipeline != null)
            {
                pipeline.ApplyDiagnosticsSettings(settings);
            }

            host.ApplySettings(settings, pipeline);
        }
    }
}
