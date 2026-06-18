#ifndef HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED
#define HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED

// =============================================================================
//  SphNeighborQuery.hlsl - the canonical "get the nearest particles" routine.
//
//  This is the ONE place SPH neighbor iteration lives. Any kernel that needs a
//  particle's neighbors (density, pressure, viscosity, color diffusion, ...)
//  should call ForEachNeighbor instead of re-implementing the loop.
//
//  HOW NEIGHBOR QUERIES WORK (sorted spatial hash):
//    1. Particles are hashed into a power-of-two bucket table by cell coord.
//    2. The (hash, index) key buffer is bitonic-sorted so same-cell particles
//       are contiguous (see SpatialHashGridIndirect.compute).
//    3. _CellStartEndBuffer maps each hash to its [start,end] run in that sorted
//       buffer.
//    4. To find neighbors of a particle we visit the 3x3x3 = 27 cells around its
//       own cell (kNeighborOffsets), read each cell's run, and iterate the
//       particles in it, rejecting anything beyond the 2*h support radius.
//
//  REQUIRED before including this file the includer MUST declare:
//      StructuredBuffer<FluidParticle>     _ReadOnlyParticleSource;
//      StructuredBuffer<GridKeyPair>       _SortedGridKeyValueBuffer;
//      StructuredBuffer<HashCellGridRange> _CellStartEndBuffer;
//      RWStructuredBuffer<FluidParticle>   _DensityWritableCache;
//      uint  _ActiveParticleCount;
//      uint  _GridResolution;
//      float _CellSize;
//      float _SmoothingRadius;
//      float _ParticleMass;
//  and must have included SphCommon.hlsl first (structs + helpers + kernels).
// =============================================================================

// Accumulates SPH fields over the 27-cell neighborhood of `self`.
//   useDensityCache = false : density-only pass (reads _ReadOnlyParticleSource).
//   useDensityCache = true  : force pass (reads _DensityWritableCache for the
//                             neighbor's resolved density/pressure).
// colorLaplacian accumulates sum_j (m_j/rho_j)(c_j - c_i) * Laplacian(r,h), the
// SPH color-diffusion term (only meaningful in the force pass); callers scale it
// by the diffusion coefficient and dt to mix colors between touching fluids.
void ForEachNeighbor(
    uint particleIndex,
    FluidParticle self,
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

    int3 baseCell = SphCellFromPosition(self.Position, _CellSize);
    float h = _SmoothingRadius;
    float selfDensity = max(self.Density, 1e-4);
    float selfPressure = self.Pressure;
    float3 selfColor = UnpackUintToFloat3(self.PackedColorRGBA);

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

            FluidParticle neighbor;
            if (useDensityCache)
            {
                neighbor = _DensityWritableCache[neighborIndex];
            }
            else
            {
                neighbor = _ReadOnlyParticleSource[neighborIndex];
            }

            float3 diff = self.Position - neighbor.Position;
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

            float neighborDensity = max(neighbor.Density, 1e-4);
            float3 gradW = CubicSplineGradient(diff, r, h);
            float pressureTerm = (selfPressure / (selfDensity * selfDensity) + neighbor.Pressure / (neighborDensity * neighborDensity));
            pressureGrad += -_ParticleMass * pressureTerm * gradW;

            float lap = CubicSplineLaplacian(r, h);
            float3 relVel = neighbor.Velocity - self.Velocity;
            viscosityForce += _ParticleMass * relVel / neighborDensity * lap;

            float3 neighborColor = UnpackUintToFloat3(neighbor.PackedColorRGBA);
            colorLaplacian += (_ParticleMass / neighborDensity) * (neighborColor - selfColor) * lap;
        }
    }
}

#endif // HARMONIC_SPH_NEIGHBOR_QUERY_INCLUDED
