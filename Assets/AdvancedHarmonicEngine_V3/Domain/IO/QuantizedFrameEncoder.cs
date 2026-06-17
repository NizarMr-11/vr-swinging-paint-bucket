using System;
using System.Runtime.InteropServices;

namespace HarmonicEngine.Domain.IO
{
    public static class QuantizedFrameEncoder
    {
        public const int HeaderByteSize = 16;

        public static byte[] BuildFramePayload(
            byte[] quantizedParticleBytes,
            uint particleCount,
            uint frameIndex,
            ulong timestampUtcTicks)
        {
            if (quantizedParticleBytes == null)
            {
                throw new ArgumentNullException(nameof(quantizedParticleBytes));
            }

            return BuildFramePayload(quantizedParticleBytes.AsSpan(), particleCount, frameIndex, timestampUtcTicks);
        }

        public static byte[] BuildFramePayload(
            ReadOnlySpan<byte> quantizedParticleBytes,
            uint particleCount,
            uint frameIndex,
            ulong timestampUtcTicks)
        {
            int payloadSize = HeaderByteSize + quantizedParticleBytes.Length;
            var payload = new byte[payloadSize];
            WriteHeader(payload, particleCount, frameIndex, timestampUtcTicks);
            quantizedParticleBytes.CopyTo(payload.AsSpan(HeaderByteSize));
            return payload;
        }

        public static void WriteHeader(byte[] buffer, uint particleCount, uint frameIndex, ulong timestampUtcTicks)
        {
            if (buffer == null || buffer.Length < HeaderByteSize)
            {
                throw new ArgumentException("Buffer too small for frame header.", nameof(buffer));
            }

            WriteUInt32(buffer, 0, particleCount);
            WriteUInt32(buffer, 4, frameIndex);
            WriteUInt64(buffer, 8, timestampUtcTicks);
        }

        public static void ReadHeader(ReadOnlySpan<byte> buffer, out uint particleCount, out uint frameIndex, out ulong timestampUtcTicks)
        {
            if (buffer.Length < HeaderByteSize)
            {
                throw new ArgumentException("Buffer too small for frame header.", nameof(buffer));
            }

            particleCount = ReadUInt32(buffer, 0);
            frameIndex = ReadUInt32(buffer, 4);
            timestampUtcTicks = ReadUInt64(buffer, 8);
        }

        public static int ExpectedPayloadByteSize(uint particleCount) =>
            HeaderByteSize + (int)particleCount * 16;

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64(byte[] buffer, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
            (uint)(buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24));

        private static ulong ReadUInt64(ReadOnlySpan<byte> buffer, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
            {
                value |= (ulong)buffer[offset + i] << (8 * i);
            }

            return value;
        }
    }
}
