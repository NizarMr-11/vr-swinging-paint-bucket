using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    /// <summary>
    /// Tracks particle count deltas and periodic samples; drives overlay peak/active values.
    /// </summary>
    public sealed class ParticleTelemetryAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "ParticleTelemetry";
        public int Order => 10;

        private HarmonicDiagnosticSession _session;
        private IParticleCountOverlaySink _overlay;
        private uint _peak;
        private uint _lastPublished;
        private float _sampleTimer;
        private float _sampleInterval = 0.25f;
        private bool _logToConsole;

        public void Configure(float sampleIntervalSeconds, bool logToConsole, IParticleCountOverlaySink overlay)
        {
            _sampleInterval = Mathf.Max(0.05f, sampleIntervalSeconds);
            _logToConsole = logToConsole;
            _overlay = overlay;
        }

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            _session = session;
            _peak = 0;
            _lastPublished = 0;
            _sampleTimer = 0f;
        }

        public void OnDetach() => _session = null;

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent.Category == "TELEMETRY")
            {
                return;
            }

            uint active = diagnosticEvent.ActiveParticleCount;
            if (active == 0 && _session != null)
            {
                active = _session.ReadActiveParticleCount();
            }

            if (active > _peak)
            {
                _peak = active;
            }

            _overlay?.SetPeak(_peak);
            _overlay?.SetLastActive(active);

            bool countChanged = active != _lastPublished;
            bool important = diagnosticEvent.Type is HarmonicDiagnosticEventType.SpawnBurst
                or HarmonicDiagnosticEventType.ParticlesAppended
                or HarmonicDiagnosticEventType.ParticlesCleared
                or HarmonicDiagnosticEventType.RainStart
                or HarmonicDiagnosticEventType.PipelineFrameAfter;

            if (countChanged && important)
            {
                PublishDelta(diagnosticEvent, active, "COUNT_CHANGE");
                _lastPublished = active;
                return;
            }

            if (diagnosticEvent.Type == HarmonicDiagnosticEventType.PipelineFrameAfter)
            {
                _sampleTimer += Time.unscaledDeltaTime;
                if (_sampleTimer >= _sampleInterval)
                {
                    _sampleTimer = 0f;
                    PublishDelta(diagnosticEvent, active, "PERIODIC");
                    _lastPublished = active;
                }
            }
        }

        private void PublishDelta(in HarmonicDiagnosticEvent source, uint active, string tag)
        {
            int delta = (int)active - (int)_lastPublished;
            string msg = $"{tag} delta={delta} total={active} peak={_peak}";
            var evt = new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineFrameAfter,
                "TELEMETRY",
                msg,
                source.FrameIndex,
                source.TimeSeconds,
                active,
                _peak,
                source.CanvasHitCount);

            if (_logToConsole)
            {
                Debug.Log($"[HarmonicTelemetry] {msg}");
            }

            HarmonicDiagnosticHub.Publish(evt);
        }
    }
}
