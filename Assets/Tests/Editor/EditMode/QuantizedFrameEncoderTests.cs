using HarmonicEngine.Domain.IO;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class QuantizedFrameEncoderTests
    {
        [Test]
        public void BuildFramePayload_PrefixesHeaderBeforeParticleBytes()
        {
            var particleBytes = new byte[32];
            for (int i = 0; i < particleBytes.Length; i++)
            {
                particleBytes[i] = (byte)(i + 1);
            }

            byte[] payload = QuantizedFrameEncoder.BuildFramePayload(particleBytes, 2, 42, 999);

            Assert.AreEqual(QuantizedFrameEncoder.ExpectedPayloadByteSize(2), payload.Length);
            QuantizedFrameEncoder.ReadHeader(payload, out uint count, out uint frameIndex, out ulong timestamp);
            Assert.AreEqual(2u, count);
            Assert.AreEqual(42u, frameIndex);
            Assert.AreEqual(999ul, timestamp);
            CollectionAssert.AreEqual(particleBytes, payload[16..(16 + particleBytes.Length)]);
        }

        [Test]
        public void ExpectedPayloadByteSize_MatchesHeaderPlusParticles()
        {
            Assert.AreEqual(16 + 32, QuantizedFrameEncoder.ExpectedPayloadByteSize(2));
        }
    }
}
