#ifndef HARMONIC_V4_COMMON_INCLUDED
#define HARMONIC_V4_COMMON_INCLUDED

// =============================================================================
//  V4Common.hlsl - shared GPU data layout + math for HarmonicEngineV4.
//
//  Particle SOA layout:
//    _Block0        float4(position.xyz, radius)
//    _Block1        float4(velocity.xyz, unused)
//    _PackedColors  uint RGBA8 per particle
//    _Flags         uint per particle (see V4ParticleFlags below)
//
//  CPU mirrors: HarmonicEngineV4.Core.V4ParticleFlags, V4SpatialHashMath.
// =============================================================================

struct HashCellGridRange
{
    int StartIndex;
    int EndIndex;
};

// -----------------------------------------------------------------------------
//  Particle flag layout (must match V4ParticleFlags in C#).
//    bit 0      : Inside bucket volume this frame (reversible)
//    bit 1      : hasEscaped latch (permanent, only Classification may set it)
//    bits 2-4   : zone assignment this frame (0=None 1=HoleEject 2=HeightLayer …)
//    bits 5-9   : owning hole index (valid when zone is a hole zone)
//    bits 10-17 : liquid profile index
// -----------------------------------------------------------------------------
#define V4_FLAG_INSIDE        (1u << 0)
#define V4_FLAG_ESCAPED       (1u << 1)
#define V4_ZONE_SHIFT         2u
#define V4_ZONE_MASK          (7u << 2)
#define V4_HOLE_SHIFT         5u
#define V4_HOLE_MASK          (31u << 5)
#define V4_PROFILE_SHIFT      10u
#define V4_PROFILE_MASK       (255u << 10)

#define V4_ZONE_NONE          0u
#define V4_ZONE_HOLE0         1u
#define V4_ZONE_HEIGHT_LAYER  2u
#define V4_ZONE_HOLE1         3u
#define V4_ZONE_HOLE2         4u
#define V4_ZONE_TOPBAND       5u

uint V4GetZone(uint flags) { return (flags & V4_ZONE_MASK) >> V4_ZONE_SHIFT; }
uint V4GetHole(uint flags) { return (flags & V4_HOLE_MASK) >> V4_HOLE_SHIFT; }
uint V4GetProfile(uint flags) { return (flags & V4_PROFILE_MASK) >> V4_PROFILE_SHIFT; }
bool V4IsInside(uint flags) { return (flags & V4_FLAG_INSIDE) != 0u; }
bool V4HasEscaped(uint flags) { return (flags & V4_FLAG_ESCAPED) != 0u; }

uint V4SetZone(uint flags, uint zone)
{
    return (flags & ~V4_ZONE_MASK) | ((zone << V4_ZONE_SHIFT) & V4_ZONE_MASK);
}

uint V4SetHole(uint flags, uint hole)
{
    return (flags & ~V4_HOLE_MASK) | ((hole << V4_HOLE_SHIFT) & V4_HOLE_MASK);
}

uint V4SetProfile(uint flags, uint profile)
{
    return (flags & ~V4_PROFILE_MASK) | ((profile << V4_PROFILE_SHIFT) & V4_PROFILE_MASK);
}

// -----------------------------------------------------------------------------
//  Spatial hash helpers - identical math to the CPU mirror V4SpatialHashMath so
//  the grid-build pass and neighbor-query pass provably agree.
// -----------------------------------------------------------------------------
static const int3 kV4NeighborOffsets[27] =
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

int3 V4CellFromPosition(float3 position, float cellSize)
{
    return (int3)floor(position / max(cellSize, 1e-4));
}

uint V4HashCell(int3 cell, uint gridResolution)
{
    uint x = (uint)(cell.x * 73856093);
    uint y = (uint)(cell.y * 19349663);
    uint z = (uint)(cell.z * 83492791);
    return (x ^ y ^ z) & (gridResolution - 1u);
}

uint V4HashPosition(float3 position, float cellSize, uint gridResolution)
{
    return V4HashCell(V4CellFromPosition(position, cellSize), gridResolution);
}

// -----------------------------------------------------------------------------
//  RGBA8 color packing.
// -----------------------------------------------------------------------------
float3 V4UnpackColor(uint packed)
{
    float r = (float)(packed & 0xFFu);
    float g = (float)((packed >> 8) & 0xFFu);
    float b = (float)((packed >> 16) & 0xFFu);
    return float3(r, g, b) / 255.0;
}

uint V4PackColor(float3 color)
{
    float3 c = saturate(color) * 255.0 + 0.5;
    uint r = (uint)c.r;
    uint g = (uint)c.g;
    uint b = (uint)c.b;
    return (r & 0xFFu) | ((g & 0xFFu) << 8) | ((b & 0xFFu) << 16) | (0xFFu << 24);
}

// -----------------------------------------------------------------------------
//  SPH smoothing kernels. Poly6 for density/color weights, Spiky gradient for
//  PBF position constraints (Macklin & Mueller 2013). Support radius = h.
// -----------------------------------------------------------------------------
float V4Poly6(float r, float h)
{
    if (r > h)
    {
        return 0.0;
    }

    float h2 = h * h;
    float coeff = 315.0 / (64.0 * 3.14159265 * h2 * h2 * h2 * h2 * h);
    float d = h2 - r * r;
    return coeff * d * d * d;
}

float3 V4SpikyGradient(float3 diff, float r, float h)
{
    if (r > h || r < 1e-6)
    {
        return float3(0, 0, 0);
    }

    float h6 = h * h * h * h * h * h;
    float coeff = -45.0 / (3.14159265 * h6);
    float hmr = h - r;
    return coeff * hmr * hmr * (diff / r);
}

#endif // HARMONIC_V4_COMMON_INCLUDED
