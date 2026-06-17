using HarmonicEngine.Core.Mathematics.Quantization;
using HarmonicEngine.Domain.IO;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class QuantizedBakeDecoderTests
    {
        [Test]
        public void DecodeParticle_RoundTripsPackedLayout()
        {
            float3 origin = new float3(10f, -6f, 2f);
            float px = 0.5f;
            float py = -0.25f;
            float pz = 1.0f;
            float vx = 0.1f;
            float vy = -0.2f;
            float vz = 0.3f;
            float d = 1000f;
            float pr = 50f;

            ushort upx = HalfPrecisionCompressor.ToHalf(px);
            ushort upy = HalfPrecisionCompressor.ToHalf(py);
            ushort upz = HalfPrecisionCompressor.ToHalf(pz);
            ushort uvx = HalfPrecisionCompressor.ToHalf(vx);
            ushort uvy = HalfPrecisionCompressor.ToHalf(vy);
            ushort uvz = HalfPrecisionCompressor.ToHalf(vz);
            ushort ud = HalfPrecisionCompressor.ToHalf(d);
            ushort upr = HalfPrecisionCompressor.ToHalf(pr);

            uint packed0Lo = (uint)((upy << 16) | upx);
            uint packed0Hi = (uint)((uvx << 16) | upz);
            uint packed1Lo = (uint)((uvz << 16) | uvy);
            uint packed1Hi = (uint)((upr << 16) | ud);

            var bytes = new byte[16];
            WriteUInt(bytes, 0, packed0Lo);
            WriteUInt(bytes, 4, packed0Hi);
            WriteUInt(bytes, 8, packed1Lo);
            WriteUInt(bytes, 12, packed1Hi);

            QuantizedBakeDecoder.DecodeParticle(bytes, origin, out float3 pos, out float3 vel, out float density, out float pressure);

            Assert.AreEqual(origin.x + px, pos.x, 0.05f);
            Assert.AreEqual(origin.y + py, pos.y, 0.05f);
            Assert.AreEqual(origin.z + pz, pos.z, 0.05f);
            Assert.AreEqual(vx, vel.x, 0.05f);
            Assert.AreEqual(density, d, 1f);
        }

        [Test]
        public void DecodeAllParticles_ReadsHeaderCount()
        {
            var particleBytes = new byte[QuantizedBakeDecoder.BytesPerParticle];
            byte[] frame = QuantizedFrameEncoder.BuildFramePayload(particleBytes, 1, 7, 0);
            var positions = new float3[4];
            float3 origin = float3.zero;

            int count = QuantizedBakeDecoder.DecodeAllParticles(frame, origin, positions, 4);
            Assert.AreEqual(1, count);
        }

        private static void WriteUInt(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
