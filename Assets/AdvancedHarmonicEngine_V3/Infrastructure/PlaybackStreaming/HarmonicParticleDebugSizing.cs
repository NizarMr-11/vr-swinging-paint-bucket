using HarmonicEngine.Infrastructure.Management;

namespace HarmonicEngine.Infrastructure.PlaybackStreaming
{
    /// <summary>
    /// Shared point-radius logic for <see cref="HarmonicParticleDebugRenderer"/> and tests.
    /// </summary>
    public static class HarmonicParticleDebugSizing
    {
        public static float ResolvePointRadius(
            IHarmonicParticleSource source,
            bool autoSizeFromSph,
            float pointSizeMultiplier,
            float manualPointSize)
        {
            if (autoSizeFromSph && source != null)
            {
                return source.SmoothingRadius * pointSizeMultiplier;
            }

            return manualPointSize;
        }
    }
}
