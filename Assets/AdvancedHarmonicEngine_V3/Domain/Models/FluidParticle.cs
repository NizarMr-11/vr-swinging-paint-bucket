using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FluidParticle
    {
        public float3 Position;
        public float Density;
        public float3 Velocity;
        public float Pressure;
    }
}
