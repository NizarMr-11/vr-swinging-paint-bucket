using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class RadixSortIntegrationTests
    {
        [UnityTest]
        public IEnumerator RadixSort_CellRangesCorrect_InPipeline()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 4096, autoRun: false);
            pipeline.SetUseRadixSort(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = 0f,
                rimY = 2f
            });

            float spacing = pipeline.CellSize * 0.45f;
            var settings = new HarmonicLatticeSpawnSettings
            {
                center = Vector3.one * (pipeline.CellSize * 0.5f),
                spacing = spacing,
                gridDimensions = new int3(3, 3, 3),
                restDensity = pipeline.RestDensity
            };

            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.Greater(spawned, 0);

            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(ValidateCellRanges(pipeline));
        }

        [UnityTest]
        public IEnumerator RadixSort_PhysicsUnchanged()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            (float avgCBitonic, float avgSpeedBitonic) = RunPhysicsSample(useRadix: false);
            yield return null;
            (float avgCRadix, float avgSpeedRadix) = RunPhysicsSample(useRadix: true);

            Assert.AreEqual(avgCBitonic, avgCRadix, Mathf.Max(0.05f, Mathf.Abs(avgCBitonic) * 0.05f));
            Assert.AreEqual(avgSpeedBitonic, avgSpeedRadix, Mathf.Max(0.05f, Mathf.Abs(avgSpeedBitonic) * 0.10f));
        }

        [UnityTest]
        public IEnumerator RadixSort_FasterThanBitonic_At32k()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const int targetCount = 32768;
            float bitonicMs = MeasureSortTime(useRadix: false, targetCount, frames: 10);
            yield return null;
            float radixMs = MeasureSortTime(useRadix: true, targetCount, frames: 10);

            if (bitonicMs < 0.5f)
            {
                Assert.Ignore($"Bitonic sort too fast to compare ({bitonicMs:F3}ms); GPU may be high-end.");
            }

            Assert.Less(radixMs, bitonicMs / 3f,
                $"Radix {radixMs:F3}ms not 3x faster than bitonic {bitonicMs:F3}ms");
        }

        private static bool ValidateCellRanges(PipelineExecutionController pipeline)
        {
            if (!pipeline.TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize))
            {
                return false;
            }

            var keys = new GridKeyPair[sortSize];
            var ranges = new HashCellGridRange[sortSize];
            gridKeys.GetData(keys);
            cellRanges.GetData(ranges);

            int lastStart = -1;
            for (int h = 0; h < sortSize; h++)
            {
                HashCellGridRange range = ranges[h];
                if (range.StartIndex < 0)
                {
                    continue;
                }

                Assert.GreaterOrEqual(range.EndIndex, range.StartIndex);
                if (lastStart >= 0)
                {
                    Assert.GreaterOrEqual(range.StartIndex, lastStart);
                }

                lastStart = range.StartIndex;
                uint cellHash = (uint)h;
                for (int i = range.StartIndex; i <= range.EndIndex; i++)
                {
                    if (keys[i].CellHash == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    Assert.AreEqual(cellHash, keys[i].CellHash, $"Cell {h} range contains foreign hash at {i}");
                }
            }

            for (int i = 1; i < sortSize; i++)
            {
                if (keys[i].CellHash == 0xFFFFFFFFu)
                {
                    continue;
                }

                Assert.LessOrEqual(keys[i - 1].CellHash, keys[i].CellHash);
            }

            return true;
        }

        private static (float avgC, float avgSpeed) RunPhysicsSample(bool useRadix)
        {
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 8192, autoRun: false);
            pipeline.SetUseRadixSort(useRadix);
            pipeline.SetUsePbf(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 0.8f,
                floorY = 0f,
                rimY = 1.2f
            });

            var settings = new HarmonicLatticeSpawnSettings
            {
                center = new Vector3(0f, 0.5f, 0f),
                spacing = pipeline.LatticeSpacing,
                gridDimensions = new int3(12, 8, 12),
                restDensity = pipeline.RestDensity
            };
            HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);

            const int frames = 50;
            const float dt = 0.008f;
            for (int i = 0; i < frames; i++)
            {
                pipeline.ExecutePipelineFrame(dt);
            }

            pipeline.TryGetDensityCacheBuffers(out ComputeBuffer densities, out _, out uint count);
            pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount);
            int sampleCount = (int)Mathf.Min(count, activeCount);
            if (sampleCount <= 0 || densities == null || soa == null)
            {
                return (0f, 0f);
            }

            var densityData = new float[sampleCount];
            densities.GetData(densityData, 0, 0, sampleCount);
            float rho0 = pipeline.RestDensity;
            float sumC = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                sumC += densityData[i] / rho0 - 1f;
            }

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, sampleCount);
            float sumSpeed = particles.Sum(p => math.length(p.Velocity));
            return (sumC / sampleCount, sumSpeed / sampleCount);
        }

        private static float MeasureSortTime(bool useRadix, int targetCount, int frames)
        {
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(targetCount, 4096));
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: capacity, autoRun: false);
            pipeline.SetUseRadixSort(useRadix);
            pipeline.SetDynamicSortSizing(false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 2f,
                floorY = 0f,
                rimY = 3f
            });

            int side = Mathf.CeilToInt(Mathf.Pow(targetCount, 1f / 3f));
            var settings = new HarmonicLatticeSpawnSettings
            {
                center = new Vector3(0f, 1f, 0f),
                spacing = pipeline.LatticeSpacing,
                gridDimensions = new int3(side, side, side),
                restDensity = pipeline.RestDensity
            };
            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.GreaterOrEqual(spawned, targetCount / 2);

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                pipeline.RebuildSpatialHashForVerification();
            }

            stopwatch.Stop();
            return (float)stopwatch.Elapsed.TotalMilliseconds / frames;
        }
    }
}
