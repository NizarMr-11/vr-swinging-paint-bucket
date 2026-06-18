using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Tests.PlayMode;
using NUnit.Framework;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    /// <summary>
    /// Legacy stress entry point — delegates to the scale-test pattern (fill buffer + frame budget).
    /// </summary>
    [Category("Stress")]
    public class HarmonicPipelineStressPlayModeTests
    {
        [UnityTest]
        public IEnumerator Pipeline_100kCapacity_10kParticles_CompletesFramesUnderBudget()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const int capacity = 100_000;
            const int spawnCount = 10_000;
            const int frames = 30;
            const int maxTotalMs = 30_000;

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: capacity, autoRun: false);
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
                particleCount = spawnCount,
                seed = 99u
            };

            pipeline.SetSimulationActive(true);
            int appended = HarmonicParticleSpawner.Spawn(pipeline, region);
            Assert.Greater(appended, spawnCount / 2, "Expected most particles to append");

            var stopwatch = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            stopwatch.Stop();
            Assert.Less(stopwatch.ElapsedMilliseconds, maxTotalMs, "Stress frames exceeded manual budget.");

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
