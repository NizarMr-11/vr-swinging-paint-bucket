using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace HarmonicEngine.Domain.Models
{
    // Exactly 48 bytes (3x 16-byte float4 blocks) for GPU cache alignment.
    //   Block 1: Position (12) + Density (4)
    //   Block 2: Velocity (12) + Pressure (4)
    //   Block 3: PackedColorRGBA (4) + _Padding (12, reserved for mass/viscosity)
    // Color rides inside the struct so Append/Consume stream compaction never
    // separates it from its particle. Must match the HLSL FluidParticle in
    // Infrastructure/ComputeShaders/Include/SphCommon.hlsl and the ComputeBuffer
    // stride in PipelineExecutionController.
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct FluidParticle
    {
        public float3 Position;
        public float Density;
        public float3 Velocity;
        public float Pressure;
        public uint PackedColorRGBA;
        public float3 _Padding;
    }
}
