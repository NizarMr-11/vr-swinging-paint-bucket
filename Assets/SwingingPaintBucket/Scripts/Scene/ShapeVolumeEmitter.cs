using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using Unity.Mathematics;
using UnityEngine;

namespace SwingingPaintBucket.Scene
{
    /// <summary>
    /// Fills a 3D shape with an equal, uniformly distributed share of particles and sends that
    /// initial state to the harmonic engine. Supports box / sphere / capsule primitives and
    /// arbitrary mesh volumes (via <see cref="MeshVolumeSampler"/>).
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

        private float3[] _positionBuffer;
        private FluidParticle[] _particleBuffer;

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
        /// Samples the shape, builds particles, and sends the initial state to the engine.
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

            int requested = Mathf.Clamp(particleCount, 0, pipeline.MaxCapacity);
            if (requested <= 0)
            {
                return 0;
            }

            EnsureBuffers(requested);
            int sampled = SamplePositions(requested);
            if (sampled <= 0)
            {
                Publish($"emitFailed shape={shapeType} requested={requested} sampled=0");
                return 0;
            }

            for (int i = 0; i < sampled; i++)
            {
                _particleBuffer[i] = FluidParticleFactory.FromWorldPosition(
                    (Vector3)_positionBuffer[i], Vector3.zero, restDensity);
            }

            if (clearBeforeEmit)
            {
                pipeline.ClearAllParticles();
            }

            int appended = pipeline.AppendParticles(_particleBuffer, sampled);
            LastEmittedCount = appended;

            if (activateSimulationOnEmit)
            {
                pipeline.SetSimulationActive(true);
            }

            Publish(
                $"shape={shapeType} requested={requested} sampled={sampled} appended={appended} " +
                $"perShape={appended} restDensity={restDensity}",
                intArg0: appended,
                intArg1: requested);

            return appended;
        }

        private int SamplePositions(int count)
        {
            Vector3 center = shapeSource.position;
            quaternion rotation = shapeSource.rotation;
            Vector3 scale = shapeSource.lossyScale;
            float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            switch (shapeType)
            {
                case ShapeVolumeType.Box:
                    float3 worldSize = new(
                        boxSize.x * Mathf.Abs(scale.x),
                        boxSize.y * Mathf.Abs(scale.y),
                        boxSize.z * Mathf.Abs(scale.z));
                    return ShapeVolumeSampler.SampleBox(center, worldSize, rotation, count, seed, _positionBuffer);

                case ShapeVolumeType.Sphere:
                    return ShapeVolumeSampler.SampleSphere(center, sphereRadius * maxScale, count, seed, _positionBuffer);

                case ShapeVolumeType.Capsule:
                    float radial = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    return ShapeVolumeSampler.SampleCapsule(
                        center,
                        capsuleRadius * radial,
                        capsuleHeight * Mathf.Abs(scale.y),
                        rotation,
                        count,
                        seed,
                        _positionBuffer);

                case ShapeVolumeType.Mesh:
                    return SampleMesh(count);

                default:
                    return 0;
            }
        }

        private int SampleMesh(int count)
        {
            if (meshFilter == null)
            {
                meshFilter = shapeSource.GetComponent<MeshFilter>();
            }

            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null)
            {
                Debug.LogWarning("[ShapeVolumeEmitter] Mesh shape selected but no readable mesh found.");
                return 0;
            }

            Vector3[] localVerts = mesh.vertices;
            int[] tris = mesh.triangles;
            Matrix4x4 toWorld = meshFilter.transform.localToWorldMatrix;

            var worldVerts = new Vector3[localVerts.Length];
            var worldBounds = new Bounds(toWorld.MultiplyPoint3x4(localVerts.Length > 0 ? localVerts[0] : Vector3.zero), Vector3.zero);
            for (int i = 0; i < localVerts.Length; i++)
            {
                worldVerts[i] = toWorld.MultiplyPoint3x4(localVerts[i]);
                worldBounds.Encapsulate(worldVerts[i]);
            }

            return MeshVolumeSampler.SampleInsideMesh(
                worldBounds, worldVerts, tris, count, seed, meshMaxAttemptsPerPoint, _positionBuffer);
        }

        private void EnsureBuffers(int count)
        {
            if (_positionBuffer == null || _positionBuffer.Length < count)
            {
                _positionBuffer = new float3[count];
            }

            if (_particleBuffer == null || _particleBuffer.Length < count)
            {
                _particleBuffer = new FluidParticle[count];
            }
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
