using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace SwingingPaintBucket.Scene
{
    /// <summary>
    /// Fills a 3D shape with a uniformly distributed share of particles and sends that initial
    /// state to the harmonic engine. Supports box / sphere / capsule primitives and arbitrary
    /// mesh volumes. Sampling + colored ingestion is delegated to the engine-level
    /// <see cref="HarmonicParticleSpawner"/> via a <see cref="HarmonicSpawnRegion"/>.
    /// </summary>
    public class ShapeVolumeEmitter : MonoBehaviour
    {
        [Header("Engine")]
        [SerializeField] private PipelineExecutionController pipeline;

        [Header("Shape")]
        [SerializeField] private ShapeVolumeType shapeType = ShapeVolumeType.Sphere;
        [Tooltip("Transform that positions/orients the shape. Defaults to this object's transform.")]
        [SerializeField] private Transform shapeSource;
        [Tooltip("Mesh used when shapeType is Mesh. Defaults to a MeshFilter on the shape source.")]
        [SerializeField] private MeshFilter meshFilter;

        [Header("Primitive size (local, before transform scale)")]
        [SerializeField] private Vector3 boxSize = Vector3.one;
        [SerializeField] private float sphereRadius = 0.5f;
        [SerializeField] private float capsuleRadius = 0.5f;
        [SerializeField] private float capsuleHeight = 2f;

        [Header("Particles")]
        [Tooltip("Number of particles to divide equally across the shape volume.")]
        [SerializeField] private int particleCount = 4096;
        [SerializeField] private float restDensity = 1000f;
        [Tooltip("Color assigned to every particle from this volume (mixes via SPH color diffusion).")]
        [SerializeField] private Color spawnColor = Color.white;
        [SerializeField] private uint seed = 12345u;
        [SerializeField, Min(1)] private int meshMaxAttemptsPerPoint = 32;

        [Header("Flow")]
        [SerializeField] private bool emitOnStart = true;
        [SerializeField] private bool clearBeforeEmit = true;
        [Tooltip("Activate the engine simulation right after sending the initial state.")]
        [SerializeField] private bool activateSimulationOnEmit = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmo = true;
        [SerializeField] private Color gizmoColor = new(0.2f, 0.8f, 1f, 0.25f);

        public int LastEmittedCount { get; private set; }

        public void SetPipeline(PipelineExecutionController controller) => pipeline = controller;

        public void Configure(
            ShapeVolumeType type,
            int count,
            uint randomSeed,
            bool emitOnStartValue = false,
            bool clearBeforeEmitValue = true,
            bool activateSimulationOnEmitValue = false)
        {
            shapeType = type;
            particleCount = count;
            seed = randomSeed;
            emitOnStart = emitOnStartValue;
            clearBeforeEmit = clearBeforeEmitValue;
            activateSimulationOnEmit = activateSimulationOnEmitValue;
        }

        /// <summary>
        /// Applies run-time emit settings from a director/UI without overwriting shape type or dimensions.
        /// </summary>
        public void PrepareRun(
            int count,
            bool clearBeforeEmitValue = true,
            bool activateSimulationOnEmitValue = true)
        {
            particleCount = count;
            emitOnStart = false;
            clearBeforeEmit = clearBeforeEmitValue;
            activateSimulationOnEmit = activateSimulationOnEmitValue;
        }

        public void SetSphere(float radius) => sphereRadius = radius;

        public void SetBox(Vector3 size) => boxSize = size;

        public void SetCapsule(float radius, float height)
        {
            capsuleRadius = radius;
            capsuleHeight = height;
        }

        public void SetSpawnColor(Color color) => spawnColor = color;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (shapeSource == null)
            {
                shapeSource = transform;
            }

            if (meshFilter == null && shapeType == ShapeVolumeType.Mesh)
            {
                meshFilter = shapeSource.GetComponent<MeshFilter>();
            }
        }

        private void Start()
        {
            if (emitOnStart)
            {
                Emit();
            }
        }

        /// <summary>
        /// Builds a spawn region from the inspector shape and sends the initial state to the engine.
        /// Returns the number of particles appended.
        /// </summary>
        public int Emit()
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[ShapeVolumeEmitter] No pipeline assigned; cannot emit.");
                return 0;
            }

            if (shapeSource == null)
            {
                shapeSource = transform;
            }

            if (clearBeforeEmit)
            {
                pipeline.ClearAllParticles();
            }

            HarmonicSpawnRegion region = BuildRegion();
            int appended = HarmonicParticleSpawner.Spawn(pipeline, region);
            LastEmittedCount = appended;

            if (activateSimulationOnEmit)
            {
                pipeline.SetSimulationActive(true);
            }

            Publish(
                $"shape={shapeType} requested={region.particleCount} appended={appended} " +
                $"restDensity={restDensity}",
                intArg0: appended,
                intArg1: region.particleCount);

            return appended;
        }

        private HarmonicSpawnRegion BuildRegion()
        {
            Vector3 scale = shapeSource.lossyScale;
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            var region = new HarmonicSpawnRegion
            {
                shape = shapeType,
                center = shapeSource.position,
                rotation = shapeSource.rotation,
                particleCount = particleCount,
                restDensity = restDensity,
                spawnColor = spawnColor,
                seed = seed,
                meshMaxAttemptsPerPoint = meshMaxAttemptsPerPoint
            };

            switch (shapeType)
            {
                case ShapeVolumeType.Box:
                    region.boxSize = new Vector3(
                        boxSize.x * Mathf.Abs(scale.x),
                        boxSize.y * Mathf.Abs(scale.y),
                        boxSize.z * Mathf.Abs(scale.z));
                    break;
                case ShapeVolumeType.Sphere:
                    region.sphereRadius = sphereRadius * maxScale;
                    break;
                case ShapeVolumeType.Capsule:
                    float radial = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    region.capsuleRadius = capsuleRadius * radial;
                    region.capsuleHeight = capsuleHeight * Mathf.Abs(scale.y);
                    break;
                case ShapeVolumeType.Mesh:
                    if (meshFilter == null)
                    {
                        meshFilter = shapeSource.GetComponent<MeshFilter>();
                    }

                    if (meshFilter != null)
                    {
                        region.mesh = meshFilter.sharedMesh;
                        region.meshToWorld = meshFilter.transform.localToWorldMatrix;
                    }

                    break;
            }

            return region;
        }

        private static void Publish(string message, int intArg0 = 0, int intArg1 = 0)
        {
            if (!HarmonicDiagnosticHub.Enabled)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            float t = session?.ElapsedSeconds ?? 0f;
            int frame = session?.FrameIndex ?? 0;
            uint active = session?.ReadActiveParticleCount() ?? 0;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.ShapeEmit, "SHAPE", message, frame, t, active,
                intArg0: intArg0, intArg1: intArg1));
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
            {
                return;
            }

            Transform src = shapeSource != null ? shapeSource : transform;
            Gizmos.color = gizmoColor;
            Gizmos.matrix = Matrix4x4.TRS(src.position, src.rotation, src.lossyScale);

            switch (shapeType)
            {
                case ShapeVolumeType.Box:
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);
                    break;
                case ShapeVolumeType.Sphere:
                    Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
                    break;
                case ShapeVolumeType.Capsule:
                    Gizmos.DrawWireSphere(new Vector3(0f, (capsuleHeight * 0.5f) - capsuleRadius, 0f), capsuleRadius);
                    Gizmos.DrawWireSphere(new Vector3(0f, -(capsuleHeight * 0.5f) + capsuleRadius, 0f), capsuleRadius);
                    break;
                case ShapeVolumeType.Mesh:
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        Gizmos.matrix = meshFilter.transform.localToWorldMatrix;
                        Gizmos.DrawWireMesh(meshFilter.sharedMesh);
                    }

                    break;
            }
        }
    }
}
