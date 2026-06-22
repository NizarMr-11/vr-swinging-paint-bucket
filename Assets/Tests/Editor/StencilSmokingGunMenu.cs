#if UNITY_EDITOR
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using System.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    public sealed class StencilSmokingGunRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("[SPH smoking-gun] Compute shaders not supported.");
                DestroyImmediate(gameObject);
                yield break;
            }

            const float cellSize = 0.01f;
            const float spacing = 0.01f;
            const int nx = 11;
            const int ny = 11;
            const int nz = 11;

            var pipeline = TestPipelineFactory.CreatePipeline(nx * ny * nz + 64, autoRun: false);
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

            int spawned = SpawnRestLatticeWithInteriorParticle0(pipeline, nx, ny, nz, spacing);
            if (spawned != nx * ny * nz)
            {
                Debug.LogError($"[SPH smoking-gun] Spawn failed: {spawned}");
                DestroyImmediate(pipeline.gameObject);
                DestroyImmediate(gameObject);
                yield break;
            }

            yield return null;
            pipeline.RebuildSpatialHashForVerification();

            if (!pipeline.TryCountStencilNeighbors(0, out int stencilCount, out int bruteForceCount))
            {
                Debug.LogError("[SPH smoking-gun] Neighbor count probe failed.");
            }
            else
            {
                Debug.Log(
                    $"[SPH smoking-gun] particle=0 stencilNeighbors={stencilCount} "
                    + $"bruteForce2h={bruteForceCount} h={pipeline.SmoothingRadius:F4}m spacing={spacing:F4}m");

                if (stencilCount != bruteForceCount)
                {
                    Debug.LogWarning(
                        $"[SPH smoking-gun] STENCIL MISMATCH: stencil={stencilCount} vs bruteForce={bruteForceCount}");
                }

                pipeline.ExecuteContainerSphDensityForVerification();
                yield return null;

                if (pipeline.TryGetDensityCacheBuffer(out ComputeBuffer buffer, out uint count))
                {
                    var particles = HarmonicEngine.Diagnostics.GpuParticleReadbackUtility.ReadParticles(buffer, (int)count);
                    Debug.Log(
                        $"[SPH smoking-gun] particle=0 density={particles[0].Density:F1} rest={pipeline.RestDensity:F1}");
                }
            }

            DestroyImmediate(pipeline.gameObject);
            DestroyImmediate(gameObject);
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

    public static class StencilSmokingGunMenu
    {
        private static bool _pendingRun;

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !_pendingRun)
            {
                return;
            }

            _pendingRun = false;
            var go = new GameObject("StencilSmokingGunRunner");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<StencilSmokingGunRunner>();
        }

        [MenuItem("HarmonicEngine/Testing/Run Stencil Smoking Gun Probe")]
        public static void Run()
        {
            _pendingRun = true;
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
