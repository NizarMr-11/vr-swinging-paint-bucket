namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Particle budget for HarmonicEngineLab only (see scene HarmonicPipelineRoot maxCapacity).
    /// Other scenes keep their own pipeline capacity / quality-tier presets.
    /// </summary>
    public static class HarmonicEngineLimits
    {
        public const int MaxParticles = 30_000;
    }

}
