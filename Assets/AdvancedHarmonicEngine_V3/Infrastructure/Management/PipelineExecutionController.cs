using HarmonicEngine.Core.DataStructures;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Domain.Solvers;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public class PipelineExecutionController : MonoBehaviour
    {
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
        [SerializeField, Min(0.01f)] private float cellSize = 0.25f;
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

        [Header("Simulation Mode")]
        [SerializeField] private HarmonicSimulationMode simulationMode = HarmonicSimulationMode.Live;
        [SerializeField] private HarmonicQualityTier qualityTier = HarmonicQualityTier.High;

        [Header("SPH Parameters")]
        [SerializeField] private SphFluidSolverCore sphSolver = new();

        [Header("Particle Ingestion")]
        [SerializeField] private bool useExternalParticleIngestion;
        [SerializeField] private bool autoRunPipeline = true;
        [SerializeField] private bool simulationActive = true;

        [Header("World falling only (no container)")]
        [SerializeField] private bool worldFallingOnly;
        [SerializeField] private bool driveWorldParticlesFromPipelineRoot = true;

        [Header("Development")]
        [SerializeField] private bool seedTestParticlesOnStart = true;
        [SerializeField] private int testParticleCount = 2048;
        [SerializeField] private float testSpawnRadius = 0.2f;

        [Header("Engine Diagnostics")]
        [Tooltip("Emit detailed per-stage engine diagnostics (counts, mode) every frame the count changes.")]
        [SerializeField] private bool verbosePipelineDiagnostics = true;
        [Tooltip("Throttle the plain per-frame line: only log every N frames when the active count is unchanged (still always logs on change / canvas hits).")]
        [SerializeField, Min(1)] private int frameDiagnosticInterval = 15;
        [Tooltip("Read back a small particle sample from the GPU every N frames to confirm motion (min/max/avg Y + velocity). 0 disables sampling.")]
        [SerializeField, Min(0)] private int positionSampleInterval = 30;
        [SerializeField, Min(1)] private int positionSampleCount = 16;

        private ComputeBuffer _bufferInternalA;
        private ComputeBuffer _bufferInternalB;
        private ComputeBuffer _bufferFalling;
        private ComputeBuffer _bufferFallingWorld;
        private ComputeBuffer _bufferDragGrid;
        private ComputeBuffer _bufferDragParticleScratch;
        private ComputeBuffer _bufferDensityCache;
        private ComputeBuffer _gridKeyValueBuffer;
        private ComputeBuffer _cellStartEndBuffer;
        private ComputeBuffer _indirectArgsBuffer;
        private ComputeBuffer _quantizedBakeBuffer;
        private ComputeBuffer _counterReadbackBuffer;
        private ComputeBuffer _bufferCanvasHits;

        private PingPongCounterManager _pingPong;
        private HarmonicParticleBufferService _bufferService;
        private int _paddedSortSize;
        private Vector3 _lastBucketPosition;

        public int PaddedSortSize => _paddedSortSize;
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

        public void SetSimulationMode(HarmonicSimulationMode mode) => simulationMode = mode;

        public void SetWorldFallingOnly(bool enabled)
        {
            worldFallingOnly = enabled;
            if (enabled)
            {
                seedTestParticlesOnStart = false;
                useExternalParticleIngestion = true;
                enableEulerianDrag = false;
                applyNonInertialPseudoForces = false;
                driveBucketFromTransform = false;
            }
        }

        public void SetCanvasPlaneY(float planeY) => canvasPlaneY = planeY;

        /// <summary>
        /// Toggle paint-canvas culling. When disabled the canvas plane acts as a solid floor
        /// and particles are kept alive instead of being consumed (so they stay visible).
        /// </summary>
        public void SetCanvasCullingEnabled(bool enabled) => canvasCullingEnabled = enabled;

        public void SetFloorResponse(float restitution, float friction)
        {
            floorRestitution = Mathf.Clamp01(restitution);
            floorFriction = Mathf.Clamp01(friction);
        }

        public void SetEnableEulerianDrag(bool enable) => enableEulerianDrag = enable;

        public bool TryCopyIndirectDispatchArgs(int[] destination)
        {
            if (_indirectArgsBuffer == null || destination == null || destination.Length < 3)
            {
                return false;
            }

            _indirectArgsBuffer.GetData(destination);
            return true;
        }

        public void ApplyQualityTier(HarmonicQualityTier tier)
        {
            qualityTier = tier;
            int targetCapacity = HarmonicQualityPresets.GetParticleCapacity(tier);
            if (targetCapacity == maxCapacity)
            {
                return;
            }

            maxCapacity = targetCapacity;
            ReleaseBuffers();
            InitializeBuffers();
            CacheKernels();
        }

        public bool TryGetCanvasHitBuffer(out ComputeBuffer buffer, out uint count)
        {
            buffer = _bufferCanvasHits;
            count = _lastCanvasHitCount;
            return buffer != null;
        }

        public void SetBucketTransform(Transform transform) => bucketTransform = transform;

        public void SetBucketKinematicProvider(MonoBehaviour provider) => bucketKinematicProvider = provider;

        public bool TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count)
        {
            buffer = _pingPong?.ReadBuffer;
            count = _cachedInternalCount;
            return buffer != null;
        }

        public bool TryGetFallingParticleBuffer(out ComputeBuffer buffer, out uint count)
        {
            if (worldFallingOnly && _pingPong != null)
            {
                buffer = _pingPong.ReadBuffer;
                count = _cachedInternalCount;
                return buffer != null;
            }

            buffer = _bufferFallingWorld ?? _bufferFalling;
            count = _lastFallingDebugCount;
            return buffer != null;
        }

        private int _kernelArgSetup;
        private int _kernelGridClear;
        private int _kernelGridGenerate;
        private int _kernelGridBitonic;
        private int _kernelGridBuildRanges;
        private int _kernelDensity;
        private int _kernelIntegration;
        private int _kernelQuantize;
        private int _kernelFallingWorld;
        private int _kernelDragClear;
        private int _kernelDragAdvect;
        private int _kernelDragScatter;
        private int _kernelDragApply;

        private uint _cachedInternalCount;
        private uint _lastFallingQuantizeCount;
        private uint _lastFallingDebugCount;
        private uint _lastCanvasHitCount;

        private readonly uint[] _activeCountCpu = new uint[1];
        private FluidParticle[] _seedParticles;

        private uint _lastLoggedActive;
        private bool _hasLoggedActive;
        private int _framesSincePositionSample;
        private FluidParticle[] _diagSampleBuffer;

        private static readonly int IndirectArgsBufferId = Shader.PropertyToID("_IndirectArgsBuffer");
        private static readonly int CellStartEndBufferId = Shader.PropertyToID("_CellStartEndBuffer");
        private static readonly int PaddedGridSizeId = Shader.PropertyToID("_PaddedGridSize");
        private static readonly int ActiveParticleCountId = Shader.PropertyToID("_ActiveParticleCount");
        private static readonly int GridResolutionId = Shader.PropertyToID("_GridResolution");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        private static readonly int BitonicLevelId = Shader.PropertyToID("_BitonicLevel");
        private static readonly int BitonicLevelMaskId = Shader.PropertyToID("_BitonicLevelMask");
        private static readonly int GridKeyValueBufferId = Shader.PropertyToID("_GridKeyValueBuffer");
        private static readonly int BitonicWidthId = Shader.PropertyToID("_BitonicWidth");
        private static readonly int ReadOnlyParticleSourceId = Shader.PropertyToID("_ReadOnlyParticleSource");
        private static readonly int SortedGridKeyValueBufferId = Shader.PropertyToID("_SortedGridKeyValueBuffer");
        private static readonly int DensityWritableCacheId = Shader.PropertyToID("_DensityWritableCache");
        private static readonly int InternalAppendId = Shader.PropertyToID("_InternalAppend");
        private static readonly int FallingAppendId = Shader.PropertyToID("_FallingAppend");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int SourceParticleBufferId = Shader.PropertyToID("_SourceParticleBuffer");
        private static readonly int QuantizedOutputBufferId = Shader.PropertyToID("_QuantizedOutputBuffer");
        private static readonly int QuantizedCountId = Shader.PropertyToID("_QuantizedCount");
        private static readonly int QuantizationOriginId = Shader.PropertyToID("_QuantizationOrigin");
        private static readonly int ReadOnlyParticlePositionsId = Shader.PropertyToID("_ReadOnlyParticlePositions");
        private static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
        private static readonly int ParticleMassId = Shader.PropertyToID("_ParticleMass");
        private static readonly int GasConstantKId = Shader.PropertyToID("_GasConstantK");
        private static readonly int RestDensityId = Shader.PropertyToID("_RestDensity");
        private static readonly int ViscosityId = Shader.PropertyToID("_Viscosity");
        private static readonly int GravityId = Shader.PropertyToID("_Gravity");
        private static readonly int AngularVelocityWorldId = Shader.PropertyToID("_AngularVelocityWorld");
        private static readonly int AngularAccelerationWorldId = Shader.PropertyToID("_AngularAccelerationWorld");
        private static readonly int NozzlePlaneLocalYId = Shader.PropertyToID("_NozzlePlaneLocalY");
        private static readonly int NozzleRadiusId = Shader.PropertyToID("_NozzleRadius");
        private static readonly int BucketRimLocalYId = Shader.PropertyToID("_BucketRimLocalY");
        private static readonly int LocalToWorldMatrixId = Shader.PropertyToID("_LocalToWorldMatrix");
        private static readonly int InstantaneousBucketGlobalVelocityId = Shader.PropertyToID("_InstantaneousBucketGlobalVelocity");
        private static readonly int FallingReadId = Shader.PropertyToID("_FallingRead");
        private static readonly int FallingCountId = Shader.PropertyToID("_FallingCount");
        private static readonly int WorldGravityId = Shader.PropertyToID("_WorldGravity");
        private static readonly int WorldDragId = Shader.PropertyToID("_WorldDrag");
        private static readonly int CanvasPlaneYId = Shader.PropertyToID("_CanvasPlaneY");
        private static readonly int CanvasCullingEnabledId = Shader.PropertyToID("_CanvasCullingEnabled");
        private static readonly int FloorRestitutionId = Shader.PropertyToID("_FloorRestitution");
        private static readonly int FloorFrictionId = Shader.PropertyToID("_FloorFriction");
        private static readonly int DragGridId = Shader.PropertyToID("_DragGrid");
        private static readonly int ParticleSourceId = Shader.PropertyToID("_ParticleSource");
        private static readonly int ParticleTargetId = Shader.PropertyToID("_ParticleTarget");
        private static readonly int GridVolumeId = Shader.PropertyToID("_GridVolume");
        private static readonly int DragStrengthId = Shader.PropertyToID("_DragStrength");
        private static readonly int DragDecayId = Shader.PropertyToID("_DragDecay");
        private static readonly int AmbientWindStrengthId = Shader.PropertyToID("_AmbientWindStrength");
        private static readonly int CanvasHitAppendId = Shader.PropertyToID("_CanvasHitAppend");

        private void Awake()
        {
            InitializeBuffers();
            CacheKernels();
            _lastBucketPosition = GetBucketPosition();
        }

        private void Start()
        {
            if (seedTestParticlesOnStart && !useExternalParticleIngestion)
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

        public void EnableExternalIngestion(bool enabled)
        {
            useExternalParticleIngestion = enabled;
            if (enabled)
            {
                seedTestParticlesOnStart = false;
            }
        }

        public int AppendParticles(FluidParticle[] particles, int count)
        {
            int appended = _bufferService?.AppendParticles(particles, count) ?? 0;
            if (appended > 0)
            {
                PublishDiagnostic(
                    HarmonicDiagnosticEventType.ParticlesAppended,
                    "PIPELINE",
                    $"appended={appended} total={GetActiveParticleCount()}",
                    intArg0: appended,
                    intArg1: count);
            }

            return appended;
        }

        public uint GetActiveParticleCount() => _bufferService?.GetActiveCount() ?? 0;

        public void ClearAllParticles()
        {
            _bufferService?.ClearAll();
            PublishDiagnostic(HarmonicDiagnosticEventType.ParticlesCleared, "PIPELINE", "cleared");
        }

        public void SetSimulationActive(bool active)
        {
            simulationActive = active;
            PublishDiagnostic(
                HarmonicDiagnosticEventType.SimulationStateChanged,
                "PIPELINE",
                active ? "active" : "inactive",
                boolArg0: active);
        }

        public bool IsSimulationActive => simulationActive;

        public void ExecutePipelineFrame(float deltaTime)
        {
            if (!simulationActive || simulationMode == HarmonicSimulationMode.BakePlayback || !AreShadersReady())
            {
                return;
            }

            _pingPong.BeginFrame();
            _bufferFalling.SetCounterValue(0);
            _bufferFallingWorld?.SetCounterValue(0);
            _bufferCanvasHits?.SetCounterValue(0);
            _lastCanvasHitCount = 0;

            uint activeCount = FetchActiveCount(_pingPong.ReadBuffer);
            _cachedInternalCount = activeCount;
            if (activeCount == 0)
            {
                _lastFallingQuantizeCount = 0;
                _lastFallingDebugCount = 0;
                _pingPong.Swap();
                PublishPipelineFrameDiagnostic(0);
                return;
            }

            if (worldFallingOnly)
            {
                ExecuteWorldFallingOnlyFrame(activeCount, deltaTime);
                PublishPipelineFrameDiagnostic(_cachedInternalCount);
                return;
            }

            float smoothingRadius = sphSolver.SmoothingRadius(cellSize);
            Matrix4x4 localToWorld = GetLocalToWorldMatrix();
            Vector3 bucketVelocity = GetBucketVelocity(deltaTime);
            ResolveAngularKinematics(out Vector3 angularVelocityWorld, out Vector3 angularAccelerationWorld);

            ComputeBuffer particleSourceForSph = _pingPong.ReadBuffer;
            if (enableEulerianDrag && eulerianDragGridShader != null && _bufferDragGrid != null)
            {
                RunEulerianDragPass(activeCount, deltaTime);
                particleSourceForSph = _bufferDragParticleScratch;
            }

            ComputeBuffer.CopyCount(_pingPong.ReadBuffer, _indirectArgsBuffer, sizeof(int) * 3);
            argumentUtilityShader.SetBuffer(_kernelArgSetup, IndirectArgsBufferId, _indirectArgsBuffer);
            argumentUtilityShader.Dispatch(_kernelArgSetup, 1, 1, 1);

            spatialHashGridShader.SetBuffer(_kernelGridClear, CellStartEndBufferId, _cellStartEndBuffer);
            spatialHashGridShader.SetInt(PaddedGridSizeId, _paddedSortSize);
            spatialHashGridShader.SetInt(GridResolutionId, _paddedSortSize);
            int clearGroups = Mathf.CeilToInt(_paddedSortSize / 256f);
            spatialHashGridShader.Dispatch(_kernelGridClear, clearGroups, 1, 1);

            spatialHashGridShader.SetBuffer(_kernelGridGenerate, GridKeyValueBufferId, _gridKeyValueBuffer);
            spatialHashGridShader.SetBuffer(_kernelGridGenerate, ReadOnlyParticlePositionsId, _pingPong.ReadBuffer);
            spatialHashGridShader.SetInt(PaddedGridSizeId, _paddedSortSize);
            spatialHashGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
            spatialHashGridShader.SetInt(GridResolutionId, _paddedSortSize);
            spatialHashGridShader.SetFloat(CellSizeId, cellSize);
            spatialHashGridShader.DispatchIndirect(_kernelGridGenerate, _indirectArgsBuffer, 0);

            int bitonicGroups = Mathf.CeilToInt(_paddedSortSize / 256f);
            for (int level = 2; level <= _paddedSortSize; level <<= 1)
            {
                for (int levelMask = level >> 1; levelMask > 0; levelMask >>= 1)
                {
                    spatialHashGridShader.SetBuffer(_kernelGridBitonic, GridKeyValueBufferId, _gridKeyValueBuffer);
                    spatialHashGridShader.SetInt(PaddedGridSizeId, _paddedSortSize);
                    spatialHashGridShader.SetInt(BitonicLevelId, level);
                    spatialHashGridShader.SetInt(BitonicLevelMaskId, levelMask);
                    spatialHashGridShader.SetInt(BitonicWidthId, _paddedSortSize);
                    spatialHashGridShader.Dispatch(_kernelGridBitonic, bitonicGroups, 1, 1);
                }
            }

            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, GridKeyValueBufferId, _gridKeyValueBuffer);
            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, CellStartEndBufferId, _cellStartEndBuffer);
            spatialHashGridShader.SetInt(PaddedGridSizeId, _paddedSortSize);
            spatialHashGridShader.Dispatch(_kernelGridBuildRanges, Mathf.CeilToInt(_paddedSortSize / 64f), 1, 1);

            ApplySphUniforms(streamCompactionShader, smoothingRadius, localToWorld, bucketVelocity, angularVelocityWorld, angularAccelerationWorld);
            streamCompactionShader.SetBuffer(_kernelDensity, ReadOnlyParticleSourceId, particleSourceForSph);
            streamCompactionShader.SetBuffer(_kernelDensity, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
            streamCompactionShader.SetBuffer(_kernelDensity, CellStartEndBufferId, _cellStartEndBuffer);
            streamCompactionShader.SetBuffer(_kernelDensity, DensityWritableCacheId, _bufferDensityCache);
            streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
            streamCompactionShader.DispatchIndirect(_kernelDensity, _indirectArgsBuffer, 0);

            ApplySphUniforms(streamCompactionShader, smoothingRadius, localToWorld, bucketVelocity, angularVelocityWorld, angularAccelerationWorld);
            streamCompactionShader.SetBuffer(_kernelIntegration, DensityWritableCacheId, _bufferDensityCache);
            streamCompactionShader.SetBuffer(_kernelIntegration, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
            streamCompactionShader.SetBuffer(_kernelIntegration, CellStartEndBufferId, _cellStartEndBuffer);
            streamCompactionShader.SetBuffer(_kernelIntegration, InternalAppendId, _pingPong.WriteBuffer);
            streamCompactionShader.SetBuffer(_kernelIntegration, FallingAppendId, _bufferFalling);
            streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
            streamCompactionShader.SetFloat(DeltaTimeId, deltaTime);
            streamCompactionShader.DispatchIndirect(_kernelIntegration, _indirectArgsBuffer, 0);

            uint fallingCount = FetchActiveCount(_bufferFalling);
            ComputeBuffer quantizeSource = _bufferFalling;
            _lastFallingDebugCount = fallingCount;

            if (fallingCount > 0 && fallingFluidWorldShader != null)
            {
                _bufferFallingWorld.SetCounterValue(0);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingReadId, _bufferFalling);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingAppendId, _bufferFallingWorld);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, CanvasHitAppendId, _bufferCanvasHits);
                fallingFluidWorldShader.SetInt(FallingCountId, (int)fallingCount);
                fallingFluidWorldShader.SetFloat(DeltaTimeId, deltaTime);
                fallingFluidWorldShader.SetVector(WorldGravityId, gravity);
                fallingFluidWorldShader.SetFloat(WorldDragId, worldDrag);
                fallingFluidWorldShader.SetFloat(CanvasPlaneYId, canvasPlaneY);
                fallingFluidWorldShader.SetInt(CanvasCullingEnabledId, canvasCullingEnabled ? 1 : 0);
                fallingFluidWorldShader.SetFloat(FloorRestitutionId, floorRestitution);
                fallingFluidWorldShader.SetFloat(FloorFrictionId, floorFriction);
                int fallingGroups = Mathf.CeilToInt(fallingCount / 64f);
                fallingFluidWorldShader.Dispatch(_kernelFallingWorld, fallingGroups, 1, 1);

                _lastCanvasHitCount = FetchActiveCount(_bufferCanvasHits);
                fallingCount = FetchActiveCount(_bufferFallingWorld);
                quantizeSource = _bufferFallingWorld;
                _lastFallingDebugCount = fallingCount;
            }

            _lastFallingQuantizeCount = fallingCount;
            if (fallingCount == 0)
            {
                _lastBucketPosition = GetBucketPosition();
                _pingPong.Swap();
                return;
            }

            ComputeBuffer.CopyCount(quantizeSource, _indirectArgsBuffer, sizeof(int) * 3);
            argumentUtilityShader.SetBuffer(_kernelArgSetup, IndirectArgsBufferId, _indirectArgsBuffer);
            argumentUtilityShader.Dispatch(_kernelArgSetup, 1, 1, 1);

            dataCompactionShader.SetBuffer(_kernelQuantize, SourceParticleBufferId, quantizeSource);
            dataCompactionShader.SetBuffer(_kernelQuantize, QuantizedOutputBufferId, _quantizedBakeBuffer);
            dataCompactionShader.SetVector(QuantizationOriginId, GetBucketPosition());
            dataCompactionShader.SetInt(QuantizedCountId, (int)fallingCount);
            dataCompactionShader.DispatchIndirect(_kernelQuantize, _indirectArgsBuffer, 0);

            _lastBucketPosition = GetBucketPosition();
            _pingPong.Swap();
            PublishPipelineFrameDiagnostic(_cachedInternalCount);
        }

        private void ExecuteWorldFallingOnlyFrame(uint activeCount, float deltaTime)
        {
            _lastFallingDebugCount = activeCount;
            _lastFallingQuantizeCount = activeCount;

            if (fallingFluidWorldShader == null)
            {
                _pingPong.Swap();
                return;
            }

            _pingPong.WriteBuffer.SetCounterValue(0);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingReadId, _pingPong.ReadBuffer);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingAppendId, _pingPong.WriteBuffer);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, CanvasHitAppendId, _bufferCanvasHits);
            fallingFluidWorldShader.SetInt(FallingCountId, (int)activeCount);
            fallingFluidWorldShader.SetFloat(DeltaTimeId, deltaTime);
            fallingFluidWorldShader.SetVector(WorldGravityId, gravity);
            fallingFluidWorldShader.SetFloat(WorldDragId, worldDrag);
            fallingFluidWorldShader.SetFloat(CanvasPlaneYId, canvasPlaneY);
            fallingFluidWorldShader.SetInt(CanvasCullingEnabledId, canvasCullingEnabled ? 1 : 0);
            fallingFluidWorldShader.SetFloat(FloorRestitutionId, floorRestitution);
            fallingFluidWorldShader.SetFloat(FloorFrictionId, floorFriction);
            int groups = Mathf.CeilToInt(activeCount / 64f);
            fallingFluidWorldShader.Dispatch(_kernelFallingWorld, groups, 1, 1);

            _lastCanvasHitCount = FetchActiveCount(_bufferCanvasHits);
            _cachedInternalCount = FetchActiveCount(_pingPong.WriteBuffer);
            _lastFallingDebugCount = _cachedInternalCount;
            _lastFallingQuantizeCount = _cachedInternalCount;
            _pingPong.Swap();

            if (verbosePipelineDiagnostics)
            {
                int lost = (int)activeCount - (int)_cachedInternalCount - (int)_lastCanvasHitCount;
                PublishStageDiagnostic(
                    "worldFalling",
                    $"in={activeCount} survived={_cachedInternalCount} canvasHits={_lastCanvasHitCount} lost={lost} " +
                    $"culling={canvasCullingEnabled} planeY={canvasPlaneY:F2} dt={deltaTime:F4}");
            }

            // GPU read-back sample on the surviving (post-swap read) buffer to confirm real motion.
            MaybeSampleParticlePositions(_pingPong.ReadBuffer, _cachedInternalCount, "worldFalling");
        }

        private void ApplySphUniforms(
            ComputeShader shader,
            float smoothingRadius,
            Matrix4x4 localToWorld,
            Vector3 bucketVelocity,
            Vector3 angularVelocityWorld,
            Vector3 angularAccelerationWorld)
        {
            shader.SetInt(GridResolutionId, _paddedSortSize);
            shader.SetFloat(CellSizeId, cellSize);
            shader.SetFloat(SmoothingRadiusId, smoothingRadius);
            shader.SetFloat(ParticleMassId, sphSolver.ParticleMass);
            shader.SetFloat(GasConstantKId, sphSolver.GasConstantK);
            shader.SetFloat(RestDensityId, sphSolver.RestDensity);
            shader.SetFloat(ViscosityId, sphSolver.Viscosity);
            shader.SetVector(GravityId, gravity);
            shader.SetVector(AngularVelocityWorldId, angularVelocityWorld);
            shader.SetVector(AngularAccelerationWorldId, angularAccelerationWorld);
            shader.SetFloat(NozzlePlaneLocalYId, nozzlePlaneLocalY);
            shader.SetFloat(NozzleRadiusId, nozzleRadius);
            shader.SetFloat(BucketRimLocalYId, bucketRimLocalY);
            shader.SetMatrix(LocalToWorldMatrixId, localToWorld);
            shader.SetVector(InstantaneousBucketGlobalVelocityId, bucketVelocity);
        }

        private void ResolveAngularKinematics(out Vector3 angularVelocityWorld, out Vector3 angularAccelerationWorld)
        {
            if (!applyNonInertialPseudoForces || bucketKinematicProvider is not IBucketKinematicProvider provider)
            {
                angularVelocityWorld = Vector3.zero;
                angularAccelerationWorld = Vector3.zero;
                return;
            }

            angularVelocityWorld = provider.AngularVelocityWorld;
            angularAccelerationWorld = provider.AngularAccelerationWorld;
        }

        private void RunEulerianDragPass(uint activeCount, float deltaTime)
        {
            int gridVolume = Mathf.Max(1, dragGridVolume);
            int clearGroups = Mathf.CeilToInt(gridVolume / 256f);
            int applyGroups = Mathf.CeilToInt(activeCount / 64f);

            eulerianDragGridShader.SetBuffer(_kernelDragAdvect, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetFloat(DragDecayId, dragDecay);
            eulerianDragGridShader.SetFloat(AmbientWindStrengthId, ambientWindStrength);
            eulerianDragGridShader.SetFloat(DeltaTimeId, deltaTime);
            eulerianDragGridShader.Dispatch(_kernelDragAdvect, clearGroups, 1, 1);

            eulerianDragGridShader.SetBuffer(_kernelDragScatter, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetBuffer(_kernelDragScatter, ParticleSourceId, _pingPong.ReadBuffer);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
            eulerianDragGridShader.Dispatch(_kernelDragScatter, applyGroups, 1, 1);

            eulerianDragGridShader.SetBuffer(_kernelDragApply, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetBuffer(_kernelDragApply, ParticleSourceId, _pingPong.ReadBuffer);
            eulerianDragGridShader.SetBuffer(_kernelDragApply, ParticleTargetId, _bufferDragParticleScratch);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
            eulerianDragGridShader.SetFloat(DragStrengthId, dragStrength);
            eulerianDragGridShader.SetFloat(DeltaTimeId, deltaTime);
            eulerianDragGridShader.Dispatch(_kernelDragApply, applyGroups, 1, 1);
        }

        private Vector3 GetBucketVelocity(float deltaTime)
        {
            if (bucketKinematicProvider is IBucketKinematicProvider provider)
            {
                return provider.BucketWorldVelocity;
            }

            if (deltaTime <= 1e-6f)
            {
                return Vector3.zero;
            }

            Vector3 current = GetBucketPosition();
            return (current - _lastBucketPosition) / deltaTime;
        }

        private Matrix4x4 GetLocalToWorldMatrix()
        {
            Transform t = driveBucketFromTransform && bucketTransform != null ? bucketTransform : transform;
            return t.localToWorldMatrix;
        }

        private Vector3 GetBucketPosition()
        {
            Transform t = driveBucketFromTransform && bucketTransform != null ? bucketTransform : transform;
            return t.position;
        }

        private void SeedTestParticlesIfEmpty()
        {
            uint count = FetchActiveCount(_bufferInternalA);
            if (count > 0)
            {
                return;
            }

            int spawnCount = Mathf.Clamp(testParticleCount, 1, maxCapacity);
            _seedParticles ??= new FluidParticle[spawnCount];
            if (_seedParticles.Length < spawnCount)
            {
                _seedParticles = new FluidParticle[spawnCount];
            }

            var rng = new Unity.Mathematics.Random(0xC0FFEEu);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 offset = rng.NextFloat3Direction() * rng.NextFloat(0f, testSpawnRadius);
                _seedParticles[i] = new FluidParticle
                {
                    Position = offset,
                    Velocity = float3.zero,
                    Density = sphSolver.RestDensity,
                    Pressure = 0f
                };
            }

            _bufferInternalA.SetData(_seedParticles, 0, 0, spawnCount);
            _bufferInternalA.SetCounterValue((uint)spawnCount);
            _bufferInternalB.SetCounterValue(0);
        }

        private bool AreShadersReady()
        {
            return argumentUtilityShader != null
                && spatialHashGridShader != null
                && streamCompactionShader != null
                && dataCompactionShader != null;
        }

        private void InitializeBuffers()
        {
            int particleStride = sizeof(float) * 8;
            int keyStride = sizeof(uint) * 2;
            int cellStride = sizeof(int) * 2;
            int quantizedStride = sizeof(ushort) * 8;

            _bufferInternalA = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Append);
            _bufferInternalB = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Append);
            _bufferFalling = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Append);
            _bufferFallingWorld = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Append);
            _bufferDensityCache = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Structured);
            _bufferDragParticleScratch = new ComputeBuffer(maxCapacity, particleStride, ComputeBufferType.Structured);

            int dragVolume = Mathf.Max(1, dragGridVolume);
            int dragStride = sizeof(float) * 4;
            _bufferDragGrid = new ComputeBuffer(dragVolume, dragStride, ComputeBufferType.Structured);

            int canvasHitStride = sizeof(float) * 4;
            int canvasHitCapacity = Mathf.Max(1, maxCanvasHitsPerFrame);
            _bufferCanvasHits = new ComputeBuffer(canvasHitCapacity, canvasHitStride, ComputeBufferType.Append);

            _paddedSortSize = GPUIndirectSortBinder.CalculatePaddedSortSize(maxCapacity);
            _gridKeyValueBuffer = new ComputeBuffer(_paddedSortSize, keyStride, ComputeBufferType.Structured);
            _cellStartEndBuffer = new ComputeBuffer(_paddedSortSize, cellStride, ComputeBufferType.Structured);

            _indirectArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            _quantizedBakeBuffer = new ComputeBuffer(maxCapacity, quantizedStride, ComputeBufferType.Structured);
            _counterReadbackBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            _bufferInternalA.SetCounterValue(0);
            _bufferInternalB.SetCounterValue(0);
            _bufferFalling.SetCounterValue(0);
            _bufferFallingWorld.SetCounterValue(0);
            _bufferCanvasHits.SetCounterValue(0);

            _pingPong = new PingPongCounterManager(_bufferInternalA, _bufferInternalB);
            _bufferService = new HarmonicParticleBufferService(
                _bufferInternalA,
                _bufferInternalB,
                _pingPong,
                maxCapacity,
                _counterReadbackBuffer);
        }

        private void CacheKernels()
        {
            if (argumentUtilityShader != null)
            {
                _kernelArgSetup = argumentUtilityShader.FindKernel("CalculateGridArgsKernel");
            }

            if (spatialHashGridShader != null)
            {
                _kernelGridClear = spatialHashGridShader.FindKernel("ClearGridCellsKernel");
                _kernelGridGenerate = spatialHashGridShader.FindKernel("GenerateGridKeysKernel");
                _kernelGridBitonic = spatialHashGridShader.FindKernel("BitonicSortStepKernel");
                _kernelGridBuildRanges = spatialHashGridShader.FindKernel("BuildCellRangesKernel");
            }

            if (streamCompactionShader != null)
            {
                _kernelDensity = streamCompactionShader.FindKernel("ExecuteSphDensityPass");
                _kernelIntegration = streamCompactionShader.FindKernel("ExecuteInternalFluidIntegration");
            }

            if (dataCompactionShader != null)
            {
                _kernelQuantize = dataCompactionShader.FindKernel("QuantizeFallingParticlesKernel");
            }

            if (fallingFluidWorldShader != null)
            {
                _kernelFallingWorld = fallingFluidWorldShader.FindKernel("ExecuteFallingFluidIntegration");
            }

            if (eulerianDragGridShader != null)
            {
                _kernelDragClear = eulerianDragGridShader.FindKernel("ClearDragGridKernel");
                _kernelDragAdvect = eulerianDragGridShader.FindKernel("AdvectDragGridKernel");
                _kernelDragScatter = eulerianDragGridShader.FindKernel("ScatterParticleToGridKernel");
                _kernelDragApply = eulerianDragGridShader.FindKernel("ApplyDragFromGridKernel");
            }
        }

        /// <summary>
        /// Test/runtime bootstrap for Play Mode and integration tests.
        /// </summary>
        public void ConfigureAndInitialize(
            ComputeShader argumentShader,
            ComputeShader spatialShader,
            ComputeShader streamShader,
            ComputeShader dataShader,
            int capacity = 8192,
            bool externalIngestion = true,
            bool autoRun = false,
            ComputeShader fallingShader = null,
            ComputeShader eulerianShader = null)
        {
            ReleaseBuffers();
            argumentUtilityShader = argumentShader;
            spatialHashGridShader = spatialShader;
            streamCompactionShader = streamShader;
            dataCompactionShader = dataShader;
            fallingFluidWorldShader = fallingShader;
            eulerianDragGridShader = eulerianShader;
            enableEulerianDrag = eulerianShader != null;
            maxCapacity = capacity;
            useExternalParticleIngestion = externalIngestion;
            seedTestParticlesOnStart = false;
            autoRunPipeline = autoRun;
            InitializeBuffers();
            CacheKernels();
            _lastBucketPosition = GetBucketPosition();
        }

        private static void PublishDiagnostic(
            HarmonicDiagnosticEventType type,
            string category,
            string message,
            int intArg0 = 0,
            int intArg1 = 0,
            bool boolArg0 = false)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            uint active = session.ReadActiveParticleCount();
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                type,
                category,
                message,
                session.FrameIndex,
                session.ElapsedSeconds,
                active,
                canvasHitCount: 0,
                intArg0: intArg0,
                intArg1: intArg1,
                boolArg0: boolArg0));
        }

        private void PublishPipelineFrameDiagnostic(uint activeCount)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            // Throttle: always log on count change or canvas activity; otherwise only every N frames.
            bool countChanged = !_hasLoggedActive || activeCount != _lastLoggedActive;
            bool periodic = frameDiagnosticInterval <= 1
                || (HarmonicDiagnosticHub.Session.FrameIndex % frameDiagnosticInterval) == 0;
            if (!countChanged && _lastCanvasHitCount == 0 && !periodic)
            {
                return;
            }

            _lastLoggedActive = activeCount;
            _hasLoggedActive = true;

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineFrameAfter,
                "PIPELINE",
                $"active={activeCount} canvasHits={_lastCanvasHitCount} worldOnly={worldFallingOnly} culling={canvasCullingEnabled}",
                session.FrameIndex,
                session.ElapsedSeconds,
                activeCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        private void PublishStageDiagnostic(string stage, string detail)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "ENGINE",
                $"stage={stage} {detail}",
                session.FrameIndex,
                session.ElapsedSeconds,
                _cachedInternalCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        /// <summary>
        /// Reads a small sample of particles back from the GPU on an interval and logs their
        /// position/velocity range. This proves the engine is actually integrating motion
        /// (independent of any rendering), so we can rule out the engine when debugging.
        /// </summary>
        private void MaybeSampleParticlePositions(ComputeBuffer buffer, uint activeCount, string stage)
        {
            if (positionSampleInterval <= 0 || buffer == null || activeCount == 0)
            {
                return;
            }

            if (++_framesSincePositionSample < positionSampleInterval)
            {
                return;
            }

            _framesSincePositionSample = 0;
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            int sampleCount = Mathf.Min(positionSampleCount, (int)activeCount);
            if (sampleCount <= 0)
            {
                return;
            }

            if (_diagSampleBuffer == null || _diagSampleBuffer.Length < sampleCount)
            {
                _diagSampleBuffer = new FluidParticle[Mathf.Max(sampleCount, positionSampleCount)];
            }

            buffer.GetData(_diagSampleBuffer, 0, 0, sampleCount);

            float minY = float.MaxValue, maxY = float.MinValue, sumY = 0f, sumSpeed = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float y = _diagSampleBuffer[i].Position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                sumY += y;
                sumSpeed += math.length(_diagSampleBuffer[i].Velocity);
            }

            float3 p0 = _diagSampleBuffer[0].Position;
            PublishStageDiagnostic(
                $"{stage}.sample",
                $"n={sampleCount} minY={minY:F2} maxY={maxY:F2} avgY={(sumY / sampleCount):F2} " +
                $"avgSpeed={(sumSpeed / sampleCount):F2} p0=({p0.x:F2},{p0.y:F2},{p0.z:F2})");
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            _bufferInternalA?.Release();
            _bufferInternalB?.Release();
            _bufferFalling?.Release();
            _bufferFallingWorld?.Release();
            _bufferDragGrid?.Release();
            _bufferDragParticleScratch?.Release();
            _bufferDensityCache?.Release();
            _gridKeyValueBuffer?.Release();
            _cellStartEndBuffer?.Release();
            _indirectArgsBuffer?.Release();
            _quantizedBakeBuffer?.Release();
            _counterReadbackBuffer?.Release();
            _bufferCanvasHits?.Release();
            _bufferInternalA = null;
            _bufferInternalB = null;
            _bufferFalling = null;
            _bufferFallingWorld = null;
            _bufferDragGrid = null;
            _bufferDragParticleScratch = null;
            _bufferDensityCache = null;
            _gridKeyValueBuffer = null;
            _cellStartEndBuffer = null;
            _indirectArgsBuffer = null;
            _quantizedBakeBuffer = null;
            _counterReadbackBuffer = null;
            _bufferCanvasHits = null;
        }

        private uint FetchActiveCount(ComputeBuffer source)
        {
            ComputeBuffer.CopyCount(source, _counterReadbackBuffer, 0);
            _counterReadbackBuffer.GetData(_activeCountCpu);
            return _activeCountCpu[0];
        }
    }
}
