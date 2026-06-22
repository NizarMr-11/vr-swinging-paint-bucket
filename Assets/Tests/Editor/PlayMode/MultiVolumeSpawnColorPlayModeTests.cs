using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class MultiVolumeSpawnColorPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpawnTwoColoredRegions_AssignsDistinctPackedColors()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 256, autoRun: false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = 0f,
                rimY = 2f
            });
            pipeline.SetSimulationActive(true);

            uint red = FluidParticleFactory.PackColor(Color.red);
            uint blue = FluidParticleFactory.PackColor(Color.blue);

            HarmonicParticleSpawner.Spawn(pipeline, new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(-0.15f, 0.5f, 0f),
                sphereRadius = 0.12f,
                particleCount = 12,
                spawnColor = Color.red,
                seed = 11u
            });

            HarmonicParticleSpawner.Spawn(pipeline, new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(0.15f, 0.5f, 0f),
                sphereRadius = 0.12f,
                particleCount = 12,
                spawnColor = Color.blue,
                seed = 22u
            });

            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count));
            Assert.Greater(count, 0u);

            var particles = new FluidParticle[count];
            buffer.GetData(particles, 0, 0, (int)count);

            int redCount = 0;
            int blueCount = 0;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].PackedColorRGBA == red)
                {
                    redCount++;
                }
                else if (particles[i].PackedColorRGBA == blue)
                {
                    blueCount++;
                }
            }

            Assert.Greater(redCount, 0, "Expected red particles from first spawn region");
            Assert.Greater(blueCount, 0, "Expected blue particles from second spawn region");

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator SpawnAll_TwoVolumesWithPriority_AssignsDistinctColors()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 256, autoRun: false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = 0f,
                rimY = 2f
            });

            uint red = FluidParticleFactory.PackColor(Color.red);
            uint blue = FluidParticleFactory.PackColor(Color.blue);

            var highGo = new GameObject("HighPriorityVolume");
            highGo.transform.position = new Vector3(-0.15f, 0.5f, 0f);
            var high = highGo.AddComponent<ParticleSpawnVolume>();
            high.SetPipeline(pipeline);
            high.Configure(ShapeVolumeType.Sphere, 12, 11u, emitOnStartValue: false);
            high.SetSphere(0.12f);
            high.SetSpawnColor(Color.red);
            high.SetSpawnPriority(10);

            var lowGo = new GameObject("LowPriorityVolume");
            lowGo.transform.position = new Vector3(0.15f, 0.5f, 0f);
            var low = lowGo.AddComponent<ParticleSpawnVolume>();
            low.SetPipeline(pipeline);
            low.Configure(ShapeVolumeType.Sphere, 12, 22u, emitOnStartValue: false);
            low.SetSphere(0.12f);
            low.SetSpawnColor(Color.blue);
            low.SetSpawnPriority(1);

            yield return null;

            int appended = HarmonicParticleSpawnCoordinator.SpawnAll(
                pipeline,
                clearFirst: true,
                activateSimulation: true,
                volumes: new[] { low, high });

            Assert.Greater(appended, 0);

            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(pipeline.TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count));
            Assert.Greater(count, 0u);

            var particles = new FluidParticle[count];
            buffer.GetData(particles, 0, 0, (int)count);

            int redCount = 0;
            int blueCount = 0;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i].PackedColorRGBA == red)
                {
                    redCount++;
                }
                else if (particles[i].PackedColorRGBA == blue)
                {
                    blueCount++;
                }
            }

            Assert.Greater(redCount, 0, "Expected red particles from high-priority volume");
            Assert.Greater(blueCount, 0, "Expected blue particles from low-priority volume");

            Object.DestroyImmediate(highGo);
            Object.DestroyImmediate(lowGo);
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
