using HarmonicEngine.Domain.Models;
using Unity.Mathematics;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// CPU mirror of <c>ForEachNeighbor</c> in SphNeighborQuery.hlsl for stencil diagnostics.
    /// </summary>
    public static class SphNeighborQueryCpuMirror
    {
        private static readonly int3[] NeighborOffsets =
        {
            new int3(-1, -1, -1), new int3(0, -1, -1), new int3(1, -1, -1),
            new int3(-1, 0, -1), new int3(0, 0, -1), new int3(1, 0, -1),
            new int3(-1, 1, -1), new int3(0, 1, -1), new int3(1, 1, -1),
            new int3(-1, -1, 0), new int3(0, -1, 0), new int3(1, -1, 0),
            new int3(-1, 0, 0), new int3(0, 0, 0), new int3(1, 0, 0),
            new int3(-1, 1, 0), new int3(0, 1, 0), new int3(1, 1, 0),
            new int3(-1, -1, 1), new int3(0, -1, 1), new int3(1, -1, 1),
            new int3(-1, 0, 1), new int3(0, 0, 1), new int3(1, 0, 1),
            new int3(-1, 1, 1), new int3(0, 1, 1), new int3(1, 1, 1)
        };

        public static int CountStencilNeighbors(
            int particleIndex,
            FluidParticle[] particles,
            uint activeCount,
            GridKeyPair[] sortedKeys,
            HashCellGridRange[] cellRanges,
            float cellSize,
            float smoothingRadius,
            int gridResolution)
        {
            if (particles == null
                || sortedKeys == null
                || cellRanges == null
                || particleIndex < 0
                || particleIndex >= particles.Length
                || particleIndex >= activeCount)
            {
                return 0;
            }

            FluidParticle self = particles[particleIndex];
            int3 baseCell = SphHashCpuMirror.CellFromPosition(self.Position, cellSize);
            float supportRadius = 2f * smoothingRadius;
            int count = 0;

            for (int n = 0; n < NeighborOffsets.Length; n++)
            {
                uint cellHash = SphHashCpuMirror.HashCell(baseCell + NeighborOffsets[n], gridResolution);
                if (cellHash >= cellRanges.Length)
                {
                    continue;
                }

                HashCellGridRange range = cellRanges[(int)cellHash];
                if (range.StartIndex < 0 || range.EndIndex < range.StartIndex)
                {
                    continue;
                }

                for (int sortedIndex = range.StartIndex; sortedIndex <= range.EndIndex; sortedIndex++)
                {
                    if (sortedIndex < 0 || sortedIndex >= sortedKeys.Length)
                    {
                        continue;
                    }

                    GridKeyPair pair = sortedKeys[sortedIndex];
                    if (pair.CellHash == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    uint neighborIndex = pair.ParticleIndex;
                    if (neighborIndex >= activeCount || neighborIndex >= particles.Length)
                    {
                        continue;
                    }

                    float r = math.distance(self.Position, particles[neighborIndex].Position);
                    if (r > supportRadius)
                    {
                        continue;
                    }

                    count++;
                }
            }

            return count;
        }

        public static int CountBruteForceNeighbors(
            int particleIndex,
            FluidParticle[] particles,
            uint activeCount,
            float smoothingRadius)
        {
            if (particles == null || particleIndex < 0 || particleIndex >= particles.Length || particleIndex >= activeCount)
            {
                return 0;
            }

            FluidParticle self = particles[particleIndex];
            float supportRadius = 2f * smoothingRadius;
            int count = 0;
            for (int i = 0; i < activeCount && i < particles.Length; i++)
            {
                if (math.distance(self.Position, particles[i].Position) <= supportRadius)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
