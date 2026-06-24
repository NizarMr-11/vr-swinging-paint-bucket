#ifndef HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED
#define HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED

// =============================================================================
//  SphNeighborQuery.hlsl - the canonical "get the nearest particles" routine.
//
//  REQUIRED before including this file the includer MUST declare:
//      SOA read buffers (_PositionsX/Y/Z, _VelocitiesX/Y/Z, _Densities,
//      _Pressures, _PackedColors) via SphSoaAccess.hlsl
//      StructuredBuffer<GridKeyPair>       _SortedGridKeyValueBuffer;
//      StructuredBuffer<HashCellGridRange> _CellStartEndBuffer;
//      RWStructuredBuffer<float>           _DensityCacheDensities;
//      RWStructuredBuffer<float>           _DensityCachePressures;
//      uint  _ActiveParticleCount;
//      uint  _GridResolution;
//      float _CellSize;
//      float _SmoothingRadius;
//      float _ParticleMass;
//  and must have included SphCommon.hlsl first.
// =============================================================================

StructuredBuffer<GridKeyPair> _SortedGridKeyValueBuffer;

void ForEachNeighbor(
    uint particleIndex,
    bool useDensityCache,
    out float density,
    out float3 pressureGrad,
    out float3 viscosityForce,
    out float3 colorLaplacian)
{
    density = 0.0;
    pressureGrad = float3(0, 0, 0);
    viscosityForce = float3(0, 0, 0);
    colorLaplacian = float3(0, 0, 0);

    float3 selfPos = SphLoadPosition(particleIndex);
    int3 baseCell = SphCellFromPosition(selfPos, _CellSize);
    float h = _SmoothingRadius;

    float selfDensity = 1e-4;
    float selfPressure = 0.0;
    float3 selfVel = float3(0, 0, 0);
    float3 selfColor = float3(0, 0, 0);

    if (useDensityCache)
    {
        selfDensity = max(_DensityCacheDensities[particleIndex], 1e-4);
        selfPressure = _DensityCachePressures[particleIndex];
        selfVel = SphLoadVelocity(particleIndex);
        selfColor = UnpackUintToFloat3(SphLoadPackedColor(particleIndex));
    }

    [loop]
    for (int n = 0; n < 27; n++)
    {
        uint cellHash = SphHashCell(baseCell + kNeighborOffsets[n], _GridResolution);
        HashCellGridRange range = _CellStartEndBuffer[cellHash];
        if (range.StartIndex < 0 || range.EndIndex < range.StartIndex)
        {
            continue;
        }

        for (int sortedIndex = range.StartIndex; sortedIndex <= range.EndIndex; sortedIndex++)
        {
            GridKeyPair pair = _SortedGridKeyValueBuffer[sortedIndex];
            if (pair.CellHash == 0xFFFFFFFFu)
            {
                continue;
            }

            uint neighborIndex = pair.ParticleIndex;
            if (neighborIndex >= _ActiveParticleCount)
            {
                continue;
            }

            float3 neighborPos = SphLoadPosition(neighborIndex);
            float3 diff = selfPos - neighborPos;
            float r = length(diff);
            if (r > 2.0 * h)
            {
                continue;
            }

            float w = CubicSplineKernel(r, h);
            density += _ParticleMass * w;

            if (!useDensityCache)
            {
                continue;
            }

            float neighborDensity = max(_DensityCacheDensities[neighborIndex], 1e-4);
            float3 gradW = CubicSplineGradient(diff, r, h);
            float neighborPressure = _DensityCachePressures[neighborIndex];
            float pressureTerm = (selfPressure / (selfDensity * selfDensity) + neighborPressure / (neighborDensity * neighborDensity));
            pressureGrad += -_ParticleMass * pressureTerm * gradW;

            float lap = CubicSplineLaplacian(r, h);
            float3 neighborVel = SphLoadVelocity(neighborIndex);
            float3 relVel = neighborVel - selfVel;
            viscosityForce += _ParticleMass * relVel / neighborDensity * lap;

            float3 neighborColor = UnpackUintToFloat3(SphLoadPackedColor(neighborIndex));
            colorLaplacian += (_ParticleMass / neighborDensity) * (neighborColor - selfColor) * lap;
        }
    }
}

#endif // HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED
