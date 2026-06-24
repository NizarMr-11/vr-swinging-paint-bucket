#ifndef HARMONIC_SORTED_GRID_ACCESS_INCLUDED
#define HARMONIC_SORTED_GRID_ACCESS_INCLUDED

// Radix sort keeps keys/values in separate buffers; bitonic uses uint2 pairs.
uint _UseSplitSortedGrid;
StructuredBuffer<GridKeyPair> _SortedGridKeyValueBuffer;
StructuredBuffer<uint> _SortedGridKeys;
StructuredBuffer<uint> _SortedGridValues;

GridKeyPair HarmonicLoadSortedGridPair(uint sortedIndex)
{
    GridKeyPair pair;
    if (_UseSplitSortedGrid != 0u)
    {
        pair.CellHash = _SortedGridKeys[sortedIndex];
        pair.ParticleIndex = _SortedGridValues[sortedIndex];
    }
    else
    {
        pair = _SortedGridKeyValueBuffer[sortedIndex];
    }

    return pair;
}

#endif // HARMONIC_SORTED_GRID_ACCESS_INCLUDED
