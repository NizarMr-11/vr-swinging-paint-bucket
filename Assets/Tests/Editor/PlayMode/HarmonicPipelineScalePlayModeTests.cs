using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    /// <summary>
    /// GPU scale tests: fill the buffer, run SPH frames, assert stability + budget.
    /// See docs/testing-strategy.md for pass criteria.
    /// </summary>
    [Category("Scale")]
    public class HarmonicPipelineScalePlayModeTests
    {
        [UnityTest]
        public IEnumerator ContainerFluid_10kParticles_StaysStableUnderBudget()
        {
            yield return RunScaleTest(particleCount: 10_000, frames: 20, maxAvgMsPerFrame: 500);
        }

        [UnityTest]
        public IEnumerator ContainerFluid_20kParticles_StaysStableUnderBudget()
        {
            yield return RunScaleTest(particleCount: 20_000, frames: 15, maxAvgMsPerFrame: 1000);
        }

        [UnityTest]
        public IEnumerator ContainerFluid_50kParticles_StaysStableUnderBudget()
        {
            yield return RunScaleTest(particleCount: 50_000, frames: 10, maxAvgMsPerFrame: 2500);
        }

        private static IEnumerator RunScaleTest(int particleCount, int frames, int maxAvgMsPerFrame)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: particleCount, autoRun: false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 0.55f,
                floorY = 0f,
                rimY = 1.1f
            });

            var region = new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(0f, 0.4f, 0f),
                sphereRadius = 0.25f,
                particleCount = particleCount,
                restDensity = 1000f,
                seed = 42u
            };

            pipeline.SetSimulationActive(true);
            int spawned = HarmonicParticleSpawner.Spawn(pipeline, region);
            Assert.GreaterOrEqual(spawned, particleCount * 9 / 10, $"Expected at least 90% of {particleCount} spawned, got {spawned}");

            var stopwatch = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            stopwatch.Stop();
            double avgMs = stopwatch.ElapsedMilliseconds / (double)frames;
            Assert.Less(avgMs, maxAvgMsPerFrame, $"Avg frame time {avgMs:F1} ms exceeded budget {maxAvgMsPerFrame} ms for {particleCount} particles");

            uint active = pipeline.GetActiveParticleCount();
            Assert.GreaterOrEqual(active, (uint)(spawned * 9 / 10), "Too many particles lost during simulation");

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
