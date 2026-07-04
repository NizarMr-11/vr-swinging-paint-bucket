using HarmonicEngine.Core.DataStructures;
using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        private readonly HarmonicGpuResourcePool _gpuPool = new();
        private readonly HarmonicComputeKernelRegistry _kernelRegistry = new();

        private ParticleSoaBuffers _soaInternalA => _gpuPool.SoaInternalA;
        private ParticleSoaBuffers _soaInternalB => _gpuPool.SoaInternalB;
        private ParticleSoaBuffers _soaDragScratch => _gpuPool.SoaDragScratch;
        private ComputeBuffer _bufferDensityCacheDensities => _gpuPool.DensityCacheDensities;
        private ComputeBuffer _bufferDensityCachePressures => _gpuPool.DensityCachePressures;
        private ComputeBuffer _bufferDragGrid => _gpuPool.DragGrid;
        private ComputeBuffer _gridKeyValueBuffer => _gpuPool.GridKeyValueBuffer;
        private ComputeBuffer _cellStartEndBuffer => _gpuPool.CellStartEndBuffer;
        private ComputeBuffer _sortKeysBuffer => _gpuPool.SortKeysBuffer;
        private ComputeBuffer _sortTempKeysBuffer => _gpuPool.SortTempKeysBuffer;
        private ComputeBuffer _sortValuesBuffer => _gpuPool.SortValuesBuffer;
        private ComputeBuffer _sortTempValuesBuffer => _gpuPool.SortTempValuesBuffer;
        private GpuRadixSort _gpuRadixSort => _gpuPool.GpuRadixSort;
        private ComputeBuffer _indirectArgsBuffer => _gpuPool.IndirectArgsBuffer;
        private ComputeBuffer _quantizedBakeBuffer => _gpuPool.QuantizedBakeBuffer;
        private ComputeBuffer _counterReadbackBuffer => _gpuPool.CounterReadbackBuffer;
        private ComputeBuffer _bufferCanvasHits => _gpuPool.CanvasHits;
        private PbfScratchBuffers _pbfScratch => _gpuPool.PbfScratch;
        private ComputeBuffer _bufferPrevCarryContribution => _gpuPool.PrevCarryContribution;
        private ComputeBuffer _bufferPrevInsideForCarry => _gpuPool.PrevInsideForCarry;
        private PingPongSoaManager _pingPong => _gpuPool.PingPong;
        private HarmonicParticleBufferService _bufferService => _gpuPool.BufferService;
        private int _paddedSortSize => _gpuPool.PaddedSortSize;
        private int _frameSortSize
        {
            get => _gpuPool.FrameSortSize;
            set => _gpuPool.SetFrameSortSize(value);
        }

        private bool _capacityClampWarningLogged;
        private readonly uint[] _activeCountCpu = new uint[1];

        private int _kernelArgSetup => _kernelRegistry.ArgSetup;
        private int _kernelGridClear => _kernelRegistry.GridClear;
        private int _kernelGridGenerate => _kernelRegistry.GridGenerate;
        private int _kernelGridBitonic => _kernelRegistry.GridBitonic;
        private int _kernelGridBuildRanges => _kernelRegistry.GridBuildRanges;
        private int _kernelDensity => _kernelRegistry.Density;
        private int _kernelIntegration => _kernelRegistry.Integration;
        private int _kernelContainerIntegration => _kernelRegistry.ContainerIntegration;
        private int _kernelQuantize => _kernelRegistry.Quantize;
        private int _kernelFallingWorld => _kernelRegistry.FallingWorld;
        private int _kernelDragClear => _kernelRegistry.DragClear;
        private int _kernelDragAdvect => _kernelRegistry.DragAdvect;
        private int _kernelDragScatter => _kernelRegistry.DragScatter;
        private int _kernelDragApply => _kernelRegistry.DragApply;
        private int _kernelPbfPredict => _kernelRegistry.PbfPredict;
        private int _kernelPbfDensity => _kernelRegistry.PbfDensity;
        private int _kernelPbfLambda => _kernelRegistry.PbfLambda;
        private int _kernelPbfSolve => _kernelRegistry.PbfSolve;
        private int _kernelPbfApply => _kernelRegistry.PbfApply;
        private int _kernelContainerRigidCarry => _kernelRegistry.ContainerRigidCarry;

        private static readonly int IndirectArgsBufferId = HarmonicShaderPropertyIds.IndirectArgsBuffer;
        private static readonly int CellStartEndBufferId = HarmonicShaderPropertyIds.CellStartEndBuffer;
        private static readonly int PaddedGridSizeId = HarmonicShaderPropertyIds.PaddedGridSize;
        private static readonly int ActiveParticleCountId = HarmonicShaderPropertyIds.ActiveParticleCount;
        private static readonly int GridResolutionId = HarmonicShaderPropertyIds.GridResolution;
        private static readonly int CellSizeId = HarmonicShaderPropertyIds.CellSize;
        private static readonly int BitonicLevelId = HarmonicShaderPropertyIds.BitonicLevel;
        private static readonly int BitonicLevelMaskId = HarmonicShaderPropertyIds.BitonicLevelMask;
        private static readonly int GridKeyValueBufferId = HarmonicShaderPropertyIds.GridKeyValueBuffer;
        private static readonly int SortKeysId = HarmonicShaderPropertyIds.SortKeys;
        private static readonly int SortValuesId = HarmonicShaderPropertyIds.SortValues;
        private static readonly int BitonicWidthId = HarmonicShaderPropertyIds.BitonicWidth;
        private static readonly int SortedGridKeyValueBufferId = HarmonicShaderPropertyIds.SortedGridKeyValueBuffer;
        private static readonly int DensityCacheDensitiesId = HarmonicShaderPropertyIds.DensityCacheDensities;
        private static readonly int DensityCachePressuresId = HarmonicShaderPropertyIds.DensityCachePressures;
        private static readonly int Block0Id = HarmonicShaderPropertyIds.Block0;
        private static readonly int Block1Id = HarmonicShaderPropertyIds.Block1;
        private static readonly int PackedColorsId = HarmonicShaderPropertyIds.PackedColors;
        private static readonly int WetnessId = HarmonicShaderPropertyIds.Wetness;
        private static readonly int WriteBlock0Id = HarmonicShaderPropertyIds.WriteBlock0;
        private static readonly int WriteBlock1Id = HarmonicShaderPropertyIds.WriteBlock1;
        private static readonly int WritePackedColorsId = HarmonicShaderPropertyIds.WritePackedColors;
        private static readonly int WriteWetnessId = HarmonicShaderPropertyIds.WriteWetness;
        private static readonly int InternalBlock0Id = HarmonicShaderPropertyIds.InternalBlock0;
        private static readonly int InternalBlock1Id = HarmonicShaderPropertyIds.InternalBlock1;
        private static readonly int InternalPackedColorsId = HarmonicShaderPropertyIds.InternalPackedColors;
        private static readonly int InternalWetnessId = HarmonicShaderPropertyIds.InternalWetness;
        private static readonly int FallingBlock0Id = HarmonicShaderPropertyIds.FallingBlock0;
        private static readonly int FallingBlock1Id = HarmonicShaderPropertyIds.FallingBlock1;
        private static readonly int FallingPackedColorsId = HarmonicShaderPropertyIds.FallingPackedColors;
        private static readonly int FallingWetnessId = HarmonicShaderPropertyIds.FallingWetness;
        private static readonly int FallingReadBlock0Id = HarmonicShaderPropertyIds.FallingReadBlock0;
        private static readonly int FallingReadBlock1Id = HarmonicShaderPropertyIds.FallingReadBlock1;
        private static readonly int FallingReadPackedColorsId = HarmonicShaderPropertyIds.FallingReadPackedColors;
        private static readonly int FallingReadWetnessId = HarmonicShaderPropertyIds.FallingReadWetness;
        private static readonly int FallingAppendBlock0Id = HarmonicShaderPropertyIds.FallingAppendBlock0;
        private static readonly int FallingAppendBlock1Id = HarmonicShaderPropertyIds.FallingAppendBlock1;
        private static readonly int FallingAppendPackedColorsId = HarmonicShaderPropertyIds.FallingAppendPackedColors;
        private static readonly int FallingAppendWetnessId = HarmonicShaderPropertyIds.FallingAppendWetness;
        private static readonly int TargetBlock0Id = HarmonicShaderPropertyIds.TargetBlock0;
        private static readonly int TargetBlock1Id = HarmonicShaderPropertyIds.TargetBlock1;
        private static readonly int TargetPackedColorsId = HarmonicShaderPropertyIds.TargetPackedColors;
        private static readonly int TargetWetnessId = HarmonicShaderPropertyIds.TargetWetness;
        private static readonly int DeltaTimeId = HarmonicShaderPropertyIds.DeltaTime;
        private static readonly int QuantizedOutputBufferId = HarmonicShaderPropertyIds.QuantizedOutputBuffer;
        private static readonly int QuantizedCountId = HarmonicShaderPropertyIds.QuantizedCount;
        private static readonly int QuantizationOriginId = HarmonicShaderPropertyIds.QuantizationOrigin;
        private static readonly int SmoothingRadiusId = HarmonicShaderPropertyIds.SmoothingRadius;
        private static readonly int ParticleMassId = HarmonicShaderPropertyIds.ParticleMass;
        private static readonly int GasConstantKId = HarmonicShaderPropertyIds.GasConstantK;
        private static readonly int StiffnessBId = HarmonicShaderPropertyIds.StiffnessB;
        private static readonly int RestDensityId = HarmonicShaderPropertyIds.RestDensity;
        private static readonly int ViscosityId = HarmonicShaderPropertyIds.Viscosity;
        private static readonly int GravityId = HarmonicShaderPropertyIds.Gravity;
        private static readonly int AngularVelocityWorldId = HarmonicShaderPropertyIds.AngularVelocityWorld;
        private static readonly int AngularAccelerationWorldId = HarmonicShaderPropertyIds.AngularAccelerationWorld;
        private static readonly int NozzlePlaneLocalYId = HarmonicShaderPropertyIds.NozzlePlaneLocalY;
        private static readonly int NozzleRadiusId = HarmonicShaderPropertyIds.NozzleRadius;
        private static readonly int BucketRimLocalYId = HarmonicShaderPropertyIds.BucketRimLocalY;
        private static readonly int LocalToWorldMatrixId = HarmonicShaderPropertyIds.LocalToWorldMatrix;
        private static readonly int InstantaneousBucketGlobalVelocityId = HarmonicShaderPropertyIds.InstantaneousBucketGlobalVelocity;
        private static readonly int FallingCountId = HarmonicShaderPropertyIds.FallingCount;
        private static readonly int WorldGravityId = HarmonicShaderPropertyIds.WorldGravity;
        private static readonly int WorldDragId = HarmonicShaderPropertyIds.WorldDrag;
        private static readonly int CanvasPlaneYId = HarmonicShaderPropertyIds.CanvasPlaneY;
        private static readonly int CanvasCullingEnabledId = HarmonicShaderPropertyIds.CanvasCullingEnabled;
        private static readonly int FloorRestitutionId = HarmonicShaderPropertyIds.FloorRestitution;
        private static readonly int FloorFrictionId = HarmonicShaderPropertyIds.FloorFriction;
        private static readonly int DragGridId = HarmonicShaderPropertyIds.DragGrid;
        private static readonly int GridVolumeId = HarmonicShaderPropertyIds.GridVolume;
        private static readonly int DragStrengthId = HarmonicShaderPropertyIds.DragStrength;
        private static readonly int DragDecayId = HarmonicShaderPropertyIds.DragDecay;
        private static readonly int AmbientWindStrengthId = HarmonicShaderPropertyIds.AmbientWindStrength;
        private static readonly int CanvasHitAppendId = HarmonicShaderPropertyIds.CanvasHitAppend;
        private static readonly int ContainerCenterId = HarmonicShaderPropertyIds.ContainerCenter;
        private static readonly int ContainerRadiusId = HarmonicShaderPropertyIds.ContainerRadius;
        private static readonly int ContainerFloorYId = HarmonicShaderPropertyIds.ContainerFloorY;
        private static readonly int ContainerRimYId = HarmonicShaderPropertyIds.ContainerRimY;
        private static readonly int ContainerRestitutionId = HarmonicShaderPropertyIds.ContainerRestitution;
        private static readonly int ContainerFrictionId = HarmonicShaderPropertyIds.ContainerFriction;
        private static readonly int ContainerWallStiffnessId = HarmonicShaderPropertyIds.ContainerWallStiffness;
        private static readonly int ContainerDampingId = HarmonicShaderPropertyIds.ContainerDamping;
        private static readonly int ContainerMaxSpeedId = HarmonicShaderPropertyIds.ContainerMaxSpeed;
        private static readonly int ColorDiffusionRateId = HarmonicShaderPropertyIds.ColorDiffusionRate;
        private static readonly int MaxParticleCountId = HarmonicShaderPropertyIds.MaxParticleCount;
        private static readonly int CanvasPaintAbsorbEnabledId = HarmonicShaderPropertyIds.CanvasPaintAbsorbEnabled;
        private static readonly int CanvasAbsorbRateId = HarmonicShaderPropertyIds.CanvasAbsorbRate;
        private static readonly int CanvasAbsorbPaintWeightScaleId = HarmonicShaderPropertyIds.CanvasAbsorbPaintWeightScale;
        private static readonly int PredictedBlock0Id = HarmonicShaderPropertyIds.PredictedBlock0;
        private static readonly int OldBlock0Id = HarmonicShaderPropertyIds.OldBlock0;
        private static readonly int LambdasId = HarmonicShaderPropertyIds.Lambdas;
        private static readonly int GradSqSumId = HarmonicShaderPropertyIds.GradSqSum;
        private static readonly int PbfEpsilonId = HarmonicShaderPropertyIds.PbfEpsilon;
        private static readonly int PbfRelaxationId = HarmonicShaderPropertyIds.PbfRelaxation;
        private static readonly int PbfVelocityDampingId = HarmonicShaderPropertyIds.PbfVelocityDamping;
        private static readonly int PbfMaxPositionDeltaId = HarmonicShaderPropertyIds.PbfMaxPositionDelta;
        private static readonly int CohesionStrengthId = HarmonicShaderPropertyIds.CohesionStrength;
        private static readonly int ContainerLocalToWorldId = HarmonicShaderPropertyIds.ContainerLocalToWorld;
        private static readonly int ContainerWorldToLocalId = HarmonicShaderPropertyIds.ContainerWorldToLocal;
        private static readonly int ContainerHeightId = HarmonicShaderPropertyIds.ContainerHeight;
        private static readonly int ContainerSpillEnabledId = HarmonicShaderPropertyIds.ContainerSpillEnabled;
        private static readonly int ContainerUsesOrientationId = HarmonicShaderPropertyIds.ContainerUsesOrientation;
        private static readonly int ContainerRotationDeltaId = HarmonicShaderPropertyIds.ContainerRotationDelta;
        private static readonly int ContainerAngularVelocityWorldId = HarmonicShaderPropertyIds.ContainerAngularVelocityWorld;
        private static readonly int ContainerFloorPivotId = HarmonicShaderPropertyIds.ContainerFloorPivot;

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

            if (openTopCylinder.enabled && openTopCylinderUsePbf)
            {
                return pbfSolverShader != null;
            }

            return wcsphDensityShader != null
                && wcsphIntegrationShader != null;
        }

        private void InitializeBuffers()
        {
            _gpuPool.Initialize(new HarmonicGpuResourcePoolConfig
            {
                MaxCapacity = maxCapacity,
                DragGridVolume = dragGridVolume,
                MaxCanvasHitsPerFrame = maxCanvasHitsPerFrame,
                RadixSortShader = radixSortShader
            });
        }

        private void CacheKernels()
        {
            _kernelRegistry.CacheKernels(new HarmonicComputeShaderSet
            {
                ArgumentUtility = argumentUtilityShader,
                SpatialHashGrid = spatialHashGridShader,
                StreamCompaction = wcsphDensityShader,
                StreamCompactionIntegrate = wcsphIntegrationShader,
                DataCompaction = dataCompactionShader,
                FallingFluidWorld = fallingFluidWorldShader,
                EulerianDragGrid = eulerianDragGridShader,
                PbfSolver = pbfSolverShader,
                ContainerRigidCarry = containerRigidCarryShader
            });
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
            wcsphDensityShader = streamShader;
            wcsphIntegrationShader = integrateShader ?? streamShader;
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
            ResolveOtcParticleFieldShader();
            CacheOtcFieldKernel();
            _lastBucketPosition = GetBucketPosition();
        }

        internal void ComputeFrameSortSize(uint activeCount) => ComputeFrameSortSizeInternal(activeCount);

        private void ComputeFrameSortSizeInternal(uint activeCount)
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

        private void ReleaseBuffers() => _gpuPool.Release();

        private uint FetchActiveCount(ComputeBuffer source) =>
            HarmonicActiveCountUtility.FetchActiveCount(source, _counterReadbackBuffer, _activeCountCpu);

        private uint FetchActiveCount(ParticleSoaBuffers soa) =>
            HarmonicActiveCountUtility.FetchActiveCount(soa, _counterReadbackBuffer, _activeCountCpu);

        private void DispatchIndirectArgsSetup()
        {
            argumentUtilityShader.SetInt(MaxParticleCountId, maxCapacity);
            argumentUtilityShader.SetBuffer(_kernelArgSetup, IndirectArgsBufferId, _indirectArgsBuffer);
            argumentUtilityShader.Dispatch(_kernelArgSetup, 1, 1, 1);
        }

        private uint SanitizeCount(uint raw) =>
            HarmonicActiveCountUtility.SanitizeCount(raw, maxCapacity);

        private uint SanitizeAndRepairCount(ComputeBuffer source) =>
            HarmonicActiveCountUtility.SanitizeAndRepairCount(
                source,
                maxCapacity,
                _counterReadbackBuffer,
                _activeCountCpu,
                ref _capacityClampWarningLogged);

        private uint SanitizeAndRepairCount(ParticleSoaBuffers soa) =>
            HarmonicActiveCountUtility.SanitizeAndRepairCount(
                soa,
                maxCapacity,
                _counterReadbackBuffer,
                _activeCountCpu,
                ref _capacityClampWarningLogged);

        private void BindReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindReadSoa(kernel, soa);

        private void BindPositionsOnly(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindPositionsOnly(kernel, soa);

        private void BindWriteSoaIndexed(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindWriteSoaIndexed(kernel, soa);

        private void BindInternalAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindInternalAppendSoa(kernel, soa);

        private void BindFallingAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindFallingAppendSoa(kernel, soa);

        private void BindFallingReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindFallingReadSoa(kernel, soa);

        private void BindFallingWorldAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindFallingWorldAppendSoa(kernel, soa);

        private void BindDragTargetSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>
            shader.BindDragTargetSoa(kernel, soa);

        private void BindDensityCacheRw(ComputeShader shader, int kernel) =>
            shader.BindDensityCacheRw(kernel, _bufferDensityCacheDensities, _bufferDensityCachePressures);

        private void BindDensityCacheRead(ComputeShader shader, int kernel) =>
            shader.BindDensityCacheRead(kernel, _bufferDensityCacheDensities, _bufferDensityCachePressures);

        private void BindDensityCacheDensitiesOnly(ComputeShader shader, int kernel) =>
            shader.BindDensityCacheDensitiesOnly(kernel, _bufferDensityCacheDensities);

        internal bool UseRadixSortActive => useRadixSort && _gpuRadixSort != null;
    }
}
