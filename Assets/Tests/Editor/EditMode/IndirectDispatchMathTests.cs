using HarmonicEngine.Core.Utilities;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class IndirectDispatchMathTests
    {
        [TestCase(0u, 0u)]
        [TestCase(1u, 1u)]
        [TestCase(64u, 1u)]
        [TestCase(65u, 2u)]
        [TestCase(4096u, 64u)]
        public void CalculateThreadGroupsX_MatchesCeilDiv64(uint rawCount, uint expectedGroups)
        {
            Assert.AreEqual(expectedGroups, IndirectDispatchMath.CalculateThreadGroupsX(rawCount));
        }

        [Test]
        public void ThreadsPerGroup_Is64()
        {
            Assert.AreEqual(64, IndirectDispatchMath.ThreadsPerGroup);
        }
    }
}
