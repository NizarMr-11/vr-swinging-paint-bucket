using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Scene-attached spawn volume: shape, color, priority, and particle count.
    /// Attach to any GameObject, configure in the Inspector, and emit alone or via
    /// <see cref="HarmonicParticleSpawnCoordinator"/>.
    /// </summary>
    [AddComponentMenu("Harmonic Engine/Particle Spawn Volume")]
    public class ParticleSpawnVolume : MonoBehaviour
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
        [SerializeField] private float cylinderRadius = 0.5f;
        [SerializeField] private float cylinderHeight = 2f;

        [Header("Particles")]
        [Tooltip("Requested particles for this volume (may be reduced by priority coordinator).")]
        [SerializeField] private int particleCount = 4096;
        [SerializeField] private float restDensity = 1000f;
        [SerializeField] private Vector3 initialVelocity = Vector3.zero;
        [Tooltip("Higher priority spawns first and receives a larger share when capacity is limited.")]
        [SerializeField] private int spawnPriority;
        [Tooltip("Color assigned to every particle from this volume.")]
        [SerializeField] private Color spawnColor = Color.white;
        [SerializeField] private uint seed = 12345u;
        [SerializeField, Min(1)] private int meshMaxAttemptsPerPoint = 32;

        [Header("Flow")]
        [SerializeField] private bool emitOnStart;
        [SerializeField] private bool clearBeforeEmit = true;
        [SerializeField] private bool activateSimulationOnEmit = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmo = true;
        [Tooltip("When enabled, draw a faint outline even when this object is not selected.")]
        [SerializeField] private bool drawGizmoWhenNotSelected = true;

        public int ParticleCount => particleCount;
        public int SpawnPriority => spawnPriority;
        public ShapeVolumeType ShapeType => shapeType;
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

        public void SetCylinder(float radius, float height)
        {
            cylinderRadius = radius;
            cylinderHeight = height;
        }

        public void SetSpawnColor(Color color) => spawnColor = color;

        public void SetSpawnPriority(int priority) => spawnPriority = priority;

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

        /// <summary>Spawns using this volume's full <see cref="particleCount"/>.</summary>
        public int Emit()
        {
            return Emit(particleCount, clearBeforeEmit, activateSimulationOnEmit);
        }

        /// <summary>Spawns with an explicit particle count (used by the priority coordinator).</summary>
        public int Emit(int count, bool clearFirst, bool activateSimulation)
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[ParticleSpawnVolume] No pipeline assigned; cannot emit.");
                return 0;
            }

            if (shapeSource == null)
            {
                shapeSource = transform;
            }

            if (clearFirst)
            {
                pipeline.ClearAllParticles();
            }

            HarmonicSpawnRegion region = BuildRegion(count);
            int appended = HarmonicParticleSpawner.Spawn(pipeline, region);
            LastEmittedCount = appended;

            if (activateSimulation)
            {
                pipeline.SetSimulationActive(true);
            }

            PublishEmitDiagnostic(region, appended);
            return appended;
        }

        public HarmonicSpawnRegion BuildRegion(int countOverride = -1)
        {
            Vector3 scale = shapeSource.lossyScale;
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            var region = new HarmonicSpawnRegion
            {
                shape = shapeType,
                center = shapeSource.position,
                rotation = shapeSource.rotation,
                particleCount = countOverride >= 0 ? countOverride : particleCount,
                restDensity = restDensity,
                initialVelocity = initialVelocity,
                spawnColor = spawnColor,
                spawnPriority = spawnPriority,
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
                case ShapeVolumeType.Cylinder:
                    float cylRadial = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    region.cylinderRadius = cylinderRadius * cylRadial;
                    region.cylinderHeight = cylinderHeight * Mathf.Abs(scale.y);
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

        internal void PublishEmitDiagnostic(HarmonicSpawnRegion region, int appended)
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
                HarmonicDiagnosticEventType.ShapeEmit,
                "SHAPE",
                $"shape={shapeType} priority={spawnPriority} requested={region.particleCount} appended={appended} " +
                $"restDensity={restDensity}",
                frame,
                t,
                active,
                intArg0: appended,
                intArg1: region.particleCount));
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo || !drawGizmoWhenNotSelected)
            {
                return;
            }

            DrawShapeGizmos(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
            {
                return;
            }

            DrawShapeGizmos(selected: true);
        }

        private void DrawShapeGizmos(bool selected)
        {
            Transform src = shapeSource != null ? shapeSource : transform;
            Color fill = spawnColor;
            fill.a = selected ? 0.2f : 0.08f;
            Gizmos.color = fill;
            Gizmos.matrix = Matrix4x4.TRS(src.position, src.rotation, src.lossyScale);

            switch (shapeType)
            {
                case ShapeVolumeType.Box:
                    Gizmos.DrawCube(Vector3.zero, boxSize);
                    Gizmos.color = spawnColor;
                    Gizmos.DrawWireCube(Vector3.zero, boxSize);
                    break;
                case ShapeVolumeType.Sphere:
                    Gizmos.DrawSphere(Vector3.zero, sphereRadius);
                    Gizmos.color = spawnColor;
                    Gizmos.DrawWireSphere(Vector3.zero, sphereRadius);
                    break;
                case ShapeVolumeType.Capsule:
                    DrawWireCapsuleGizmo(capsuleRadius, capsuleHeight, spawnColor);
                    break;
                case ShapeVolumeType.Cylinder:
                    DrawWireCylinderGizmo(cylinderRadius, cylinderHeight, spawnColor);
                    break;
                case ShapeVolumeType.Mesh:
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        Gizmos.matrix = meshFilter.transform.localToWorldMatrix;
                        Gizmos.color = spawnColor;
                        Gizmos.DrawWireMesh(meshFilter.sharedMesh);
                    }

                    break;
            }
        }

        private static void DrawWireCapsuleGizmo(float radius, float height, Color color)
        {
            Gizmos.color = color;
            float half = height * 0.5f - radius;
            Gizmos.DrawWireSphere(new Vector3(0f, half, 0f), radius);
            Gizmos.DrawWireSphere(new Vector3(0f, -half, 0f), radius);
        }

        private static void DrawWireCylinderGizmo(float radius, float height, Color color)
        {
            Gizmos.color = color;
            float half = height * 0.5f;
            Gizmos.DrawWireSphere(new Vector3(0f, half, 0f), radius);
            Gizmos.DrawWireSphere(new Vector3(0f, -half, 0f), radius);
        }
    }
}
