using HarmonicEngine.Diagnostics.Aspects;
using HarmonicEngine.Infrastructure.Management;
using System.Collections;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// Scene component: boots the diagnostic hub and registers default AOP aspects.
    /// Configuration is applied via <see cref="Bootstrap"/> from
    /// <see cref="HarmonicPipelineDiagnosticsController"/>.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class HarmonicDiagnosticHost : MonoBehaviour
    {
        public static HarmonicDiagnosticHost Instance { get; private set; }

        private const float DefaultTelemetrySampleInterval = 0.25f;

        private PipelineExecutionController _pipeline;
        private bool _bootstrapped;

        private MultiChannelFileLogAspect _fileLog;
        private UnifiedProfileOverlayAspect _unifiedOverlay;
        private ParticleTelemetryAspect _telemetry;
        private PerformanceTelemetryAspect _performanceTelemetry;

        public string RunDirectory => _fileLog?.RunDirectory ?? HarmonicDiagnosticHub.Session?.RunDirectory ?? string.Empty;

        public string LogFilePath => RunDirectory;

        public string GetChannelLogPath(HarmonicLogChannel channel) =>
            _fileLog?.GetChannelLogPath(channel) ?? string.Empty;

        public bool IsOverlayVisible => _unifiedOverlay?.IsVisible ?? false;

        public void SetOverlayVisible(bool visible) => _unifiedOverlay?.SetVisible(visible);

        public void ApplySettings(
            HarmonicPipelineDiagnosticsSettings settings,
            PipelineExecutionController pipelineController) =>
            Bootstrap(settings, pipelineController);

        public void Bootstrap(HarmonicPipelineDiagnosticsSettings settings, PipelineExecutionController pipelineController)
        {
            if (_bootstrapped)
            {
                return;
            }

            _pipeline = pipelineController;
            HarmonicDiagnosticHub.Enabled = settings.enableDiagnostics;

            RegisterAspects(settings);
            HarmonicDiagnosticHub.Initialize(_pipeline, settings);
            _unifiedOverlay?.SetLogFilePath(RunDirectory);
            StartCoroutine(EnsureManifestInitSnapshotFallback());

            if (settings.logToUnityConsole)
            {
                Debug.Log($"[HarmonicDiagnosticHost] AOP session started. Run directory: {RunDirectory}");
            }

            _bootstrapped = true;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            if (GetComponent<HarmonicPipelineDiagnosticsController>() == null)
            {
                _pipeline = FindFirstObjectByType<PipelineExecutionController>();
                Bootstrap(HarmonicPipelineDiagnosticsSettings.CreateDefault(), _pipeline);
            }
        }

        private void Update()
        {
            HarmonicDiagnosticHub.TickFrame();
        }

        private void OnGUI()
        {
            _unifiedOverlay?.DrawGui();
        }

        private void OnDestroy()
        {
            UnregisterOwnedAspects();
            HarmonicDiagnosticHub.Shutdown();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            UnregisterOwnedAspects();
            HarmonicDiagnosticHub.Shutdown();
        }

        private void UnregisterOwnedAspects()
        {
            if (_fileLog != null)
            {
                HarmonicDiagnosticHub.Unregister(_fileLog);
                _fileLog = null;
            }

            if (_unifiedOverlay != null)
            {
                HarmonicDiagnosticHub.Unregister(_unifiedOverlay);
                _unifiedOverlay = null;
            }

            if (_telemetry != null)
            {
                HarmonicDiagnosticHub.Unregister(_telemetry);
                _telemetry = null;
            }

            if (_performanceTelemetry != null)
            {
                HarmonicDiagnosticHub.Unregister(_performanceTelemetry);
                _performanceTelemetry = null;
            }
        }

        private void RegisterAspects(HarmonicPipelineDiagnosticsSettings settings)
        {
            if (settings.enableFileLog && _fileLog == null)
            {
                _fileLog = new MultiChannelFileLogAspect();
                HarmonicDiagnosticHub.Register(_fileLog);
            }

            if (settings.showOverlay && _unifiedOverlay == null)
            {
                _unifiedOverlay = new UnifiedProfileOverlayAspect();
                _unifiedOverlay.Configure(
                    settings.overlayFontSize,
                    true,
                    settings.smoothingFrames,
                    settings.spikeThresholdMs,
                    settings.enableFrameTimingStats);
                HarmonicDiagnosticHub.Register(_unifiedOverlay);
            }

            if (settings.enableTelemetry && _telemetry == null)
            {
                _telemetry = new ParticleTelemetryAspect();
                _telemetry.Configure(DefaultTelemetrySampleInterval, settings.logToUnityConsole, _unifiedOverlay);
                HarmonicDiagnosticHub.Register(_telemetry);
            }

            if (settings.enableProfileTelemetry && _performanceTelemetry == null)
            {
                _performanceTelemetry = new PerformanceTelemetryAspect();
                _performanceTelemetry.Configure(settings);
                HarmonicDiagnosticHub.Register(_performanceTelemetry);
            }
        }

        public static void Publish(in HarmonicDiagnosticEvent evt) => HarmonicDiagnosticHub.Publish(evt);

        private IEnumerator EnsureManifestInitSnapshotFallback()
        {
            yield return null;

            if (!HarmonicDiagnosticHub.InitSnapshotWritten)
            {
                HarmonicDiagnosticHub.RefreshManifestInit();
            }
        }
    }
}
