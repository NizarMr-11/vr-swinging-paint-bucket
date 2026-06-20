using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class GpuPhase2PlayModeTests
    {
        [UnityTest]
        public IEnumerator SpatialHash_NeighborsInSameCell_ShareHashKey()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 512, autoRun: false);
            yield return null;
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = -1f,
                rimY = 2f
            });

            float spacing = pipeline.CellSize * 0.4f;
            var settings = new HarmonicLatticeSpawnSettings
            {
                // Offset away from world origin so all lattice sites stay in one hash cell.
                center = Vector3.one * (pipeline.CellSize * 0.5f),
                spacing = spacing,
                gridDimensions = new int3(2, 2, 2),
                restDensity = 1000f
            };

            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.AreEqual(8, spawned);

            yield return null;
            pipeline.RebuildSpatialHashForVerification();

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer particleBuffer, out uint activeCount));
            Assert.AreEqual(8u, activeCount);

            Assert.IsTrue(
                pipeline.TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize));
            Assert.Greater(sortSize, 0);

            var particles = GpuParticleReadbackUtility.ReadParticles(particleBuffer, (int)activeCount);
            uint referenceHash = SphHashCpuMirror.HashPosition(particles[0].Position, pipeline.CellSize, sortSize);

            for (int i = 1; i < particles.Length; i++)
            {
                uint hash = SphHashCpuMirror.HashPosition(particles[i].Position, pipeline.CellSize, sortSize);
                Assert.AreEqual(
                    referenceHash,
                    hash,
                    $"Lattice neighbor {i} should share hash with particle 0 (same cell)");
            }

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator SpatialHash_LatticeCellRange_IsContiguousAndSorted()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 512, autoRun: false);
            yield return null;
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = -1f,
                rimY = 2f
            });

            var settings = new HarmonicLatticeSpawnSettings
            {
                center = Vector3.one * (pipeline.CellSize * 0.5f),
                spacing = pipeline.CellSize * 0.4f,
                gridDimensions = new int3(2, 2, 2),
                restDensity = 1000f
            };

            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.AreEqual(8, spawned);
            Assert.AreEqual(8u, pipeline.GetActiveParticleCount());
            yield return null;
            pipeline.RebuildSpatialHashForVerification();

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer particleBuffer, out uint activeCount));
            Assert.IsTrue(
                pipeline.TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize));

            var keys = new GridKeyPair[sortSize];
            gridKeys.GetData(keys);

            for (int i = 1; i < sortSize; i++)
            {
                if (keys[i].CellHash == 0xFFFFFFFFu)
                {
                    continue;
                }

                Assert.LessOrEqual(keys[i - 1].CellHash, keys[i].CellHash, $"Keys not sorted at {i}");
            }

            var particles = GpuParticleReadbackUtility.ReadParticles(particleBuffer, (int)activeCount);
            uint bucketHash = SphHashCpuMirror.HashPosition(particles[0].Position, pipeline.CellSize, sortSize);

            var ranges = new HashCellGridRange[sortSize];
            cellRanges.GetData(ranges);

            int runStart = -1;
            int runEnd = -1;
            int matchCount = 0;
            var indicesInRange = new HashSet<uint>();
            for (int i = 0; i < sortSize; i++)
            {
                if (keys[i].CellHash == 0xFFFFFFFFu
                    || keys[i].CellHash != bucketHash
                    || keys[i].ParticleIndex >= activeCount)
                {
                    continue;
                }

                if (runStart < 0)
                {
                    runStart = i;
                }

                runEnd = i;
                matchCount++;
                indicesInRange.Add(keys[i].ParticleIndex);

                uint cpuHash = SphHashCpuMirror.HashPosition(
                    particles[keys[i].ParticleIndex].Position,
                    pipeline.CellSize,
                    sortSize);
                Assert.AreEqual(bucketHash, cpuHash, $"GPU key hash mismatch at sorted slot {i}");
            }

            Assert.AreEqual((int)activeCount, matchCount, "All lattice particles should share one hash bucket");
            Assert.AreEqual(runEnd - runStart + 1, matchCount, "Hash bucket run should be contiguous in sorted keys");
            Assert.AreEqual((int)activeCount, indicesInRange.Count, "Sorted run should reference each particle once");

            HashCellGridRange range = ranges[(int)bucketHash];
            Assert.AreEqual(runStart, range.StartIndex, "Cell range table start should match sorted run");
            Assert.AreEqual(runEnd, range.EndIndex, "Cell range table end should match sorted run");

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
