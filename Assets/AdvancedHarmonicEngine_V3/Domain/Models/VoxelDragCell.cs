using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VoxelDragCell
    {
        public float3 Velocity;
        public float Drag;
    }
}
