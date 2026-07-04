using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Device capability detection (spec section 8.3): picks which PassManifest variant
    /// to load based on the actual GPU the app is running on.
    /// </summary>
    public static class V4DeviceCaps
    {
        public static bool SupportsCompute => SystemInfo.supportsComputeShaders;

        public static V4DeviceTier DetectTier()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                V4Log.Error(V4LogCategory.General, "device does not support compute shaders");
                return V4DeviceTier.Split;
            }

            string gpuName = SystemInfo.graphicsDeviceName ?? string.Empty;
            bool integratedIntel = gpuName.IndexOf("Intel", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool lowShaderLevel = SystemInfo.graphicsShaderLevel < 50;
            bool lowMemory = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2048;

            V4DeviceTier tier = (integratedIntel || lowShaderLevel || lowMemory)
                ? V4DeviceTier.Split
                : V4DeviceTier.Combined;

            V4Log.Info(V4LogCategory.General,
                $"device tier={tier} gpu='{gpuName}' shaderLevel={SystemInfo.graphicsShaderLevel} vramMb={SystemInfo.graphicsMemorySize} api={SystemInfo.graphicsDeviceType}");
            return tier;
        }

        public static V4PassManifest ChooseManifest(V4PassManifest split, V4PassManifest combined)
        {
            V4DeviceTier tier = DetectTier();
            V4PassManifest chosen = tier == V4DeviceTier.Combined && combined != null ? combined : split;
            if (chosen == null)
            {
                chosen = combined;
            }

            V4Log.Info(V4LogCategory.General, $"manifest chosen: {(chosen != null ? chosen.manifestName : "<none>")} (tier {tier})");
            return chosen;
        }
    }
}
