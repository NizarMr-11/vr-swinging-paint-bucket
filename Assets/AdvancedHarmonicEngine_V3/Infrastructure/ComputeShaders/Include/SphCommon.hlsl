#ifndef HARMONIC_SPH_COMMON_INCLUDED
#define HARMONIC_SPH_COMMON_INCLUDED

// =============================================================================
//  SphCommon.hlsl - single source of truth for the GPU SPH data layout + math.
//
//  Included by every compute shader that touches particles or the spatial hash
//  (StreamCompactionPingPong.compute, SpatialHashGridIndirect.compute, ...).
//  Contains ONLY buffer-independent declarations (structs, constants, pure
//  functions) so it can be shared without resource-binding conflicts. Buffer-
//  bound neighbor iteration lives in SphNeighborQuery.hlsl.
//
//  IMPORTANT: the structs below are the GPU mirror of the C# blittable structs
//  in HarmonicEngine.Domain.Models. If you change a layout here you MUST change
//  the matching C# struct and the ComputeBuffer stride in
//  PipelineExecutionController, or the GPU buffers silently corrupt.
// =============================================================================

// 48-byte particle (3x 16-byte float4 blocks) - matches FluidParticle.cs.
//   Block 1: Position (12) + Density (4)
//   Block 2: Velocity (12) + Pressure (4)
//   Block 3: PackedColorRGBA (4) + _Padding (12, reserved for mass/viscosity)
struct FluidParticle
{
    float3 Position;
    float Density;
    float3 Velocity;
    float Pressure;
    uint PackedColorRGBA;
    float3 _Padding;
};

struct HashCellGridRange
{
    int StartIndex;
    int EndIndex;
};

struct GridKeyPair
{
    uint CellHash;
    uint ParticleIndex;
};

// 3x3x3 stencil (including the self cell) covering every cell that can hold a
// neighbor within the 2*h cubic-spline support radius when h = cellSize (support = 2h = 2 * cellSize).
static const int3 kNeighborOffsets[27] =
{
    int3(-1, -1, -1), int3(0, -1, -1), int3(1, -1, -1),
    int3(-1, 0, -1), int3(0, 0, -1), int3(1, 0, -1),
    int3(-1, 1, -1), int3(0, 1, -1), int3(1, 1, -1),
    int3(-1, -1, 0), int3(0, -1, 0), int3(1, -1, 0),
    int3(-1, 0, 0), int3(0, 0, 0), int3(1, 0, 0),
    int3(-1, 1, 0), int3(0, 1, 0), int3(1, 1, 0),
    int3(-1, -1, 1), int3(0, -1, 1), int3(1, -1, 1),
    int3(-1, 0, 1), int3(0, 0, 1), int3(1, 0, 1),
    int3(-1, 1, 1), int3(0, 1, 1), int3(1, 1, 1)
};

// -----------------------------------------------------------------------------
//  Spatial hash helpers (pure - resolution/cell size passed explicitly so the
//  grid-build pass and the neighbor-query pass provably use identical math).
// -----------------------------------------------------------------------------
int3 SphCellFromPosition(float3 position, float cellSize)
{
    return (int3)floor(position / max(cellSize, 1e-4));
}

uint SphHashCell(int3 cell, uint gridResolution)
{
    uint x = (uint)(cell.x * 73856093);
    uint y = (uint)(cell.y * 19349663);
    uint z = (uint)(cell.z * 83492791);
    return (x ^ y ^ z) & (gridResolution - 1u);
}

uint SphHashPosition(float3 position, float cellSize, uint gridResolution)
{
    return SphHashCell(SphCellFromPosition(position, cellSize), gridResolution);
}

// -----------------------------------------------------------------------------
//  RGBA8 color packing. Color rides inside FluidParticle (one uint) so stream
//  compaction never separates it from its particle. Unpack to float3 for
//  arithmetic (e.g. SPH color diffusion), then repack.
// -----------------------------------------------------------------------------
float3 UnpackUintToFloat3(uint packed)
{
    float r = (float)(packed & 0xFFu);
    float g = (float)((packed >> 8) & 0xFFu);
    float b = (float)((packed >> 16) & 0xFFu);
    return float3(r, g, b) / 255.0;
}

uint PackFloat3ToUint(float3 color)
{
    float3 c = saturate(color) * 255.0 + 0.5;
    uint r = (uint)c.r;
    uint g = (uint)c.g;
    uint b = (uint)c.b;
    return (r & 0xFFu) | ((g & 0xFFu) << 8) | ((b & 0xFFu) << 16) | (0xFFu << 24);
}

// -----------------------------------------------------------------------------
//  Cubic spline smoothing kernel and derivatives (support radius 2*h).
// -----------------------------------------------------------------------------
float CubicSplineKernel(float r, float h)
{
    float k = 8.0 / (3.14159265 * h * h * h);
    float q = r / max(h, 1e-4);
    if (q <= 1.0)
    {
        float t = 1.0 - q;
        return k * (t * t * t);
    }
    if (q <= 2.0)
    {
        float t = 2.0 - q;
        return k * 0.125 * (t * t * t);
    }
    return 0.0;
}

float3 CubicSplineGradient(float3 diff, float r, float h)
{
    float k = 8.0 / (3.14159265 * h * h * h);
    float q = r / max(h, 1e-4);
    if (r < 1e-6 || q > 2.0)
    {
        return float3(0, 0, 0);
    }

    float3 dir = diff / r;
    if (q <= 1.0)
    {
        return -dir * k * 3.0 * q * q / max(h, 1e-4);
    }

    float t = 2.0 - q;
    return -dir * k * (-0.375) * (t * t) / max(h, 1e-4);
}

float CubicSplineLaplacian(float r, float h)
{
    float k = 8.0 / (3.14159265 * h * h * h);
    float q = r / max(h, 1e-4);
    if (q <= 1.0)
    {
        return k * (3.0 / max(h * h, 1e-4)) * (1.0 - q);
    }
    if (q <= 2.0)
    {
        float t = 2.0 - q;
        return k * (-0.75 / max(h * h, 1e-4)) * t;
    }
    return 0.0;
}

#endif // HARMONIC_SPH_COMMON_INCLUDED
