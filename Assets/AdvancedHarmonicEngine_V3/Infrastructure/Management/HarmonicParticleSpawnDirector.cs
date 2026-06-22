using HarmonicEngine.Diagnostics;
using SwingingPaintBucket.Scene;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Optional scene helper that spawns lattice fill or every <see cref="ParticleSpawnVolume"/> on start.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class HarmonicParticleSpawnDirector : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private bool spawnLatticeOnStart = true;
        [SerializeField] private bool spawnAllOnStart;
        [Tooltip("When spawning volumes, disable active ParticleRainDirector components so they do not overwrite the lab setup.")]
        [SerializeField] private bool disableRainDirectorsOnSpawn = true;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }
        }

        private void Start()
        {
            EnsureContainerReady();

            if (TrySpawnLattice())
            {
                WriteManifestInitSnapshot();
                return;
            }

            if (spawnAllOnStart)
            {
                SpawnAllVolumes();
            }

            WriteManifestInitSnapshot();
        }

        private void WriteManifestInitSnapshot()
        {
            FluidContainer container = FindFirstObjectByType<FluidContainer>();
            HarmonicDiagnosticHub.RefreshManifestInit(
                spawnLatticeOnStart: spawnLatticeOnStart || (pipeline != null && pipeline.UseLatticeSpawn),
                sceneContainerName: container != null ? container.name : null);
        }

        private bool TrySpawnLattice()
        {
            if (pipeline == null)
            {
                return false;
            }

            if (!spawnLatticeOnStart && !pipeline.UseLatticeSpawn)
            {
                return false;
            }

            int spawned = pipeline.TrySpawnContainerLatticeFill();
            return spawned > 0;
        }

        [ContextMenu("Spawn Container Lattice")]
        public void SpawnContainerLattice()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            EnsureContainerReady();
            pipeline?.TrySpawnContainerLatticeFill();
        }

        [ContextMenu("Spawn All Volumes")]
        public void SpawnAllVolumes()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (pipeline == null)
            {
                return;
            }

            EnsureContainerReady();
            pipeline.EnableExternalIngestion(true);
            HarmonicParticleSpawnCoordinator.SpawnAll(pipeline);
        }

        private void EnsureContainerReady()
        {
            if (pipeline == null)
            {
                return;
            }

            if (disableRainDirectorsOnSpawn)
            {
                ParticleRainDirector[] rainDirectors = FindObjectsByType<ParticleRainDirector>(FindObjectsSortMode.None);
                for (int i = 0; i < rainDirectors.Length; i++)
                {
                    rainDirectors[i].enabled = false;
                }
            }

            FluidContainer container = FindFirstObjectByType<FluidContainer>();
            if (container != null)
            {
                container.ApplyToPipeline();
            }
            else
            {
                pipeline.SetContainerFluidEnabled(true);
            }

            pipeline.EnableExternalIngestion(true);
        }
    }
}
