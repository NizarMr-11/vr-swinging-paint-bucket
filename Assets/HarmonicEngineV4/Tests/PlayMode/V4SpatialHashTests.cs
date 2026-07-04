using System.Collections.Generic;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Quarantine gate for the ported spatial hash (plan Phase 1): the neighbor set
    /// produced by walking the GPU-built grid must equal a CPU brute-force O(n^2)
    /// neighbor search, for both uniform and clustered particle distributions
    /// (clustered is where hash bugs hide).
    /// </summary>
    public sealed class V4SpatialHashTests
    {
        private struct GridReadback
        {
            public uint[] KeyValuePairs; // interleaved (hash, particleIndex)
            public int[] CellRanges;     // interleaved (start, end)
            public int GridResolution;
            public int PaddedGridSize;
        }

        private static GridReadback BuildGrid(Vector3[] positions, float cellSize)
        {
            int n = positions.Length;
            ComputeShader hashShader = V4ShaderLibrary.Load(V4ShaderLibrary.SpatialHash);
            ComputeShader radixShader = V4ShaderLibrary.Load(V4ShaderLibrary.RadixSort);

            var grid = new V4SpatialHashGrid(hashShader, radixShader, n);
            var block0 = new ComputeBuffer(n, sizeof(float) * 4);
            try
            {
                var data = new Vector4[n];
                for (int i = 0; i < n; i++)
                {
                    data[i] = new Vector4(positions[i].x, positions[i].y, positions[i].z, 0.05f);
                }

                block0.SetData(data);
                grid.Build(block0, n, cellSize);

                var pairs = new uint[grid.PaddedGridSize * 2];
                grid.GridKeyValueBuffer.GetData(pairs);
                var ranges = new int[grid.PaddedGridSize * 2];
                grid.CellStartEndBuffer.GetData(ranges);

                return new GridReadback
                {
                    KeyValuePairs = pairs,
                    CellRanges = ranges,
                    GridResolution = grid.GridResolution,
                    PaddedGridSize = grid.PaddedGridSize
                };
            }
            finally
            {
                grid.Dispose();
                block0.Release();
            }
        }

        /// <summary>Walks the 27-cell stencil over the GPU-built grid exactly like V4NeighborQuery.hlsl.</summary>
        private static HashSet<int> GridNeighbors(GridReadback grid, Vector3[] positions, int queryIndex, float radius, float cellSize)
        {
            var result = new HashSet<int>();
            Vector3Int baseCell = V4SpatialHashMath.CellFromPosition(positions[queryIndex], cellSize);

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                uint hash = V4SpatialHashMath.HashCell(baseCell + new Vector3Int(dx, dy, dz), (uint)grid.GridResolution);
                int start = grid.CellRanges[hash * 2];
                int end = grid.CellRanges[hash * 2 + 1];
                if (start < 0)
                {
                    continue;
                }

                for (int s = start; s <= end; s++)
                {
                    uint particleIndex = grid.KeyValuePairs[s * 2 + 1];
                    if (particleIndex == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int pi = (int)particleIndex;
                    if (pi == queryIndex)
                    {
                        continue;
                    }

                    if (Vector3.Distance(positions[queryIndex], positions[pi]) <= radius)
                    {
                        result.Add(pi);
                    }
                }
            }

            return result;
        }

        private static HashSet<int> BruteForceNeighbors(Vector3[] positions, int queryIndex, float radius)
        {
            var result = new HashSet<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (i != queryIndex && Vector3.Distance(positions[queryIndex], positions[i]) <= radius)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static void AssertNeighborParity(Vector3[] positions, float cellSize, float radius)
        {
            GridReadback grid = BuildGrid(positions, cellSize);

            // Every particle must appear exactly once in the sorted pair list.
            var seen = new HashSet<uint>();
            int liveCount = 0;
            for (int i = 0; i < grid.PaddedGridSize; i++)
            {
                uint value = grid.KeyValuePairs[i * 2 + 1];
                if (value == 0xFFFFFFFFu)
                {
                    continue;
                }

                Assert.IsTrue(seen.Add(value), $"particle {value} appears more than once in the grid");
                liveCount++;
            }

            Assert.AreEqual(positions.Length, liveCount, "grid lost or duplicated particles");

            for (int i = 0; i < positions.Length; i++)
            {
                HashSet<int> viaGrid = GridNeighbors(grid, positions, i, radius, cellSize);
                HashSet<int> viaBrute = BruteForceNeighbors(positions, i, radius);
                Assert.IsTrue(viaGrid.SetEquals(viaBrute),
                    $"neighbor mismatch for particle {i}: grid={viaGrid.Count} brute={viaBrute.Count}");
            }
        }

        [Test]
        public void UniformCloud_NeighborSetsMatchBruteForce()
        {
            const int count = 600;
            const float cellSize = 0.1f;
            var rng = new System.Random(42);
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);
            }

            AssertNeighborParity(positions, cellSize, cellSize);
        }

        [Test]
        public void ClusteredCloud_NeighborSetsMatchBruteForce()
        {
            // Dense clumps + a few outliers: worst case for hash-collision bugs.
            const float cellSize = 0.1f;
            var rng = new System.Random(77);
            var positions = new List<Vector3>();
            Vector3[] clusterCenters =
            {
                new Vector3(0.03f, 0.03f, 0.03f),
                new Vector3(-0.9f, 0.5f, 0.2f),
                new Vector3(0.5f, -0.5f, 0.8f)
            };

            foreach (Vector3 center in clusterCenters)
            {
                for (int i = 0; i < 150; i++)
                {
                    positions.Add(center + new Vector3(
                        (float)rng.NextDouble() * 0.08f - 0.04f,
                        (float)rng.NextDouble() * 0.08f - 0.04f,
                        (float)rng.NextDouble() * 0.08f - 0.04f));
                }
            }

            for (int i = 0; i < 30; i++)
            {
                positions.Add(new Vector3(
                    (float)rng.NextDouble() * 4f - 2f,
                    (float)rng.NextDouble() * 4f - 2f,
                    (float)rng.NextDouble() * 4f - 2f));
            }

            AssertNeighborParity(positions.ToArray(), cellSize, cellSize);
        }

        [Test]
        public void NegativeCoordinates_NeighborSetsMatchBruteForce()
        {
            // Cells at negative coordinates exercise the floor()/int-cast hash path.
            const float cellSize = 0.25f;
            var rng = new System.Random(5);
            var positions = new Vector3[300];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Vector3(
                    (float)rng.NextDouble() * 3f - 3f,
                    (float)rng.NextDouble() * 3f - 3f,
                    (float)rng.NextDouble() * 3f - 3f);
            }

            AssertNeighborParity(positions, cellSize, cellSize);
        }

        [Test]
        public void CpuHash_MatchesGpuHash_ForSampledCells()
        {
            // Sample GPU-produced keys and confirm the CPU mirror computes identical hashes.
            const int count = 256;
            const float cellSize = 0.2f;
            var rng = new System.Random(11);
            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f);
            }

            GridReadback grid = BuildGrid(positions, cellSize);
            for (int i = 0; i < grid.PaddedGridSize; i++)
            {
                uint value = grid.KeyValuePairs[i * 2 + 1];
                if (value == 0xFFFFFFFFu)
                {
                    continue;
                }

                uint gpuHash = grid.KeyValuePairs[i * 2];
                uint cpuHash = V4SpatialHashMath.HashPosition(positions[value], cellSize, (uint)grid.GridResolution);
                Assert.AreEqual(cpuHash, gpuHash, $"hash mismatch for particle {value}");
            }
        }
    }
}
