using UnityEngine;

namespace HarmonicEngine.Core.DataStructures
{
    public static class GPUIndirectSortBinder
    {
        public static int CalculatePaddedSortSize(int capacity)
        {
            return Mathf.NextPowerOfTwo(Mathf.Max(1, capacity));
        }
    }
}
