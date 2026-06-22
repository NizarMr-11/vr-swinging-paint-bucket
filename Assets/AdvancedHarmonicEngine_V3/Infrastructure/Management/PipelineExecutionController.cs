using System;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Domain.Solvers;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// GPU-resident SPH pipeline owner and the engine's main entry point.
    ///
    /// Adding a kernel that needs neighbor queries (the spatial hash): bind the four
    /// query buffers the grid build produces and dispatch after <c>BuildSpatialHashGrid</c>.
    /// The reusable iterator lives in
    /// <c>Infrastructure/ComputeShaders/Include/SphNeighborQuery.hlsl</c> (call
    /// <c>ForEachNeighbor</c>); shared structs/hash/kernels live in <c>SphCommon.hlsl</c>.
    /// Steps: (1) <c>BuildSpatialHashGrid(readBuffer, activeCount)</c>; (2) bind
    /// <c>_SortedGridKeyValueBuffer</c>, <c>_CellStartEndBuffer</c>, <c>_ReadOnlyParticleSource</c>,
    /// <c>_DensityWritableCache</c> + the SPH uniforms; (3) <c>#include</c> the query header;
    /// (4) <c>DispatchIndirect</c> with <c>_indirectArgsBuffer</c>.
    ///
    /// Communication: pull (the <c>TryGet*</c> buffer accessors / <see cref="IHarmonicParticleSource"/>)
    /// or push (<see cref="FrameCompleted"/>, raised once per simulated frame).
    /// </summary>
    public partial class PipelineExecutionController : MonoBehaviour, IHarmonicParticleSource
    {
        /// <summary>
        /// Raised at the end of every simulated frame (all modes) with a summary snapshot,
        /// so overlays/debug tooling can react without polling buffers.
        /// </summary>
        public event Action<HarmonicFrameInfo> FrameCompleted;

        [Header("Compute Shaders")]
        [SerializeField] private ComputeShader argumentUtilityShader;
        [SerializeField] private ComputeShader spatialHashGridShader;
        [SerializeField] private ComputeShader streamCompactionShader;
        [SerializeField] private ComputeShader dataCompactionShader;
        [SerializeField] private ComputeShader fallingFluidWorldShader;
        [SerializeField] private ComputeShader eulerianDragGridShader;

        [Header("Scene References")]
        [SerializeField] private Transform bucketTransform;
        [SerializeField] private MonoBehaviour bucketKinematicProvider;
        [SerializeField] private bool driveBucketFromTransform = true;

        [Header("Capacity")]
        [SerializeField] private int maxCapacity = 1_000_000;
        [SerializeField, Min(0.01f)] private float cellSize = 0.1f;
        [SerializeField] private float nozzlePlaneLocalY = -0.35f;
        [SerializeField] private float nozzleRadius = 0.05f;
        [SerializeField] private float bucketRimLocalY = 0.35f;
        [SerializeField] private Vector3 gravity = new(0f, -9.81f, 0f);
        [SerializeField] private bool applyNonInertialPseudoForces = true;
        [SerializeField] private float worldDrag = 0.15f;
        [SerializeField] private float canvasPlaneY = -6f;
        [Tooltip("When true the plane culls particles into the canvas-hit buffer (paint canvas). When false the plane is a solid floor and particles stay alive/visible.")]
        [SerializeField] private bool canvasCullingEnabled = true;
        [SerializeField, Range(0f, 1f)] private float floorRestitution;
        [SerializeField, Range(0f, 1f)] private float floorFriction = 0.85f;
        [SerializeField] private bool enableEulerianDrag;
        [SerializeField] private int dragGridVolume = 4096;
        [SerializeField] private float dragStrength = 1f;
        [SerializeField] private float dragDecay = 0.5f;
        [SerializeField] private float ambientWindStrength;
        [SerializeField] private int maxCanvasHitsPerFrame = 16_384;
        [Tooltip("When canvas culling is on, particles rest on the plane and drain wetness into the canvas before removal.")]
        [SerializeField] private bool canvasPaintAbsorbEnabled = true;
        [SerializeField, Min(0.01f)] private float canvasAbsorbRate = 1.5f;
        [SerializeField, Min(0f)] private float canvasAbsorbPaintWeightScale = 1f;

        [Header("Simulation Mode")]
        [SerializeField] private HarmonicSimulationMode simulationMode = HarmonicSimulationMode.Live;
        [SerializeField] private HarmonicQualityTier qualityTier = HarmonicQualityTier.High;

        [Header("SPH Parameters")]
        [SerializeField] private SphFluidSolverCore sphSolver = new();
        [SerializeField, Min(1f)] private float speedOfSound = 12f;

        [Header("Particle Ingestion")]
        [SerializeField] private bool useExternalParticleIngestion;
        [SerializeField] private bool autoRunPipeline = true;
        [SerializeField] private bool simulationActive = true;

        [Header("World falling only (no container)")]
        [SerializeField] private bool worldFallingOnly;
        [SerializeField] private bool driveWorldParticlesFromPipelineRoot = true;

        [Header("Container fluid (world-space SPH in a cylinder)")]
        [SerializeField] private ContainerFluidSettings containerFluid = new();

        [Header("Color mixing")]
        [Tooltip("SPH color diffusion coefficient. 0 = colors stay distinct; higher = neighbors blend faster (marbling -> uniform mix).")]
        [SerializeField, Min(0f)] private float colorDiffusionRate;

        [Header("Performance")]
        [Tooltip("Size the spatial-hash sort/grid to the active particle count each frame instead of the full capacity. Big win at low/medium counts.")]
        [SerializeField] private bool dynamicSortSizing = true;
        [Tooltip("Lower bound for the per-frame padded sort/grid size (power of two).")]
        [SerializeField] private int minSortSize = 256;
        [Tooltip("Disable per-frame GPU read-back sampling and verbose stage logging for clean perf runs.")]
        [HideInInspector] private bool perfDiagnosticsMuted;
        [Tooltip("Max CFL substeps per container-fluid frame. Needs ~80 at c=8, h=0.01, dt=8ms; cap must exceed sonic+velocity CFL.")]
        [SerializeField, Range(2, 512)] private int maxCflSubsteps = 256;

        [Header("Development")]
        [SerializeField] private bool seedTestParticlesOnStart = true;
        [Tooltip("When true, fills the container with a uniform lattice on Start instead of scene spawn volumes.")]
        [SerializeField] private bool useLatticeSpawn;
        [SerializeField] private int testParticleCount = 2048;
        [SerializeField] private float testSpawnRadius = 0.2f;

        [HideInInspector] private bool verbosePipelineDiagnostics = true;
        [HideInInspector, Min(1)] private int frameDiagnosticInterval = 15;
        [HideInInspector, Min(0)] private int positionSampleInterval = 10;
        [HideInInspector, Min(1)] private int positionSampleCount = 64;
        [HideInInspector] private bool logStencilNeighborCount;
        [HideInInspector] private bool logSphToConsole = true;
        [HideInInspector] private bool muteSphTelemetry;

        public int PaddedSortSize => _paddedSortSize;
        public int FrameSortSize => _frameSortSize;
        public int MaxCapacity => maxCapacity;
        public HarmonicParticleBufferService BufferService => _bufferService;
        public bool UsesExternalIngestion => useExternalParticleIngestion;
        public ComputeBuffer QuantizedBakeBuffer => _quantizedBakeBuffer;
        public uint LastFallingQuantizeCount => _lastFallingQuantizeCount;
        public uint LastCanvasHitCount => _lastCanvasHitCount;
        public HarmonicSimulationMode SimulationMode => simulationMode;
        public float CanvasPlaneY => canvasPlaneY;
        public bool WorldFallingOnly => worldFallingOnly;
        public bool CanvasCullingEnabled => canvasCullingEnabled;
        public bool ContainerFluidEnabled => containerFluid.enabled;
        public float CellSize => cellSize;
        public float SmoothingRadius => sphSolver.SmoothingRadius(cellSize);
        public float RestDensity => sphSolver.RestDensity;
        public float SpeedOfSound => speedOfSound;
        public float ContainerFloorY => containerFluid.floorY;
        public bool IsSimulationActive => simulationActive;

        public void SetSimulationMode(HarmonicSimulationMode mode) => simulationMode = mode;

        public void SetGravity(Vector3 value) => gravity = value;

        public void SetCellSize(float value) => cellSize = Mathf.Max(0.01f, value);

        private void Awake()
        {
            SyncSpeedOfSoundToSolver();
            InitializeBuffers();
            CacheKernels();
            _lastBucketPosition = GetBucketPosition();
        }

        private void Start()
        {
            if (!useLatticeSpawn && seedTestParticlesOnStart && !useExternalParticleIngestion)
            {
                SeedTestParticlesIfEmpty();
            }
        }

        private void Update()
        {
            if (autoRunPipeline)
            {
                ExecutePipelineFrame(Time.deltaTime);
            }
        }

        public int AppendParticles(FluidParticle[] particles, int count)
        {
            int appended = _bufferService?.AppendParticles(particles, count) ?? 0;
            if (appended > 0)
            {
                _cachedInternalCount = GetActiveParticleCount();
                PublishDiagnostic(
                    HarmonicDiagnosticEventType.ParticlesAppended,
                    "PIPELINE",
                    $"appended={appended} total={_cachedInternalCount}",
                    intArg0: appended,
                    intArg1: count);
            }

            return appended;
        }

        public uint GetActiveParticleCount()
        {
            uint raw = _bufferService?.GetActiveCount() ?? 0;
            return raw > (uint)maxCapacity ? (uint)maxCapacity : raw;
        }

        public void ClearAllParticles()
        {
            _bufferService?.ClearAll();
            _cachedInternalCount = 0;
            PublishDiagnostic(HarmonicDiagnosticEventType.ParticlesCleared, "PIPELINE", "cleared");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SyncSpeedOfSoundToSolver();
        }
#endif

        private void OnDestroy()
        {
            ReleaseBuffers();
        }
    }
}
