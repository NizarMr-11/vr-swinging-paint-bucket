using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public static class HarmonicGpuCapabilityGuard
    {
        public static bool IsGpuPipelineSupported =>
            SystemInfo.supportsComputeShaders && SystemInfo.maxComputeBufferInputsCompute >= 8;

        public static bool TryValidate(out string reason)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                reason = "Compute shaders are not supported on this device.";
                return false;
            }

            if (SystemInfo.maxComputeBufferInputsCompute < 8)
            {
                reason = "Insufficient compute buffer bindings for Harmonic V3.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
