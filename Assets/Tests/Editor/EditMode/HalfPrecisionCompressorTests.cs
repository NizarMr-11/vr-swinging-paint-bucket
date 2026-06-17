using HarmonicEngine.Core.Mathematics.Quantization;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class HalfPrecisionCompressorTests
    {
        [TestCase(0f)]
        [TestCase(1.25f)]
        [TestCase(-2.5f)]
        [TestCase(100f)]
        public void RoundTrip_PreservesApproximateValue(float value)
        {
            ushort half = HalfPrecisionCompressor.ToHalf(value);
            float restored = HalfPrecisionCompressor.FromHalf(half);
            Assert.AreEqual(value, restored, 0.05f);
        }
    }
}
