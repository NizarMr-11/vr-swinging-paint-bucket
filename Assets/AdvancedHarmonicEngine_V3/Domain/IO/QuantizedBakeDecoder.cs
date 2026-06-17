using System;
using HarmonicEngine.Core.Mathematics.Quantization;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.IO
{
    /// <summary>
    /// Decodes GPU-packed quantized frames (matches DataCompactionPacker.compute layout).
    /// </summary>
    public static class QuantizedBakeDecoder
    {
        public const int BytesPerParticle = 16;

        public static void DecodeParticle(
            ReadOnlySpan<byte> particleBytes,
            float3 quantizationOrigin,
            out float3 worldPosition,
            out float3 velocity,
            out float density,
            out float pressure)
        {
            if (particleBytes.Length < BytesPerParticle)
            {
                throw new ArgumentException("Particle span too small.", nameof(particleBytes));
            }

            uint packed0Lo = ReadUInt32(particleBytes, 0);
            uint packed0Hi = ReadUInt32(particleBytes, 4);
            uint packed1Lo = ReadUInt32(particleBytes, 8);
            uint packed1Hi = ReadUInt32(particleBytes, 12);

            float px = HalfPrecisionCompressor.FromHalf((ushort)(packed0Lo & 0xFFFF));
            float py = HalfPrecisionCompressor.FromHalf((ushort)(packed0Lo >> 16));
            float pz = HalfPrecisionCompressor.FromHalf((ushort)(packed0Hi & 0xFFFF));
            float vx = HalfPrecisionCompressor.FromHalf((ushort)(packed0Hi >> 16));
            float vy = HalfPrecisionCompressor.FromHalf((ushort)(packed1Lo & 0xFFFF));
            float vz = HalfPrecisionCompressor.FromHalf((ushort)(packed1Lo >> 16));
            float pr = HalfPrecisionCompressor.FromHalf((ushort)(packed1Hi >> 16));
            float d = HalfPrecisionCompressor.FromHalf((ushort)(packed1Hi & 0xFFFF));

            worldPosition = quantizationOrigin + new float3(px, py, pz);
            velocity = new float3(vx, vy, vz);
            density = d;
            pressure = pr;
        }

        public static int DecodeAllParticles(
            ReadOnlySpan<byte> framePayload,
            float3 quantizationOrigin,
            float3[] positionsOut,
            int maxCount)
        {
            if (framePayload.Length < QuantizedFrameEncoder.HeaderByteSize)
            {
                return 0;
            }

            QuantizedFrameEncoder.ReadHeader(framePayload, out uint count, out _, out _);
            int particleCount = (int)math.min(count, (uint)maxCount);
            ReadOnlySpan<byte> particles = framePayload.Slice(QuantizedFrameEncoder.HeaderByteSize);

            for (int i = 0; i < particleCount; i++)
            {
                int offset = i * BytesPerParticle;
                if (offset + BytesPerParticle > particles.Length)
                {
                    break;
                }

                DecodeParticle(
                    particles.Slice(offset, BytesPerParticle),
                    quantizationOrigin,
                    out float3 pos,
                    out _,
                    out _,
                    out _);
                positionsOut[i] = pos;
            }

            return particleCount;
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
            (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));
    }
}
