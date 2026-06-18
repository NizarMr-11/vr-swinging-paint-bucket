using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    /// <summary>
    /// Validates which GPU buffers the debug renderer should draw for each pipeline mode.
    /// </summary>
    [Category("GPU")]
    public class HarmonicParticleDebugRendererPlayModeTests
    {
        [UnityTest]
        public IEnumerator ContainerFluidMode_ExposesInternalBufferWithActiveCount()
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

            var region = new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(0f, 0.5f, 0f),
                sphereRadius = 0.2f,
                particleCount = 16,
                seed = 7u
            };

            pipeline.SetSimulationActive(true);
            HarmonicParticleSpawner.Spawn(pipeline, region);
            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(pipeline.ContainerFluidEnabled);
            Assert.IsFalse(pipeline.WorldFallingOnly);
            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer internalBuffer, out uint internalCount));
            Assert.NotNull(internalBuffer);
            Assert.Greater(internalCount, 0u);

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator WorldFallingOnlyMode_ExposesFallingBuffer()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 256, autoRun: false);
            pipeline.SetWorldFallingOnly(true);
            pipeline.SetSimulationActive(true);

            var particles = new FluidParticle[8];
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i] = FluidParticleFactory.FromWorldPosition(
                    new Vector3(i * 0.05f, 2f, 0f),
                    Vector3.down,
                    1000f);
            }

            pipeline.AppendParticles(particles, particles.Length);
            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(pipeline.WorldFallingOnly);
            Assert.IsTrue(
                pipeline.TryGetFallingParticleBuffer(out ComputeBuffer fallingBuffer, out uint fallingCount));
            Assert.NotNull(fallingBuffer);
            Assert.Greater(fallingCount, 0u);

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
