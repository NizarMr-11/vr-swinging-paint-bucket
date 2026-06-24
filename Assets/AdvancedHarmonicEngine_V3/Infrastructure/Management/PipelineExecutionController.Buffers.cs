using HarmonicEngine.Core.DataStructures;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private ParticleSoaBuffers _soaInternalA;
        private ParticleSoaBuffers _soaInternalB;
        private ParticleSoaBuffers _soaFalling;
        private ParticleSoaBuffers _soaFallingWorld;
        private ParticleSoaBuffers _soaDragScratch;
        private ComputeBuffer _bufferDensityCacheDensities;
        private ComputeBuffer _bufferDensityCachePressures;
        private ComputeBuffer _bufferFalling;
        private ComputeBuffer _bufferFallingWorld;
        private ComputeBuffer _bufferDragGrid;
        private ComputeBuffer _gridKeyValueBuffer;
        private ComputeBuffer _cellStartEndBuffer;
        private ComputeBuffer _sortKeysBuffer;
        private ComputeBuffer _sortTempKeysBuffer;
        private ComputeBuffer _sortValuesBuffer;
        private ComputeBuffer _sortTempValuesBuffer;
        private GpuRadixSort _gpuRadixSort;
        private ComputeBuffer _indirectArgsBuffer;
        private ComputeBuffer _quantizedBakeBuffer;
        private ComputeBuffer _counterReadbackBuffer;
        private ComputeBuffer _bufferCanvasHits;
        private PbfScratchBuffers _pbfScratch;

        private PingPongSoaManager _pingPong;
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
        private int _kernelPbfPredict;
        private int _kernelPbfDensity;
        private int _kernelPbfLambda;
        private int _kernelPbfSolve;
        private int _kernelPbfApply;

        private static readonly int IndirectArgsBufferId = Shader.PropertyToID("_IndirectArgsBuffer");
        private static readonly int CellStartEndBufferId = Shader.PropertyToID("_CellStartEndBuffer");
        private static readonly int PaddedGridSizeId = Shader.PropertyToID("_PaddedGridSize");
        private static readonly int ActiveParticleCountId = Shader.PropertyToID("_ActiveParticleCount");
        private static readonly int GridResolutionId = Shader.PropertyToID("_GridResolution");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
        private static readonly int BitonicLevelId = Shader.PropertyToID("_BitonicLevel");
        private static readonly int BitonicLevelMaskId = Shader.PropertyToID("_BitonicLevelMask");
        private static readonly int GridKeyValueBufferId = Shader.PropertyToID("_GridKeyValueBuffer");
        private static readonly int SortKeysId = Shader.PropertyToID("_SortKeys");
        private static readonly int SortValuesId = Shader.PropertyToID("_SortValues");
        private static readonly int BitonicWidthId = Shader.PropertyToID("_BitonicWidth");
        private static readonly int SortedGridKeyValueBufferId = Shader.PropertyToID("_SortedGridKeyValueBuffer");
        private static readonly int DensityCacheDensitiesId = Shader.PropertyToID("_DensityCacheDensities");
        private static readonly int DensityCachePressuresId = Shader.PropertyToID("_DensityCachePressures");
        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int Block1Id = Shader.PropertyToID("_Block1");
        private static readonly int PackedColorsId = Shader.PropertyToID("_PackedColors");
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
        private static readonly int WriteBlock0Id = Shader.PropertyToID("_WriteBlock0");
        private static readonly int WriteBlock1Id = Shader.PropertyToID("_WriteBlock1");
        private static readonly int WritePackedColorsId = Shader.PropertyToID("_WritePackedColors");
        private static readonly int WriteWetnessId = Shader.PropertyToID("_WriteWetness");
        private static readonly int InternalBlock0Id = Shader.PropertyToID("_InternalBlock0");
        private static readonly int InternalBlock1Id = Shader.PropertyToID("_InternalBlock1");
        private static readonly int InternalPackedColorsId = Shader.PropertyToID("_InternalPackedColors");
        private static readonly int InternalWetnessId = Shader.PropertyToID("_InternalWetness");
        private static readonly int FallingBlock0Id = Shader.PropertyToID("_FallingBlock0");
        private static readonly int FallingBlock1Id = Shader.PropertyToID("_FallingBlock1");
        private static readonly int FallingPackedColorsId = Shader.PropertyToID("_FallingPackedColors");
        private static readonly int FallingWetnessId = Shader.PropertyToID("_FallingWetness");
        private static readonly int FallingReadBlock0Id = Shader.PropertyToID("_FallingReadBlock0");
        private static readonly int FallingReadBlock1Id = Shader.PropertyToID("_FallingReadBlock1");
        private static readonly int FallingReadPackedColorsId = Shader.PropertyToID("_FallingReadPackedColors");
        private static readonly int FallingReadWetnessId = Shader.PropertyToID("_FallingReadWetness");
        private static readonly int FallingAppendBlock0Id = Shader.PropertyToID("_FallingAppendBlock0");
        private static readonly int FallingAppendBlock1Id = Shader.PropertyToID("_FallingAppendBlock1");
        private static readonly int FallingAppendPackedColorsId = Shader.PropertyToID("_FallingAppendPackedColors");
        private static readonly int FallingAppendWetnessId = Shader.PropertyToID("_FallingAppendWetness");
        private static readonly int TargetBlock0Id = Shader.PropertyToID("_TargetBlock0");
        private static readonly int TargetBlock1Id = Shader.PropertyToID("_TargetBlock1");
        private static readonly int TargetPackedColorsId = Shader.PropertyToID("_TargetPackedColors");
        private static readonly int TargetWetnessId = Shader.PropertyToID("_TargetWetness");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int QuantizedOutputBufferId = Shader.PropertyToID("_QuantizedOutputBuffer");
        private static readonly int QuantizedCountId = Shader.PropertyToID("_QuantizedCount");
        private static readonly int QuantizationOriginId = Shader.PropertyToID("_QuantizationOrigin");
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
        private static readonly int FallingCountId = Shader.PropertyToID("_FallingCount");
        private static readonly int WorldGravityId = Shader.PropertyToID("_WorldGravity");
        private static readonly int WorldDragId = Shader.PropertyToID("_WorldDrag");
        private static readonly int CanvasPlaneYId = Shader.PropertyToID("_CanvasPlaneY");
        private static readonly int CanvasCullingEnabledId = Shader.PropertyToID("_CanvasCullingEnabled");
        private static readonly int FloorRestitutionId = Shader.PropertyToID("_FloorRestitution");
        private static readonly int FloorFrictionId = Shader.PropertyToID("_FloorFriction");
        private static readonly int DragGridId = Shader.PropertyToID("_DragGrid");
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
        private static readonly int PredictedBlock0Id = Shader.PropertyToID("_PredictedBlock0");
        private static readonly int OldBlock0Id = Shader.PropertyToID("_OldBlock0");
        private static readonly int LambdasId = Shader.PropertyToID("_Lambdas");
        private static readonly int GradSqSumId = Shader.PropertyToID("_GradSqSum");
        private static readonly int PbfEpsilonId = Shader.PropertyToID("_PbfEpsilon");
        private static readonly int PbfRelaxationId = Shader.PropertyToID("_PbfRelaxation");
        private static readonly int PbfVelocityDampingId = Shader.PropertyToID("_PbfVelocityDamping");
        private static readonly int PbfMaxPositionDeltaId = Shader.PropertyToID("_PbfMaxPositionDelta");
        private static readonly int CohesionStrengthId = Shader.PropertyToID("_CohesionStrength");
        private static readonly int ContainerLocalToWorldId = Shader.PropertyToID("_ContainerLocalToWorld");
        private static readonly int ContainerWorldToLocalId = Shader.PropertyToID("_ContainerWorldToLocal");
        private static readonly int ContainerHeightId = Shader.PropertyToID("_ContainerHeight");
        private static readonly int ContainerSpillEnabledId = Shader.PropertyToID("_ContainerSpillEnabled");
        private static readonly int ContainerUsesOrientationId = Shader.PropertyToID("_ContainerUsesOrientation");
        private static readonly int ContainerRotationDeltaId = Shader.PropertyToID("_ContainerRotationDelta");
        private static readonly int ContainerAngularVelocityWorldId = Shader.PropertyToID("_ContainerAngularVelocityWorld");
        private static readonly int ContainerFloorPivotId = Shader.PropertyToID("_ContainerFloorPivot");

        private static readonly ProfilerMarker MarkerGrid = new("Harmonic.SpatialHashGrid");
        private static readonly ProfilerMarker MarkerSort = new("Harmonic.BitonicSort");
        private static readonly ProfilerMarker MarkerRadixSort = new("Harmonic.RadixSort");
        private static readonly ProfilerMarker MarkerBuildRanges = new("Harmonic.BuildRanges");

        private bool AreShadersReady()
        {
            if (argumentUtilityShader == null
                || spatialHashGridShader == null
                || dataCompactionShader == null)
            {
                return false;
            }

            if (useRadixSort && radixSortShader == null)
            {
                return false;
            }

            if (containerFluid.enabled && usePBF)
            {
                return pbfSolverShader != null;
            }

            return streamCompactionShader != null
                && streamCompactionIntegrateShader != null;
        }

        private void InitializeBuffers()
        {
            int keyStride = sizeof(uint) * 2;
            int cellStride = sizeof(int) * 2;
            int quantizedStride = sizeof(ushort) * 8;

            _soaInternalA = ParticleSoaBuffers.Create(maxCapacity, ComputeBufferType.Append);
            _soaInternalB = ParticleSoaBuffers.Create(maxCapacity, ComputeBufferType.Append);
            _soaFalling = ParticleSoaBuffers.Create(maxCapacity, ComputeBufferType.Append);
            _soaFallingWorld = ParticleSoaBuffers.Create(maxCapacity, ComputeBufferType.Append);
            _soaDragScratch = ParticleSoaBuffers.Create(maxCapacity, ComputeBufferType.Structured);

            _bufferDensityCacheDensities = new ComputeBuffer(maxCapacity, sizeof(float), ComputeBufferType.Structured);
            _bufferDensityCachePressures = new ComputeBuffer(maxCapacity, sizeof(float), ComputeBufferType.Structured);
            _pbfScratch = PbfScratchBuffers.Create(maxCapacity);

            int dragVolume = Mathf.Max(1, dragGridVolume);
            int dragStride = sizeof(float) * 4;
            _bufferDragGrid = new ComputeBuffer(dragVolume, dragStride, ComputeBufferType.Structured);

            int canvasHitStride = sizeof(float) * 8;
            int canvasHitCapacity = Mathf.Max(1, maxCanvasHitsPerFrame);
            _bufferCanvasHits = new ComputeBuffer(canvasHitCapacity, canvasHitStride, ComputeBufferType.Append);

            _paddedSortSize = HarmonicEngineLimits.SortGridSizeForCapacity(maxCapacity);
            _frameSortSize = _paddedSortSize;
            _gridKeyValueBuffer = new ComputeBuffer(_paddedSortSize, keyStride, ComputeBufferType.Structured);
            _cellStartEndBuffer = new ComputeBuffer(_paddedSortSize, cellStride, ComputeBufferType.Structured);
            _sortKeysBuffer = new ComputeBuffer(_paddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            _sortTempKeysBuffer = new ComputeBuffer(_paddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            _sortValuesBuffer = new ComputeBuffer(_paddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            _sortTempValuesBuffer = new ComputeBuffer(_paddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            _gpuRadixSort?.Release();
            _gpuRadixSort = radixSortShader != null
                ? new GpuRadixSort(radixSortShader, _paddedSortSize)
                : null;

            _indirectArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            _quantizedBakeBuffer = new ComputeBuffer(maxCapacity, quantizedStride, ComputeBufferType.Structured);
            _counterReadbackBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            _soaInternalA.SetCounterValue(0);
            _soaInternalB.SetCounterValue(0);
            _soaFalling.SetCounterValue(0);
            _soaFallingWorld.SetCounterValue(0);
            _bufferCanvasHits.SetCounterValue(0);

            _bufferFalling = _soaFalling.CounterBuffer;
            _bufferFallingWorld = _soaFallingWorld.CounterBuffer;

            _pingPong = new PingPongSoaManager(_soaInternalA, _soaInternalB);
            _bufferService = new HarmonicParticleBufferService(
                _soaInternalA,
                _soaInternalB,
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
            }

            if (streamCompactionIntegrateShader != null)
            {
                _kernelIntegration = streamCompactionIntegrateShader.FindKernel("ExecuteInternalFluidIntegration");
                _kernelContainerIntegration = streamCompactionIntegrateShader.FindKernel("ExecuteContainerFluidIntegration");
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

            if (pbfSolverShader != null)
            {
                _kernelPbfPredict = pbfSolverShader.FindKernel("PredictPositionsKernel");
                _kernelPbfDensity = pbfSolverShader.FindKernel("ComputeDensityKernel");
                _kernelPbfLambda = pbfSolverShader.FindKernel("ComputeLambdaKernel");
                _kernelPbfSolve = pbfSolverShader.FindKernel("SolvePositionsKernel");
                _kernelPbfApply = pbfSolverShader.FindKernel("ApplyPositionsKernel");
            }

            if (containerRigidCarryShader != null)
            {
                _kernelContainerRigidCarry = containerRigidCarryShader.FindKernel("ContainerRigidCarryKernel");
            }
            else
            {
                _kernelContainerRigidCarry = -1;
            }
        }

        public void ConfigureAndInitialize(
            ComputeShader argumentShader,
            ComputeShader spatialShader,
            ComputeShader streamShader,
            ComputeShader dataShader,
            int capacity = 8192,
            bool externalIngestion = true,
            bool autoRun = false,
            ComputeShader fallingShader = null,
            ComputeShader eulerianShader = null,
            ComputeShader integrateShader = null,
            ComputeShader pbfShader = null,
            ComputeShader radixShader = null)
        {
            ReleaseBuffers();
            argumentUtilityShader = argumentShader;
            spatialHashGridShader = spatialShader;
            radixSortShader = radixShader ?? radixSortShader;
            streamCompactionShader = streamShader;
            streamCompactionIntegrateShader = integrateShader ?? streamShader;
            pbfSolverShader = pbfShader ?? pbfSolverShader;
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
            _soaInternalA?.Release();
            _soaInternalB?.Release();
            _soaFalling?.Release();
            _soaFallingWorld?.Release();
            _soaDragScratch?.Release();
            _bufferDensityCacheDensities?.Release();
            _bufferDensityCachePressures?.Release();
            _bufferDragGrid?.Release();
            _gridKeyValueBuffer?.Release();
            _cellStartEndBuffer?.Release();
            _sortKeysBuffer?.Release();
            _sortTempKeysBuffer?.Release();
            _sortValuesBuffer?.Release();
            _sortTempValuesBuffer?.Release();
            _gpuRadixSort?.Release();
            _indirectArgsBuffer?.Release();
            _quantizedBakeBuffer?.Release();
            _counterReadbackBuffer?.Release();
            _bufferCanvasHits?.Release();
            _pbfScratch?.Release();
            _soaInternalA = null;
            _soaInternalB = null;
            _soaFalling = null;
            _soaFallingWorld = null;
            _soaDragScratch = null;
            _bufferDensityCacheDensities = null;
            _bufferDensityCachePressures = null;
            _bufferFalling = null;
            _bufferFallingWorld = null;
            _bufferDragGrid = null;
            _gridKeyValueBuffer = null;
            _cellStartEndBuffer = null;
            _sortKeysBuffer = null;
            _sortTempKeysBuffer = null;
            _sortValuesBuffer = null;
            _sortTempValuesBuffer = null;
            _gpuRadixSort = null;
            _indirectArgsBuffer = null;
            _quantizedBakeBuffer = null;
            _counterReadbackBuffer = null;
            _bufferCanvasHits = null;
            _pbfScratch = null;
        }

        private uint FetchActiveCount(ComputeBuffer source)
        {
            ComputeBuffer.CopyCount(source, _counterReadbackBuffer, 0);
            _counterReadbackBuffer.GetData(_activeCountCpu);
            return _activeCountCpu[0];
        }

        private uint FetchActiveCount(ParticleSoaBuffers soa) => FetchActiveCount(soa.CounterBuffer);

        private void DispatchIndirectArgsSetup()
        {
            argumentUtilityShader.SetInt(MaxParticleCountId, maxCapacity);
            argumentUtilityShader.SetBuffer(_kernelArgSetup, IndirectArgsBufferId, _indirectArgsBuffer);
            argumentUtilityShader.Dispatch(_kernelArgSetup, 1, 1, 1);
        }

        private uint SanitizeCount(uint raw) => raw > (uint)maxCapacity ? (uint)maxCapacity : raw;

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

        private uint SanitizeAndRepairCount(ParticleSoaBuffers soa)
        {
            uint raw = FetchActiveCount(soa);
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

            soa.SetCounterValue((uint)maxCapacity);
            return (uint)maxCapacity;
        }

        private void BindReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, Block0Id, soa.Block0);
            shader.SetBuffer(kernel, Block1Id, soa.Block1);
            shader.SetBuffer(kernel, PackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, WetnessId, soa.Wetness);
        }

        private void BindPositionsOnly(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, Block0Id, soa.Block0);
        }

        private void BindWriteSoaIndexed(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, WriteBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, WriteBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, WritePackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, WriteWetnessId, soa.Wetness);
        }

        private void BindInternalAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, InternalBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, InternalBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, InternalPackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, InternalWetnessId, soa.Wetness);
        }

        private void BindFallingAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, FallingBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, FallingBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, FallingPackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, FallingWetnessId, soa.Wetness);
        }

        private void BindFallingReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, FallingReadBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, FallingReadBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, FallingReadPackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, FallingReadWetnessId, soa.Wetness);
        }

        private void BindFallingWorldAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, FallingAppendBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, FallingAppendBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, FallingAppendPackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, FallingAppendWetnessId, soa.Wetness);
        }

        private void BindDragTargetSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, TargetBlock0Id, soa.Block0);
            shader.SetBuffer(kernel, TargetBlock1Id, soa.Block1);
            shader.SetBuffer(kernel, TargetPackedColorsId, soa.PackedColors);
            shader.SetBuffer(kernel, TargetWetnessId, soa.Wetness);
        }

        private void BindDensityCacheRw(ComputeShader shader, int kernel)
        {
            shader.SetBuffer(kernel, DensityCacheDensitiesId, _bufferDensityCacheDensities);
            shader.SetBuffer(kernel, DensityCachePressuresId, _bufferDensityCachePressures);
        }

        private void BindDensityCacheRead(ComputeShader shader, int kernel)
        {
            shader.SetBuffer(kernel, DensityCacheDensitiesId, _bufferDensityCacheDensities);
            shader.SetBuffer(kernel, DensityCachePressuresId, _bufferDensityCachePressures);
        }

        private bool UseRadixSortActive => useRadixSort && _gpuRadixSort != null;
    }
}
