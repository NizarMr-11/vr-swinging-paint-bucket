using HarmonicEngine.Diagnostics.Aspects;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// Scene component: boots the diagnostic hub and registers default AOP aspects.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class HarmonicDiagnosticHost : MonoBehaviour
    {
        public static HarmonicDiagnosticHost Instance { get; private set; }

        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private bool enableDiagnostics = true;
        [SerializeField] private bool enableFileLog = true;
        [SerializeField] private bool enableOverlay = true;
        [SerializeField] private bool logToUnityConsole = true;
        [SerializeField, Min(0.05f)] private float telemetrySampleInterval = 0.25f;
        [SerializeField] private int overlayFontSize = 14;

        private FileLogDiagnosticAspect _fileLog;
        private OverlayDiagnosticAspect _overlay;
        private ParticleTelemetryAspect _telemetry;

        public string LogFilePath => _fileLog?.LogFilePath ?? string.Empty;

        public bool IsOverlayVisible => _overlay?.IsVisible ?? false;

        public void SetOverlayVisible(bool visible) => _overlay?.SetVisible(visible);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            HarmonicDiagnosticHub.Enabled = enableDiagnostics;

            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            RegisterDefaultAspects();
            HarmonicDiagnosticHub.Initialize(pipeline);
            _overlay?.SetLogFilePath(LogFilePath);

            if (logToUnityConsole)
            {
                Debug.Log($"[HarmonicDiagnosticHost] AOP session started. Log: {LogFilePath}");
            }
        }

        private void Update()
        {
            HarmonicDiagnosticHub.TickFrame();
        }

        private void OnGUI()
        {
            _overlay?.DrawGui();
        }

        private void OnDestroy()
        {
            HarmonicDiagnosticHub.Shutdown();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            HarmonicDiagnosticHub.Shutdown();
        }

        private void RegisterDefaultAspects()
        {
            if (enableFileLog)
            {
                _fileLog = new FileLogDiagnosticAspect();
                HarmonicDiagnosticHub.Register(_fileLog);
            }

            if (enableOverlay)
            {
                _overlay = new OverlayDiagnosticAspect();
                _overlay.Configure(overlayFontSize, false);
                HarmonicDiagnosticHub.Register(_overlay);
            }

            _telemetry = new ParticleTelemetryAspect();
            _telemetry.Configure(telemetrySampleInterval, logToUnityConsole, _overlay);
            HarmonicDiagnosticHub.Register(_telemetry);
        }

        public static void Publish(in HarmonicDiagnosticEvent evt) => HarmonicDiagnosticHub.Publish(evt);
    }
}
