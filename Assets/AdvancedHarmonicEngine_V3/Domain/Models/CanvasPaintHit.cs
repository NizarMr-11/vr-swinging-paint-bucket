using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.Models
{
    // 32 bytes — must match CanvasPaintHit in FallingFluidWorld.compute and the canvas-hit buffer stride.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CanvasPaintHit
    {
        [FieldOffset(0)] public float3 WorldPosition;
        [FieldOffset(12)] public float PaintWeight;
        [FieldOffset(16)] public uint PackedColorRGBA;
        [FieldOffset(20)] public float WetnessDeposit;
    }
}
