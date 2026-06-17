namespace HarmonicEngine.Core.Utilities
{
    /// <summary>
    /// CPU mirror of CalculateGridArgsKernel (architecture §4.1 / §5.1).
    /// </summary>
    public static class IndirectDispatchMath
    {
        public const int ThreadsPerGroup = 64;

        public static uint CalculateThreadGroupsX(uint rawElementCount)
        {
            return (rawElementCount + (uint)ThreadsPerGroup - 1u) / (uint)ThreadsPerGroup;
        }
    }
}
