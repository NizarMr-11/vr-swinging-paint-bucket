using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class HarmonicEngineLimitsTests
    {
        [Test]
        public void SortGridSizeForCapacity_30000_PadsTo32768()
        {
            Assert.AreEqual(32768, HarmonicEngineLimits.SortGridSizeForCapacity(30_000));
        }

        [Test]
        public void SortGridSizeForCapacity_AlreadyPowerOfTwo_Unchanged()
        {
            Assert.AreEqual(8192, HarmonicEngineLimits.SortGridSizeForCapacity(8192));
        }
    }
}
