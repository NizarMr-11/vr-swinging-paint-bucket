#ifndef HARMONIC_PBF_NEIGHBOR_QUERY_INCLUDED
#define HARMONIC_PBF_NEIGHBOR_QUERY_INCLUDED

StructuredBuffer<GridKeyPair> _SortedGridKeyValueBuffer;

// Neighbor iteration over predicted positions using the spatial hash grid.
// Parent shader must include OpenTopCylinderBoundary.hlsl before this file.

float3 PbfLoadPredictedPosition(uint i)
{
    return _PredictedBlock0[i].xyz;
}

float PbfComputeDensityAtPosition(uint particleIndex, float3 selfPos)
{
    if (!OtcParticipatesInPbf(selfPos))
    {
        return 0.0;
    }

    int3 baseCell = SphCellFromPosition(selfPos, _CellSize);
    float h = _SmoothingRadius;
    float density = 0.0;

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
            if (pair.CellHash == 0xFFFFFFFFu || pair.ParticleIndex >= _ActiveParticleCount)
            {
                continue;
            }

            float3 neighborPos = PbfLoadPredictedPosition(pair.ParticleIndex);
            if (!OtcParticipatesInPbf(neighborPos))
            {
                continue;
            }

            float3 diff = selfPos - neighborPos;
            float r = length(diff);
            if (r > 2.0 * h)
            {
                continue;
            }

            density += _ParticleMass * CubicSplineKernel(r, h);
        }
    }

    return density;
}

float PbfComputeDensityAt(uint particleIndex)
{
    return PbfComputeDensityAtPosition(particleIndex, PbfLoadPredictedPosition(particleIndex));
}

float PbfComputeConstraintAt(uint particleIndex)
{
    float density = PbfComputeDensityAt(particleIndex);
    return density / max(_RestDensity, 1e-4) - 1.0;
}

#endif // HARMONIC_PBF_NEIGHBOR_QUERY_INCLUDED
