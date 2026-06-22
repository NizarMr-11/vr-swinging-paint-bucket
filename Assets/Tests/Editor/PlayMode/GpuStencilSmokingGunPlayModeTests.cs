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
    public class GpuStencilSmokingGunPlayModeTests
    {
        [UnityTest]
        public IEnumerator RestLattice_Particle0_StencilNeighborCount_OneFrameLog()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const float cellSize = 0.01f;
            const float spacing = 0.01f;
            const int nx = 11;
            const int ny = 11;
            const int nz = 11;

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: nx * ny * nz + 64, autoRun: false);
            yield return null;

            pipeline.SetCellSize(cellSize);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 2f,
                floorY = -2f,
                rimY = 3f
            });

            Assert.AreEqual(cellSize, pipeline.SmoothingRadius, 1e-5f, "Expected h = cellSize after stencil fix");

            int spawned = SpawnRestLatticeWithInteriorParticle0(pipeline, nx, ny, nz, spacing);
            Assert.AreEqual(nx * ny * nz, spawned);

            yield return null;
            pipeline.RebuildSpatialHashForVerification();

            Assert.IsTrue(pipeline.TryCountStencilNeighbors(0, out int stencilCount, out int bruteForceCount));
            Debug.Log(
                $"[SPH smoking-gun] particle=0 stencilNeighbors={stencilCount} "
                + $"bruteForce2h={bruteForceCount} h={pipeline.SmoothingRadius:F4}m spacing={spacing:F4}m");

            Assert.Greater(bruteForceCount, 25, "Expected ~30-60 brute-force neighbors with h=cellSize");
            Assert.Less(bruteForceCount, 80);
            Assert.AreEqual(
                bruteForceCount,
                stencilCount,
                "27-cell stencil should find all neighbors within 2h when h=cellSize");

            pipeline.ExecuteContainerSphDensityForVerification();
            yield return null;

            Assert.IsTrue(pipeline.TryGetDensityCacheBuffer(out ComputeBuffer buffer, out uint count));
            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(buffer, (int)count);
            Debug.Log($"[SPH smoking-gun] particle=0 density={particles[0].Density:F1} rest={pipeline.RestDensity:F1}");
            Assert.IsTrue(
                GpuParticleReadbackUtility.IsNearRestDensity(particles[0], pipeline.RestDensity, 0.15f),
                $"Interior particle density {particles[0].Density} should be within 15% of rest {pipeline.RestDensity}");

            Object.DestroyImmediate(pipeline.gameObject);
        }

        private static int SpawnRestLatticeWithInteriorParticle0(
            PipelineExecutionController pipeline,
            int nx,
            int ny,
            int nz,
            float spacing)
        {
            int total = nx * ny * nz;
            float3 center = float3.zero;
            float3 origin = center - new float3(nx - 1, ny - 1, nz - 1) * spacing * 0.5f;
            int cx = nx / 2;
            int cy = ny / 2;
            int cz = nz / 2;
            float3 corePosition = origin + new float3(cx, cy, cz) * spacing;

            var particles = new FluidParticle[total];
            particles[0] = FluidParticleFactory.FromWorldPosition(
                corePosition,
                float3.zero,
                pipeline.RestDensity);

            int written = 1;
            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    for (int x = 0; x < nx; x++)
                    {
                        if (x == cx && y == cy && z == cz)
                        {
                            continue;
                        }

                        float3 pos = origin + new float3(x, y, z) * spacing;
                        particles[written++] = FluidParticleFactory.FromWorldPosition(
                            pos,
                            float3.zero,
                            pipeline.RestDensity);
                    }
                }
            }

            return pipeline.AppendParticles(particles, written);
        }
    }
}
