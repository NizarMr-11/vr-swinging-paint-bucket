using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class GpuPhase1PlayModeTests
    {
        [UnityTest]
        public IEnumerator LatticeSpawn_1000Particles_Frame0_ValidState()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 2048, autoRun: false);
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
                center = new Vector3(0f, 0.5f, 0f),
                spacing = pipeline.CellSize,
                gridDimensions = new Unity.Mathematics.int3(10, 10, 10),
                restDensity = 1000f,
                spawnColor = Color.cyan
            };

            uint expectedColor = FluidParticleFactory.PackColor(settings.spawnColor);
            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.AreEqual(1000, spawned);

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count));
            Assert.AreEqual(1000u, count);

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(buffer, (int)count);
            Assert.AreEqual(1000, particles.Length);

            for (int i = 0; i < particles.Length; i++)
            {
                Assert.IsTrue(GpuParticleReadbackUtility.IsFinite(particles[i]), $"Particle {i} not finite");
                Assert.IsTrue(
                    GpuParticleReadbackUtility.MatchesSpawnState(particles[i], expectedColor, settings.restDensity),
                    $"Particle {i} failed spawn-state check");
            }

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [Test]
        public void BufferStride_MatchesFluidParticleSize()
        {
            Assert.AreEqual(48, Marshal.SizeOf<FluidParticle>());
            Assert.AreEqual(48, sizeof(float) * 12);
        }
    }
}
