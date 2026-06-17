using Unity.Mathematics;

namespace HarmonicEngine.Core.Mathematics.Quantization
{
    public static class HalfPrecisionCompressor
    {
        public static ushort ToHalf(float value)
        {
            return (ushort)math.f32tof16(value);
        }

        public static float FromHalf(ushort value)
        {
            return math.f16tof32(value);
        }
    }
}
