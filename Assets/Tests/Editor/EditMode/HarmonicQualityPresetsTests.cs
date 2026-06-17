using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class HarmonicQualityPresetsTests
    {
        [Test]
        public void GetParticleCapacity_MatchesPlanMilestones()
        {
            Assert.AreEqual(100_000, HarmonicQualityPresets.GetParticleCapacity(HarmonicQualityTier.Low));
            Assert.AreEqual(500_000, HarmonicQualityPresets.GetParticleCapacity(HarmonicQualityTier.Medium));
            Assert.AreEqual(1_000_000, HarmonicQualityPresets.GetParticleCapacity(HarmonicQualityTier.High));
            Assert.AreEqual(5_000_000, HarmonicQualityPresets.GetParticleCapacity(HarmonicQualityTier.Cinematic));
        }
    }
}
