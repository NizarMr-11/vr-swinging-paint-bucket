using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class UnifiedProfileOverlayAspect : IHarmonicDiagnosticAspect, IParticleCountOverlaySink
    {
        public string AspectName => "UnifiedProfileOverlay";
        public int Order => 100;

        private HarmonicDiagnosticSession _session;
        private uint _lastActive;
        private uint _peak;
        private uint _lastCanvasHits;
        private float _smoothedFrameMs = 16.67f;
        private string _logFilePath = string.Empty;
        private int _overlayFontSize = 14;
        private int _smoothingFrames = 30;
        private float _spikeThresholdMs = 22f;
        private bool _visible = true;
        private bool _lastFrameSpike;
        private bool _frameTimingEnabled;

        private ProfilerRecorder _gridRecorder;
        private ProfilerRecorder _sortRecorder;
        private ProfilerRecorder _buildRangesRecorder;
        private ProfilerRecorder _densityRecorder;
        private ProfilerRecorder _integrationRecorder;
        private ProfilerRecorder _containerRecorder;
        private ProfilerRecorder _worldFallingRecorder;
        private ProfilerRecorder _ssfrRecorder;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        private readonly StringBuilder _sb = new(384);

        public bool IsVisible => _visible;

        public void Configure(
            int fontSize,
            bool visible,
            int smoothingFrames,
            float spikeThresholdMs,
            bool enableFrameTimingStats)
        {
            _overlayFontSize = fontSize;
            _visible = visible;
            _smoothingFrames = Mathf.Max(1, smoothingFrames);
            _spikeThresholdMs = spikeThresholdMs;
            _frameTimingEnabled = enableFrameTimingStats && FrameTimingManager.IsFeatureEnabled();
        }

        public void SetVisible(bool visible) => _visible = visible;

        public void SetLogFilePath(string path) => _logFilePath = path ?? string.Empty;

        public void SetPeak(uint peak) => _peak = peak;

        public void SetLastActive(uint active) => _lastActive = active;

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            _session = session;
            _gridRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SpatialHashGrid");
            _sortRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.BitonicSort");
            _buildRangesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.BuildRanges");
            _densityRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphDensity");
            _integrationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphIntegration");
            _containerRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.ContainerFluidFrame");
            _worldFallingRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.WorldFalling");
            _ssfrRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SSFR");
        }

        public void OnDetach()
        {
            _session = null;
            DisposeRecorder(ref _gridRecorder);
            DisposeRecorder(ref _sortRecorder);
            DisposeRecorder(ref _buildRangesRecorder);
            DisposeRecorder(ref _densityRecorder);
            DisposeRecorder(ref _integrationRecorder);
            DisposeRecorder(ref _containerRecorder);
            DisposeRecorder(ref _worldFallingRecorder);
            DisposeRecorder(ref _ssfrRecorder);
        }

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            _lastActive = diagnosticEvent.ActiveParticleCount;
            if (diagnosticEvent.PeakParticleCount > _peak)
            {
                _peak = diagnosticEvent.PeakParticleCount;
            }

            if (diagnosticEvent.CanvasHitCount > 0)
            {
                _lastCanvasHits = diagnosticEvent.CanvasHitCount;
            }

            if (diagnosticEvent.Category == "PERF" && diagnosticEvent.Message.StartsWith("spike=1"))
            {
                _lastFrameSpike = true;
            }
        }

        public void DrawGui()
        {
            if (!_visible || _session?.Pipeline == null)
            {
                return;
            }

            float frameMs = Time.unscaledDeltaTime * 1000f;
            float smoothT = 1f / Mathf.Max(1, _smoothingFrames);
            _smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs, frameMs, smoothT);
            if (frameMs >= _spikeThresholdMs)
            {
                _lastFrameSpike = true;
            }

            float fps = _smoothedFrameMs > 0.001f ? 1000f / _smoothedFrameMs : 0f;
            var pipeline = _session.Pipeline;

            _sb.Clear();
            _sb.AppendLine("<b>Harmonic Diagnostics</b>");
            _sb.AppendLine($"FPS {fps:F0}   frame {_smoothedFrameMs:F2} ms");

            if (_frameTimingEnabled)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
                {
                    FrameTiming timing = _frameTimings[0];
                    _sb.AppendLine($"GPU {timing.gpuFrameTime:F2} ms   CPU main {timing.cpuFrameTime:F2} ms");
                }
            }

            if (_lastFrameSpike)
            {
                _sb.AppendLine("<color=#ff6666><b>SPIKE</b> last frame exceeded threshold</color>");
                _lastFrameSpike = false;
            }

            _sb.AppendLine(
                $"Active <b>{_lastActive}</b> / {pipeline.MaxCapacity}   peak {_peak}   canvas {_lastCanvasHits}");
            _sb.AppendLine(
                $"Sort {_session.Pipeline.FrameSortSize} / {pipeline.PaddedSortSize}   frame {_session.FrameIndex}");
            _sb.AppendLine(
                $"CPU ms  grid {ToMs(_gridRecorder):F2}  sort {ToMs(_sortRecorder):F2}  ranges {ToMs(_buildRangesRecorder):F2}");
            _sb.AppendLine(
                $"        density {ToMs(_densityRecorder):F2}  integ {ToMs(_integrationRecorder):F2}  container {ToMs(_containerRecorder):F2}");
            _sb.AppendLine(
                $"        world {ToMs(_worldFallingRecorder):F2}  ssfr {ToMs(_ssfrRecorder):F2}");
            _sb.AppendLine($"Log: {TruncatePath(_logFilePath)}");

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = _overlayFontSize,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            GUI.Box(new Rect(10f, 10f, Mathf.Min(780f, Screen.width - 20f), 210f), _sb.ToString(), style);
        }

        private static string TruncatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "(none)";
            }

            const int maxLen = 72;
            return path.Length <= maxLen ? path : "..." + path.Substring(path.Length - (maxLen - 3));
        }

        private static double ToMs(ProfilerRecorder recorder) =>
            recorder.Valid ? recorder.LastValue * 1e-6 : 0.0;

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
            {
                recorder.Dispose();
            }

            recorder = default;
        }
    }
}
