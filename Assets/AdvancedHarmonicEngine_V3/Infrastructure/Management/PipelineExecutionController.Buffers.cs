using HarmonicEngine.Core.DataStructures;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
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
        private int _frameSortSize;
        private bool _capacityClampWarningLogged;
        private readonly uint[] _activeCountCpu = new uint[1];

        private int _kernelArgSetup;
        private int _kernelGridClear;
        private int _kernelGridGenerate;
        private int _kernelGridBitonic;
        private int _kernelGridBuildRanges;
        private int _kernelDensity;
        private int _kernelIntegration;
        private int _kernelContainerIntegration;
        private int _kernelQuantize;
        private int _kernelFallingWorld;
        private int _kernelDragClear;
        private int _kernelDragAdvect;
        private int _kernelDragScatter;
        private int _kernelDragApply;

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
        private static readonly int StiffnessBId = Shader.PropertyToID("_StiffnessB");
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
        private static readonly int ContainerCenterId = Shader.PropertyToID("_ContainerCenter");
        private static readonly int ContainerRadiusId = Shader.PropertyToID("_ContainerRadius");
        private static readonly int ContainerFloorYId = Shader.PropertyToID("_ContainerFloorY");
        private static readonly int ContainerRimYId = Shader.PropertyToID("_ContainerRimY");
        private static readonly int ContainerRestitutionId = Shader.PropertyToID("_ContainerRestitution");
        private static readonly int ContainerFrictionId = Shader.PropertyToID("_ContainerFriction");
        private static readonly int ContainerWallStiffnessId = Shader.PropertyToID("_ContainerWallStiffness");
        private static readonly int ContainerDampingId = Shader.PropertyToID("_ContainerDamping");
        private static readonly int ContainerMaxSpeedId = Shader.PropertyToID("_ContainerMaxSpeed");
        private static readonly int ColorDiffusionRateId = Shader.PropertyToID("_ColorDiffusionRate");
        private static readonly int MaxParticleCountId = Shader.PropertyToID("_MaxParticleCount");
        private static readonly int CanvasPaintAbsorbEnabledId = Shader.PropertyToID("_CanvasPaintAbsorbEnabled");
        private static readonly int CanvasAbsorbRateId = Shader.PropertyToID("_CanvasAbsorbRate");
        private static readonly int CanvasAbsorbPaintWeightScaleId = Shader.PropertyToID("_CanvasAbsorbPaintWeightScale");

        private static readonly ProfilerMarker MarkerGrid = new("Harmonic.SpatialHashGrid");
        private static readonly ProfilerMarker MarkerSort = new("Harmonic.BitonicSort");
        private static readonly ProfilerMarker MarkerBuildRanges = new("Harmonic.BuildRanges");

        private bool AreShadersReady()
        {
            return argumentUtilityShader != null
                && spatialHashGridShader != null
                && streamCompactionShader != null
                && dataCompactionShader != null;
        }

        private void InitializeBuffers()
        {
            int particleStride = sizeof(float) * 12; // 48-byte FluidParticle (incl. packed color + padding)
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

            int canvasHitStride = sizeof(float) * 8; // 32-byte CanvasPaintHit
            int canvasHitCapacity = Mathf.Max(1, maxCanvasHitsPerFrame);
            _bufferCanvasHits = new ComputeBuffer(canvasHitCapacity, canvasHitStride, ComputeBufferType.Append);

            _paddedSortSize = HarmonicEngineLimits.SortGridSizeForCapacity(maxCapacity);
            _frameSortSize = _paddedSortSize;
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
                _kernelContainerIntegration = streamCompactionShader.FindKernel("ExecuteContainerFluidIntegration");
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

        private void ComputeFrameSortSize(uint activeCount)
        {
            if (!dynamicSortSizing)
            {
                _frameSortSize = _paddedSortSize;
                return;
            }

            int desired = GPUIndirectSortBinder.CalculatePaddedSortSize((int)activeCount);
            int floor = Mathf.NextPowerOfTwo(Mathf.Max(2, minSortSize));
            _frameSortSize = Mathf.Clamp(desired, floor, _paddedSortSize);
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

        private void DispatchIndirectArgsSetup()
        {
            argumentUtilityShader.SetInt(MaxParticleCountId, maxCapacity);
            argumentUtilityShader.SetBuffer(_kernelArgSetup, IndirectArgsBufferId, _indirectArgsBuffer);
            argumentUtilityShader.Dispatch(_kernelArgSetup, 1, 1, 1);
        }

        private uint SanitizeCount(uint raw) => raw > (uint)maxCapacity ? (uint)maxCapacity : raw;

        /// <summary>
        /// Append counters can drift above the allocated buffer when a prior frame overflowed.
        /// Clamp for dispatch and repair the GPU counter so indirect args stay in bounds.
        /// </summary>
        private uint SanitizeAndRepairCount(ComputeBuffer source)
        {
            uint raw = FetchActiveCount(source);
            if (raw <= (uint)maxCapacity)
            {
                _capacityClampWarningLogged = false;
                return raw;
            }

            if (!_capacityClampWarningLogged)
            {
                _capacityClampWarningLogged = true;
                Debug.LogWarning(
                    $"[HarmonicPipeline] Active count {raw} exceeded maxCapacity {maxCapacity}; clamping. " +
                    "If this persists after a fix, use Clear All / restart Play.");
            }

            source.SetCounterValue((uint)maxCapacity);
            return (uint)maxCapacity;
        }
    }
}
