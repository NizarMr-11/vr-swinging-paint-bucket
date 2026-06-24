using System;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Domain.Solvers;
using HarmonicEngine.Infrastructure.Rendering;
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
    /// Steps: (1) <c>BuildSpatialHashGrid(readSoa, activeCount)</c>; (2) bind
    /// <c>_SortedGridKeyValueBuffer</c>, <c>_CellStartEndBuffer</c>, SOA read buffers
    /// (<c>_Block0</c>, <c>_Block1</c>, …), <c>_DensityCacheDensities</c>/<c>_DensityCachePressures</c>
    /// + the SPH uniforms; (3) <c>#include</c> the query header;
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
        [SerializeField] private ComputeShader radixSortShader;
        [SerializeField] private ComputeShader streamCompactionShader;
        [SerializeField] private ComputeShader streamCompactionIntegrateShader;
        [SerializeField] private ComputeShader dataCompactionShader;
        [SerializeField] private ComputeShader fallingFluidWorldShader;
        [SerializeField] private ComputeShader eulerianDragGridShader;
        [SerializeField] private ComputeShader pbfSolverShader;
        [SerializeField] private ComputeShader containerRigidCarryShader;

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
        [SerializeField] private HarmonicFluidProfile fluidProfile;
        [SerializeField, Min(1f)] private float speedOfSound = 12f;

        [Header("Particle Ingestion")]
        [SerializeField] private bool useExternalParticleIngestion;
        [SerializeField] private bool autoRunPipeline = true;
        [SerializeField] private bool simulationActive = true;

        [Header("World falling only (no container)")]
        [SerializeField] private bool worldFallingOnly;

        [Header("Container fluid (world-space SPH in a cylinder)")]
        [SerializeField] private ContainerFluidSettings containerFluid = new();
        [Tooltip("Use Position Based Fluids instead of WCSPH for container fluid.")]
        [SerializeField] private bool usePBF = true;
        [SerializeField, Range(1, 8)] private int pbfIterations = 3;
        [Tooltip("PBF epsilon numerator: epsilon = scale / h^4. Default 0.006 → ε≈960 at h=0.05.")]
        [SerializeField, Min(1e-8f)] private float pbfEpsilonScale = 0.006f;
        [Tooltip("Position-correction over-relaxation ω. 1.0 = standard Jacobi; >1 amplifies corrections.")]
        [SerializeField, Range(1f, 2.5f)] private float pbfRelaxation = 1f;
        [Tooltip("Per-frame velocity damping in ApplyPositionsKernel. 0.85 = 15% energy loss per substep.")]
        [SerializeField, Range(0.5f, 0.99f)] private float pbfVelocityDamping = 0.85f;
        [Tooltip("Max position correction magnitude per PBF solve iteration (meters).")]
        [SerializeField, Min(0.001f)] private float pbfMaxPositionDelta = 0.015f;
        [Tooltip("Spiky-kernel cohesion in PredictPositionsKernel. Pulls particles across voids (Macklin & Müller 2013 §5). 0 = off.")]
        [SerializeField, Range(0f, 1f)] private float pbfCohesion = 0.3f;

        [Header("Color mixing")]
        [Tooltip("SPH color diffusion coefficient. 0 = colors stay distinct; higher = neighbors blend faster (marbling -> uniform mix).")]
        [SerializeField, Min(0f)] private float colorDiffusionRate;

        [Header("Performance")]
        [Tooltip("Size the spatial-hash sort/grid to the active particle count each frame instead of the full capacity. Big win at low/medium counts.")]
        [SerializeField] private bool dynamicSortSizing = true;
        [Tooltip("Lower bound for the per-frame padded sort/grid size (power of two).")]
        [SerializeField] private int minSortSize = 256;
        [Tooltip("Use 6-bit LSD radix sort instead of bitonic sort for spatial-hash key ordering.")]
        [SerializeField] private bool useRadixSort = true;
        [Tooltip("Disable per-frame GPU read-back sampling and verbose stage logging for clean perf runs.")]
        [HideInInspector] private bool perfDiagnosticsMuted;
        [Tooltip("Max CFL substeps per container-fluid frame. Needs ~80 at c=8, h=0.01, dt=8ms; cap must exceed sonic+velocity CFL.")]
        [SerializeField, Range(2, 512)] private int maxCflSubsteps = 256;

        [Header("Development")]
        [SerializeField] private bool seedTestParticlesOnStart = true;
        [Tooltip("When true, fills the container with a uniform lattice on Start instead of scene spawn volumes.")]
        [SerializeField] private bool useLatticeSpawn;
        [Tooltip("Max particles for container lattice fill (prevents over-packing the fill region).")]
        [SerializeField, Min(1)] private int latticeSpawnMaxCount = 3000;
        [Tooltip("Lattice site spacing = CellSize × scale. PBF (Macklin) uses h/2 = 0.5 so neighbors sit inside Spiky support.")]
        [SerializeField, Range(0.1f, 1f)] private float latticeSpacingScale = 0.5f;
        [SerializeField] private int testParticleCount = 2048;
        [SerializeField] private float testSpawnRadius = 0.2f;

        [HideInInspector] private bool verbosePipelineDiagnostics = true;
        [HideInInspector, Min(1)] private int frameDiagnosticInterval = 15;
        [HideInInspector, Min(0)] private int positionSampleInterval = 10;
        [HideInInspector, Min(1)] private int positionSampleCount = 64;
        [HideInInspector] private bool logStencilNeighborCount;
        [HideInInspector] private bool logSphToConsole = true;
        [HideInInspector] private bool muteSphTelemetry;
        [HideInInspector] private bool logPbfToConsole = true;
        [HideInInspector] private bool mutePbfTelemetry;

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
        public bool UsePbf => usePBF;
        public int PbfIterations => pbfIterations;
        public float PbfRelaxation => pbfRelaxation;
        public float CellSize => cellSize;
        public float LatticeSpacing => cellSize * latticeSpacingScale;
        public float SmoothingRadius => sphSolver.SmoothingRadius(cellSize);
        public float RestDensity => sphSolver.RestDensity;
        public float SpeedOfSound => speedOfSound;
        public float ContainerFloorY => containerFluid.floorY;
        public bool IsSimulationActive => simulationActive;
        public bool UseRadixSort => useRadixSort;
        public int LastRadixSortDispatchCount => _gpuRadixSort?.LastDispatchCount ?? 0;
        public HarmonicFluidProfile FluidProfile => fluidProfile;

        public void SetFluidProfile(HarmonicFluidProfile profile) => fluidProfile = profile;

        public void SetUseRadixSort(bool enabled)
        {
            useRadixSort = enabled;
            _radixSortAnnounced = false;
            _bitonicSortAnnounced = false;
        }

        public void SetDynamicSortSizing(bool enabled) => dynamicSortSizing = enabled;

        public void SetSimulationMode(HarmonicSimulationMode mode) => simulationMode = mode;

        public void SetGravity(Vector3 value) => gravity = value;

        public void SetUsePbf(bool enabled) => usePBF = enabled;

        public void SetCellSize(float value) => cellSize = Mathf.Max(0.01f, value);

        private HarmonicScreenSpaceFluidRenderer _fluidProfileRenderer;

        private void Awake()
        {
            SyncSpeedOfSoundToSolver();
            ResolveIntegrateShader();
            ResolvePbfShader();
            ResolveRadixSortShader();
            InitializeBuffers();
            CacheKernels();
            ApplyFluidProfileIfAssigned();
            _lastBucketPosition = GetBucketPosition();
        }

        private void ResolveIntegrateShader()
        {
            if (streamCompactionIntegrateShader != null)
            {
                return;
            }

#if UNITY_EDITOR
            streamCompactionIntegrateShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/StreamCompactionIntegrate.compute");
#endif
            if (streamCompactionIntegrateShader == null)
            {
                Debug.LogError(
                    "[HarmonicPipeline] streamCompactionIntegrateShader is not assigned. "
                    + "Assign StreamCompactionIntegrate.compute on the pipeline controller.");
            }
        }

        private void ResolvePbfShader()
        {
            if (pbfSolverShader != null)
            {
                return;
            }

#if UNITY_EDITOR
            pbfSolverShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/PbfSolver.compute");
#endif
            if (pbfSolverShader == null)
            {
                Debug.LogWarning(
                    "[HarmonicPipeline] pbfSolverShader is not assigned. "
                    + "Assign PbfSolver.compute or disable usePBF.");
            }
        }

        private void ResolveRadixSortShader()
        {
            if (radixSortShader != null)
            {
                return;
            }

#if UNITY_EDITOR
            radixSortShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/RadixSort.compute");
#endif
            if (radixSortShader == null && useRadixSort)
            {
                Debug.LogWarning(
                    "[HarmonicPipeline] radixSortShader is not assigned. "
                    + "Assign RadixSort.compute or disable useRadixSort.");
            }
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
            ApplyFluidProfileIfAssigned();

            if (autoRunPipeline)
            {
                ExecutePipelineFrame(Time.deltaTime);
            }
        }

        private void ApplyFluidProfileIfAssigned()
        {
            if (fluidProfile == null)
            {
                return;
            }

            _fluidProfileRenderer ??= GetComponent<HarmonicScreenSpaceFluidRenderer>()
                ?? FindFirstObjectByType<HarmonicScreenSpaceFluidRenderer>();
            fluidProfile.ApplyTo(this, _fluidProfileRenderer);
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
