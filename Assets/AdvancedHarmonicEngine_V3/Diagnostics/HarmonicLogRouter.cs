namespace HarmonicEngine.Diagnostics
{
    public static class HarmonicLogRouter
    {
        public static HarmonicLogChannel Route(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent.Type is HarmonicDiagnosticEventType.SessionStart
                or HarmonicDiagnosticEventType.SessionEnd)
            {
                return HarmonicLogChannel.Session;
            }

            if (diagnosticEvent.Category == "RAIN"
                || diagnosticEvent.Type is HarmonicDiagnosticEventType.CountdownTick
                    or HarmonicDiagnosticEventType.RainStart
                    or HarmonicDiagnosticEventType.SpawnBurst)
            {
                return HarmonicLogChannel.Rain;
            }

            if (diagnosticEvent.Category == "SPH" || ContainsSphMarker(diagnosticEvent.Message))
            {
                return HarmonicLogChannel.Sph;
            }

            if (diagnosticEvent.Category == "TELEMETRY")
            {
                return HarmonicLogChannel.Telemetry;
            }

            if (diagnosticEvent.Category == "PERF")
            {
                return HarmonicLogChannel.Perf;
            }

            if (diagnosticEvent.Category == "PIPELINE"
                || diagnosticEvent.Type is HarmonicDiagnosticEventType.ParticlesAppended
                    or HarmonicDiagnosticEventType.ParticlesCleared
                    or HarmonicDiagnosticEventType.SimulationStateChanged
                    or HarmonicDiagnosticEventType.PipelineFrameBefore
                    or HarmonicDiagnosticEventType.PipelineFrameAfter
                    or HarmonicDiagnosticEventType.ShapeEmit)
            {
                return HarmonicLogChannel.Pipeline;
            }

            if (diagnosticEvent.Type == HarmonicDiagnosticEventType.PipelineStage)
            {
                return HarmonicLogChannel.Engine;
            }

            if (diagnosticEvent.Category == "HUB")
            {
                return HarmonicLogChannel.Session;
            }

            return HarmonicLogChannel.Pipeline;
        }

        private static bool ContainsSphMarker(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.Contains("[SPH CFL]")
                || message.Contains("[SPH HASH]")
                || message.Contains("[SPH stencil");
        }
    }
}
