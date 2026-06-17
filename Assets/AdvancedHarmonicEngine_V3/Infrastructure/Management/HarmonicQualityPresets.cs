namespace HarmonicEngine.Infrastructure.Management
{
    public enum HarmonicQualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Cinematic = 3
    }

    public static class HarmonicQualityPresets
    {
        public const int LowCapacity = 100_000;
        public const int MediumCapacity = 500_000;
        public const int HighCapacity = 1_000_000;
        public const int CinematicCapacity = 5_000_000;

        public static int GetParticleCapacity(HarmonicQualityTier tier) =>
            tier switch
            {
                HarmonicQualityTier.Low => LowCapacity,
                HarmonicQualityTier.Medium => MediumCapacity,
                HarmonicQualityTier.High => HighCapacity,
                HarmonicQualityTier.Cinematic => CinematicCapacity,
                _ => MediumCapacity
            };

        public static float GetTargetFrameRate(HarmonicQualityTier tier) =>
            tier switch
            {
                HarmonicQualityTier.Low => 60f,
                HarmonicQualityTier.Medium => 60f,
                HarmonicQualityTier.High => 30f,
                HarmonicQualityTier.Cinematic => 15f,
                _ => 60f
            };
    }
}
