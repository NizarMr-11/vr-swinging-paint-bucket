#ifndef HARMONIC_V4_NEIGHBOR_QUERY_INCLUDED
#define HARMONIC_V4_NEIGHBOR_QUERY_INCLUDED

// Neighbor iteration over the sorted uniform grid built by V4SpatialHash.compute
// + V4RadixSort.compute. Requires the including shader to declare:
//   StructuredBuffer<HashCellGridRange> _CellStartEndBuffer;  (or RW variant)
//   StructuredBuffer<uint2> _GridKeyValueBuffer;              (sorted pairs)
//   uint _GridResolution;
//   float _CellSize;
//
// Usage:
//   V4_FOREACH_NEIGHBOR_BEGIN(queryPosition, neighborIndex)
//       ... use neighborIndex ...
//   V4_FOREACH_NEIGHBOR_END
//
// Cells are hashed, so distinct cells may collide into one bucket; callers must
// always distance-test candidates (which SPH kernels do anyway).

#include "V4Common.hlsl"

#define V4_FOREACH_NEIGHBOR_BEGIN(queryPos, neighborIndexVar)                                  \
{                                                                                              \
    int3 v4_baseCell = V4CellFromPosition(queryPos, _CellSize);                                \
    [loop]                                                                                     \
    for (uint v4_o = 0u; v4_o < 27u; v4_o++)                                                   \
    {                                                                                          \
        uint v4_cellHash = V4HashCell(v4_baseCell + kV4NeighborOffsets[v4_o], _GridResolution);\
        int v4_start = _CellStartEndBuffer[v4_cellHash].StartIndex;                            \
        int v4_end = _CellStartEndBuffer[v4_cellHash].EndIndex;                                \
        if (v4_start < 0)                                                                      \
        {                                                                                      \
            continue;                                                                          \
        }                                                                                      \
        [loop]                                                                                 \
        for (int v4_s = v4_start; v4_s <= v4_end; v4_s++)                                      \
        {                                                                                      \
            uint neighborIndexVar = _GridKeyValueBuffer[v4_s].y;                               \
            if (neighborIndexVar == 0xFFFFFFFFu)                                               \
            {                                                                                  \
                continue;                                                                      \
            }

#define V4_FOREACH_NEIGHBOR_END                                                                \
        }                                                                                      \
    }                                                                                          \
}

#endif // HARMONIC_V4_NEIGHBOR_QUERY_INCLUDED
