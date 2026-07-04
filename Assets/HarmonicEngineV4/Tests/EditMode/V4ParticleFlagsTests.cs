using HarmonicEngineV4.Core;
using NUnit.Framework;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4ParticleFlagsTests
    {
        [Test]
        public void RoundTrip_AllFields()
        {
            uint flags = 0;
            flags = V4ParticleFlags.SetInside(flags, true);
            flags = V4ParticleFlags.SetZone(flags, V4Zone.HoleZone1);
            flags = V4ParticleFlags.SetHole(flags, 17);
            flags = V4ParticleFlags.SetProfile(flags, 200);

            Assert.IsTrue(V4ParticleFlags.IsInside(flags));
            Assert.IsFalse(V4ParticleFlags.HasEscaped(flags));
            Assert.AreEqual(V4Zone.HoleZone1, V4ParticleFlags.GetZone(flags));
            Assert.AreEqual(17, V4ParticleFlags.GetHole(flags));
            Assert.AreEqual(200, V4ParticleFlags.GetProfile(flags));
        }

        [Test]
        public void FieldsDoNotBleedIntoEachOther()
        {
            uint flags = 0;
            flags = V4ParticleFlags.SetProfile(flags, 255);
            flags = V4ParticleFlags.SetHole(flags, 31);
            flags = V4ParticleFlags.SetZone(flags, V4Zone.TopBand);

            Assert.AreEqual(255, V4ParticleFlags.GetProfile(flags));
            Assert.AreEqual(31, V4ParticleFlags.GetHole(flags));
            Assert.AreEqual(V4Zone.TopBand, V4ParticleFlags.GetZone(flags));
            Assert.IsFalse(V4ParticleFlags.IsInside(flags));
            Assert.IsFalse(V4ParticleFlags.HasEscaped(flags));

            flags = V4ParticleFlags.SetZone(flags, V4Zone.None);
            Assert.AreEqual(255, V4ParticleFlags.GetProfile(flags));
            Assert.AreEqual(31, V4ParticleFlags.GetHole(flags));
        }

        [Test]
        public void EscapedBit_IsOneWay_NoUnsetApiExists()
        {
            uint flags = V4ParticleFlags.SetEscaped(0);
            Assert.IsTrue(V4ParticleFlags.HasEscaped(flags));

            // Mutating every other field must never clear the latch.
            flags = V4ParticleFlags.SetInside(flags, true);
            flags = V4ParticleFlags.SetInside(flags, false);
            flags = V4ParticleFlags.SetZone(flags, V4Zone.HoleZone2);
            flags = V4ParticleFlags.SetHole(flags, 5);
            flags = V4ParticleFlags.SetProfile(flags, 9);
            Assert.IsTrue(V4ParticleFlags.HasEscaped(flags));
        }

        [Test]
        public void InsideBit_IsReversible()
        {
            uint flags = V4ParticleFlags.SetInside(0, true);
            Assert.IsTrue(V4ParticleFlags.IsInside(flags));
            flags = V4ParticleFlags.SetInside(flags, false);
            Assert.IsFalse(V4ParticleFlags.IsInside(flags));
        }
    }
}
