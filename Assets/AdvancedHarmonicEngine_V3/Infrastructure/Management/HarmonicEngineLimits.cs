namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Lab-scene particle budget helpers. GPU sort/grid uses the next power of two internally;
    /// particle buffers honor <c>maxCapacity</c> exactly.
    /// </summary>
    public static class HarmonicEngineLimits
    {
        public const int MaxParticles = 30_000;

        /// <summary>Sort/grid padding (bitonic sort requires power-of-two width).</summary>
        public static int SortGridSizeForCapacity(int capacity) =>
            UnityEngine.Mathf.NextPowerOfTwo(UnityEngine.Mathf.Max(1, capacity));
    }
}
