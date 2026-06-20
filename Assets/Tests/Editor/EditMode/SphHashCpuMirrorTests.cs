using HarmonicEngine.Diagnostics;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class SphHashCpuMirrorTests
    {
        [Test]
        public void HashCell_MatchesGpuFormula_ForOriginCell()
        {
            int3 cell = new int3(0, 0, 0);
            uint hash = SphHashCpuMirror.HashCell(cell, gridResolution: 512);
            unchecked
            {
                uint expected = (0u ^ 0u ^ 0u) & 511u;
                Assert.AreEqual(expected, hash);
            }
        }

        [Test]
        public void LatticeSpawnSettings_TotalCount_IsProductOfDimensions()
        {
            var settings = new HarmonicLatticeSpawnSettings
            {
                gridDimensions = new int3(10, 10, 10)
            };
            Assert.AreEqual(1000, settings.TotalCount);
        }
    }
}
