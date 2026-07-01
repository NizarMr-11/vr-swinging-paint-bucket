namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    /// <summary>
    /// Per-frame simulation inputs shared across GPU passes.
    /// </summary>
    public sealed class HarmonicSimulationContext
    {
        public uint ActiveCount;
        public float DeltaTime;
        public int FrameSortSize;
    }
}
