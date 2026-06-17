using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.Models
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct CanvasPaintHit
    {
        public float3 WorldPosition;
        public float PaintWeight;
    }
}
