namespace HarmonicEngine.Diagnostics
{
    public readonly struct HarmonicDiagnosticEvent
    {
        public HarmonicDiagnosticEventType Type { get; }
        public string Category { get; }
        public string Message { get; }
        public int FrameIndex { get; }
        public float TimeSeconds { get; }
        public uint ActiveParticleCount { get; }
        public uint PeakParticleCount { get; }
        public uint CanvasHitCount { get; }
        public int IntArg0 { get; }
        public int IntArg1 { get; }
        public bool BoolArg0 { get; }

        public HarmonicDiagnosticEvent(
            HarmonicDiagnosticEventType type,
            string category,
            string message,
            int frameIndex,
            float timeSeconds,
            uint activeParticleCount = 0,
            uint peakParticleCount = 0,
            uint canvasHitCount = 0,
            int intArg0 = 0,
            int intArg1 = 0,
            bool boolArg0 = false)
        {
            Type = type;
            Category = category;
            Message = message ?? string.Empty;
            FrameIndex = frameIndex;
            TimeSeconds = timeSeconds;
            ActiveParticleCount = activeParticleCount;
            PeakParticleCount = peakParticleCount;
            CanvasHitCount = canvasHitCount;
            IntArg0 = intArg0;
            IntArg1 = intArg1;
            BoolArg0 = boolArg0;
        }
    }
}
