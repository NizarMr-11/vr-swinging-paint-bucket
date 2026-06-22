using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class GpuPhase3PlayModeTests
    {
        [UnityTest]
        public IEnumerator Sph_CoreParticles_ReachRestDensity()
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
                radius = 2f,
                floorY = -2f,
                rimY = 3f
            });

            float spacing = pipeline.CellSize;
            var spawnCenter = Vector3.one * (pipeline.CellSize * 5f);
            var settings = new HarmonicLatticeSpawnSettings
            {
                center = spawnCenter,
                spacing = spacing,
                gridDimensions = new int3(10, 10, 10),
                restDensity = pipeline.RestDensity
            };

            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.AreEqual(1000, spawned);

            pipeline.ExecuteContainerSphDensityForVerification();
            yield return null;

            Assert.IsTrue(
                pipeline.TryGetDensityCacheBuffer(out ComputeBuffer buffer, out uint count));
            Assert.AreEqual(1000u, count);

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(buffer, (int)count);
            int3 dims = settings.gridDimensions;
            int coreMin = dims.x / 2 - 1;
            int coreMax = dims.x / 2;
            int coreSamples = 0;
            float restDensity = pipeline.RestDensity;
            float densitySum = 0f;

            for (int i = 0; i < particles.Length; i++)
            {
                if (!TryGetLatticeCoords(i, dims, out int x, out int y, out int z))
                {
                    continue;
                }

                if (x < coreMin || x > coreMax || y < coreMin || y > coreMax || z < coreMin || z > coreMax)
                {
                    continue;
                }

                coreSamples++;
                densitySum += particles[i].Density;
            }

            Assert.AreEqual(8, coreSamples, "Expected full inner 2x2x2 core block");
            float averageCoreDensity = densitySum / coreSamples;
            Assert.IsTrue(
                GpuParticleReadbackUtility.IsNearRestDensity(
                    new FluidParticle { Density = averageCoreDensity },
                    restDensity,
                    0.15f),
                $"Average core density {averageCoreDensity} not within 15% of rest density {restDensity}");

            Object.DestroyImmediate(pipeline.gameObject);
        }

        private static bool TryGetLatticeCoords(int index, int3 dims, out int x, out int y, out int z)
        {
            x = y = z = 0;
            int nx = dims.x;
            int ny = dims.y;
            int nz = dims.z;
            if (nx <= 0 || ny <= 0 || nz <= 0 || index < 0 || index >= nx * ny * nz)
            {
                return false;
            }

            z = index / (nx * ny);
            int remainder = index % (nx * ny);
            y = remainder / nx;
            x = remainder % nx;
            return true;
        }

        [UnityTest]
        public IEnumerator Container_FloorBoundary_PreventsFallingThrough()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const float floorY = -1f;
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 512, autoRun: false);
            yield return null;
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetGravity(new Vector3(0f, -80f, 0f));
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 2f,
                floorY = floorY,
                rimY = 3f
            });

            float cellSize = pipeline.CellSize;
            var settings = new HarmonicLatticeSpawnSettings
            {
                center = new Vector3(0f, floorY + cellSize * 3f, 0f),
                spacing = cellSize * 0.5f,
                gridDimensions = new int3(4, 4, 4),
                restDensity = pipeline.RestDensity
            };

            int spawned = HarmonicLatticeSpawner.SpawnLattice(pipeline, settings);
            Assert.Greater(spawned, 0);

            pipeline.SetSimulationActive(true);
            for (int frame = 0; frame < 10; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count));
            Assert.Greater(count, 0u);

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(buffer, (int)count);
            float floorLimit = pipeline.ContainerFloorY - 0.001f;
            for (int i = 0; i < particles.Length; i++)
            {
                UnityEngine.Assertions.Assert.IsTrue(
                    particles[i].Position.y >= floorLimit,
                    $"Particle {i} fell below floor: y={particles[i].Position.y}, floor={pipeline.ContainerFloorY}");
            }

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator NozzleSDF_CorrectlyRoutesToFallingBuffer()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const float nozzlePlaneLocalY = -0.35f;
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 512, autoRun: false);
            yield return null;

            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 2f,
                floorY = -2f,
                rimY = 2f,
                nozzlePlaneLocalY = nozzlePlaneLocalY,
                nozzleRadius = 1.0f,
                bucketRimLocalY = 0.35f
            });

            float spacing = pipeline.CellSize * 0.3f;
            float3 center = new float3(0f, nozzlePlaneLocalY - 0.05f, 0f);
            int nx = 3;
            int ny = 3;
            int nz = 3;
            float3 origin = center - new float3(nx - 1, ny - 1, nz - 1) * spacing * 0.5f;
            var particles = new FluidParticle[nx * ny * nz];
            int index = 0;
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        float3 pos = origin + new float3(x, y, z) * spacing;
                        particles[index++] = FluidParticleFactory.FromLocalSpawn(
                            pos,
                            float3.zero,
                            pipeline.RestDensity);
                    }
                }
            }

            int appended = pipeline.AppendParticles(particles, particles.Length);
            Assert.AreEqual(particles.Length, appended);

            uint initialInternal = pipeline.GetActiveParticleCount();
            Assert.AreEqual((uint)particles.Length, initialInternal);

            pipeline.SetSimulationActive(true);
            for (int frame = 0; frame < 5; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            Assert.IsTrue(
                pipeline.TryGetFallingParticleBuffer(out ComputeBuffer fallingBuffer, out uint fallingCount));
            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer internalBuffer, out uint internalCount));
            Assert.NotNull(fallingBuffer);
            Assert.NotNull(internalBuffer);
            Assert.Greater(fallingCount, 0u, "Expected particles routed to falling buffer through nozzle SDF");
            Assert.Less(internalCount, initialInternal, "Expected internal count to decrease as particles exit nozzle");
            Assert.LessOrEqual(fallingCount + internalCount, initialInternal);

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
