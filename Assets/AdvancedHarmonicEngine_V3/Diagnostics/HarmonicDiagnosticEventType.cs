namespace HarmonicEngine.Diagnostics
{
    public enum HarmonicDiagnosticEventType
    {
        SessionStart,
        SessionEnd,
        PipelineFrameBefore,
        PipelineFrameAfter,
        PipelineStage,
        ParticlesAppended,
        ParticlesCleared,
        SimulationStateChanged,
        CountdownTick,
        RainStart,
        SpawnBurst,
        ShapeEmit
    }
}
