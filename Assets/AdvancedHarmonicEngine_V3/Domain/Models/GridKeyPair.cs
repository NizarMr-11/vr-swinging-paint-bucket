using System.Runtime.InteropServices;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GridKeyPair
    {
        public uint CellHash;
        public uint ParticleIndex;
    }
}
