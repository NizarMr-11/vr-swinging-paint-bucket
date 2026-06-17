using HarmonicEngine.Core.DataStructures;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class GPUIndirectSortBinderTests
    {
        [TestCase(1, 1)]
        [TestCase(1000, 1024)]
        [TestCase(1024, 1024)]
        [TestCase(1025, 2048)]
        [TestCase(5_000_000, 8_388_608)]
        public void CalculatePaddedSortSize_ReturnsNextPowerOfTwo(int capacity, int expected)
        {
            Assert.AreEqual(expected, GPUIndirectSortBinder.CalculatePaddedSortSize(capacity));
        }
    }
}
