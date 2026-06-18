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
    }
}
