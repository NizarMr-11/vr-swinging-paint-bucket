using HarmonicEngine.Diagnostics;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace SwingingPaintBucket.Debugging
{
    /// <summary>
    /// Legacy entry point — forwards to <see cref="HarmonicDiagnosticHost"/> AOP hub.
    /// </summary>
    [DisallowMultipleComponent]
    public class HarmonicSimulationDebugLogger : MonoBehaviour
    {
        public static HarmonicSimulationDebugLogger Instance { get; private set; }

        [SerializeField] private bool ensureDiagnosticHost = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (ensureDiagnosticHost && FindFirstObjectByType<HarmonicDiagnosticHost>() == null)
            {
                var pipeline = FindFirstObjectByType<PipelineExecutionController>();
                var hostGo = pipeline != null ? pipeline.gameObject : gameObject;
                hostGo.AddComponent<HarmonicDiagnosticHost>();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void LogEvent(string category, string message)
        {
            HarmonicDiagnosticHub.PublishSimple(
                HarmonicDiagnosticEventType.PipelineFrameAfter,
                category,
                message);
        }
    }
}
