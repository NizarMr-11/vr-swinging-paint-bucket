using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Gpu
{
    public static class HarmonicActiveCountUtility
    {
        public static uint SanitizeCount(uint raw, int maxCapacity) =>
            raw > (uint)maxCapacity ? (uint)maxCapacity : raw;

        public static uint FetchActiveCount(ComputeBuffer counterSource, ComputeBuffer counterReadbackBuffer, uint[] cpuScratch)
        {
            ComputeBuffer.CopyCount(counterSource, counterReadbackBuffer, 0);
            counterReadbackBuffer.GetData(cpuScratch);
            return cpuScratch[0];
        }

        public static uint FetchActiveCount(ParticleSoaBuffers soa, ComputeBuffer counterReadbackBuffer, uint[] cpuScratch) =>
            FetchActiveCount(soa.CounterBuffer, counterReadbackBuffer, cpuScratch);

        public static uint SanitizeAndRepairCount(
            ComputeBuffer source,
            int maxCapacity,
            ComputeBuffer counterReadbackBuffer,
            uint[] cpuScratch,
            ref bool capacityClampWarningLogged)
        {
            uint raw = FetchActiveCount(source, counterReadbackBuffer, cpuScratch);
            if (raw <= (uint)maxCapacity)
            {
                capacityClampWarningLogged = false;
                return raw;
            }

            if (!capacityClampWarningLogged)
            {
                capacityClampWarningLogged = true;
                Debug.LogWarning(
                    $"[HarmonicPipeline] Active count {raw} exceeded maxCapacity {maxCapacity}; clamping. " +
                    "If this persists after a fix, use Clear All / restart Play.");
            }

            source.SetCounterValue((uint)maxCapacity);
            return (uint)maxCapacity;
        }

        public static uint SanitizeAndRepairCount(
            ParticleSoaBuffers soa,
            int maxCapacity,
            ComputeBuffer counterReadbackBuffer,
            uint[] cpuScratch,
            ref bool capacityClampWarningLogged) =>
            SanitizeAndRepairCount(soa.CounterBuffer, maxCapacity, counterReadbackBuffer, cpuScratch, ref capacityClampWarningLogged);
    }
}
