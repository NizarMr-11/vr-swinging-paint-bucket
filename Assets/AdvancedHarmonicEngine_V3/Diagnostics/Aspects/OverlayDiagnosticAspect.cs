using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class OverlayDiagnosticAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "Overlay";
        public int Order => 100;

        private HarmonicDiagnosticSession _session;
        private uint _lastActive;
        private uint _peak;
        private uint _lastCanvasHits;
        private float _smoothedFps = 60f;
        private string _logFilePath = string.Empty;
        private int _overlayFontSize = 14;
        private bool _visible = true;

        public void Configure(int fontSize, bool visible)
        {
            _overlayFontSize = fontSize;
            _visible = visible;
        }

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            _session = session;
        }

        public void OnDetach()
        {
            _session = null;
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

            if (diagnosticEvent.Type == HarmonicDiagnosticEventType.SessionStart
                && diagnosticEvent.Message.Contains("path="))
            {
                int idx = diagnosticEvent.Message.IndexOf("path=");
            }
        }

        public void DrawGui()
        {
            if (!_visible || _session?.Pipeline == null)
            {
                return;
            }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _smoothedFps = Mathf.Lerp(_smoothedFps, 1f / dt, 0.15f);

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = _overlayFontSize,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            var pipeline = _session.Pipeline;
            string text =
                $"<b>Harmonic AOP</b>\n" +
                $"Active: <b>{_lastActive}</b> / {pipeline.MaxCapacity}\n" +
                $"Peak: {_peak} | Canvas hits: {_lastCanvasHits}\n" +
                $"Sim: {pipeline.IsSimulationActive} | World-only: {pipeline.WorldFallingOnly}\n" +
                $"FPS: {_smoothedFps:F1} | Frame: {_session.FrameIndex}\n" +
                $"Log: {_logFilePath}";

            GUI.Box(new Rect(10f, 10f, Mathf.Min(760f, Screen.width - 20f), 120f), text, style);
        }

        public void SetLogFilePath(string path) => _logFilePath = path ?? string.Empty;
        public void SetPeak(uint peak) => _peak = peak;
        public void SetLastActive(uint active) => _lastActive = active;
    }
}
