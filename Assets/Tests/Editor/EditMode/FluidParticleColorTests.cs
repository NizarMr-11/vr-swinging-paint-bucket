using HarmonicEngine.Domain.Adapters;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class FluidParticleColorTests
    {
        [Test]
        public void PackColor_UnpackColor_RoundTripsCommonColors()
        {
            AssertColorRoundTrip(Color.red);
            AssertColorRoundTrip(Color.green);
            AssertColorRoundTrip(Color.blue);
            AssertColorRoundTrip(new Color(0.2f, 0.4f, 0.9f, 0.75f));
            AssertColorRoundTrip(FluidParticleFactory.UnpackColor(FluidParticleFactory.WhiteRGBA));
        }

        [Test]
        public void WhiteRGBA_MatchesOpaqueWhite()
        {
            Color white = FluidParticleFactory.UnpackColor(FluidParticleFactory.WhiteRGBA);
            Assert.AreEqual(1f, white.r, 1e-3f);
            Assert.AreEqual(1f, white.g, 1e-3f);
            Assert.AreEqual(1f, white.b, 1e-3f);
            Assert.AreEqual(1f, white.a, 1e-3f);
        }

        private static void AssertColorRoundTrip(Color original)
        {
            uint packed = FluidParticleFactory.PackColor(original);
            Color restored = FluidParticleFactory.UnpackColor(packed);
            Assert.AreEqual(original.r, restored.r, 1f / 255f);
            Assert.AreEqual(original.g, restored.g, 1f / 255f);
            Assert.AreEqual(original.b, restored.b, 1f / 255f);
            Assert.AreEqual(original.a, restored.a, 1f / 255f);
        }
    }
}
