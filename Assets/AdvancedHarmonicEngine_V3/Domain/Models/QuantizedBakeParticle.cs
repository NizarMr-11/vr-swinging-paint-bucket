using System.Runtime.InteropServices;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct QuantizedBakeParticle
    {
        public ushort PositionX;
        public ushort PositionY;
        public ushort PositionZ;
        public ushort VelocityX;
        public ushort VelocityY;
        public ushort VelocityZ;
        public ushort Density;
        public ushort Pressure;
    }
}
