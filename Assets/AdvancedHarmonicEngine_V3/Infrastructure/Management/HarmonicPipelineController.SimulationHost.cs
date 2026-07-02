using HarmonicEngine.Core.DataStructures;
using HarmonicEngine.Infrastructure.Management.Gpu;
using HarmonicEngine.Infrastructure.Management.SimulationPasses;
using UnityEngine;



namespace HarmonicEngine.Infrastructure.Management

{

    public partial class HarmonicPipelineController

    {

        internal SpatialHashBuildPass SpatialHashPass => _spatialHashPass;

        internal PingPongSoaManager PingPong => _pingPong;

        internal PbfScratchBuffers PbfScratch => _pbfScratch;

        internal ComputeBuffer PrevCarryContributionBuffer => _bufferPrevCarryContribution;

        internal ComputeBuffer PrevInsideForCarryBuffer => _bufferPrevInsideForCarry;

        internal ComputeBuffer IndirectArgsBuffer => _indirectArgsBuffer;

        internal ComputeBuffer GridKeyValueBuffer => _gridKeyValueBuffer;

        internal ComputeBuffer CellStartEndBuffer => _cellStartEndBuffer;

        internal ComputeBuffer SortKeysBuffer => _sortKeysBuffer;

        internal ComputeBuffer SortValuesBuffer => _sortValuesBuffer;

        internal ComputeBuffer SortTempKeysBuffer => _sortTempKeysBuffer;

        internal ComputeBuffer SortTempValuesBuffer => _sortTempValuesBuffer;

        internal ComputeBuffer CanvasHitsBuffer => _bufferCanvasHits;

        internal GpuRadixSort GpuRadixSort => _gpuRadixSort;

        internal bool SimulationActive => simulationActive;

        internal bool PerfDiagnosticsMuted => perfDiagnosticsMuted;

        internal bool VerbosePipelineDiagnostics => verbosePipelineDiagnostics;

        internal uint CachedInternalCount => _cachedInternalCount;

        internal float SphSmoothingRadius => sphSolver.SmoothingRadius(cellSize);

        internal float ContainerFluidMaxTimeStep => openTopCylinder.maxTimeStep;

        internal float ContainerFluidRadius => openTopCylinder.radius;

        internal float ContainerFluidFloorY => openTopCylinder.floorY;

        internal float ContainerFluidRimY => openTopCylinder.rimY;

        internal float ContainerFluidHeight => openTopCylinder.height;

        internal float ContainerFluidRestitution => openTopCylinder.restitution;

        internal float ContainerFluidFriction => openTopCylinder.friction;

        internal Vector3 ContainerFluidCenter => openTopCylinder.center;

        internal Matrix4x4 ContainerLocalToWorld => _containerLocalToWorld;

        internal Matrix4x4 ContainerWorldToLocal => _containerWorldToLocal;

        internal Vector3 ContainerFloorPivot => _containerFloorPivot;



        internal ComputeShader SpatialHashGridShader => spatialHashGridShader;

        internal ComputeShader PbfSolverShader => pbfSolverShader;

        internal ComputeShader FallingFluidWorldShader => fallingFluidWorldShader;

        internal ComputeShader ContainerRigidCarryShader => containerRigidCarryShader;



        internal int KernelGridClear => _kernelGridClear;

        internal int KernelGridGenerate => _kernelGridGenerate;

        internal int KernelGridBitonic => _kernelGridBitonic;

        internal int KernelGridBuildRanges => _kernelGridBuildRanges;

        internal int KernelPbfPredict => _kernelPbfPredict;

        internal int KernelPbfDensity => _kernelPbfDensity;

        internal int KernelPbfLambda => _kernelPbfLambda;

        internal int KernelPbfSolve => _kernelPbfSolve;

        internal int KernelPbfApply => _kernelPbfApply;

        internal int KernelFallingWorld => _kernelFallingWorld;

        internal int KernelContainerRigidCarry => _kernelContainerRigidCarry;



        internal bool AreShadersReadyForPasses() => AreShadersReady();



        internal void SetCachedInternalCount(uint count) => _cachedInternalCount = count;



        internal void SetLastCanvasHitCount(uint count) => _lastCanvasHitCount = count;



        internal void SetLastFallingDebugCount(uint count) => _lastFallingDebugCount = count;



        internal void SetLastFallingQuantizeCount(uint count) => _lastFallingQuantizeCount = count;



        internal uint SanitizeAndRepairActiveCount() => SanitizeAndRepairCount(_pingPong.ReadSet);



        internal uint RepairParticleCount(ParticleSoaBuffers soa) => SanitizeAndRepairCount(soa);



        internal uint SanitizeCountValue(uint raw) => SanitizeCount(raw);



        internal uint FetchBufferActiveCount(ComputeBuffer source) => FetchActiveCount(source);



        internal void BeginPipelineFrame()

        {

            _pingPong.BeginFrame();

            _bufferCanvasHits?.SetCounterValue(0);

            _lastCanvasHitCount = 0;

        }



        internal void OnEmptyPipelineFrame()

        {

            _lastFallingQuantizeCount = 0;

            _lastFallingDebugCount = 0;

            _pingPong.Swap();

            PublishPipelineFrameDiagnosticInternal(0);

        }



        internal void PublishPipelineFrameDiagnostic(uint activeCount) =>

            PublishPipelineFrameDiagnosticInternal(activeCount);



        internal void PublishStageDiagnostic(string stage, string detail) =>

            PublishStageDiagnosticInternal(stage, detail);



        internal void MaybeSampleParticlePositions(ParticleSoaBuffers soa, uint activeCount, string stage) =>

            MaybeSampleParticlePositionsInternal(soa, activeCount, stage);



        internal void MaybeLogPbfConvergence() => MaybeLogPbfConvergenceInternal();



        internal void MaybeLogPbfTelemetry(float deltaTime, int substeps, float subDt) =>

            MaybeLogPbfTelemetryInternal(deltaTime, substeps, subDt);



        internal void MaybeLogSortDiagnostic(uint activeCount) => MaybeLogSortDiagnosticInternal(activeCount);



        internal void ApplyFallingWorldUniforms(ComputeShader shader, uint fallingCount, float deltaTime) =>

            ApplyFallingWorldUniformsInternal(shader, fallingCount, deltaTime);



        internal void ApplyPbfUniforms(float smoothingRadius, float deltaTime) =>

            ApplyPbfUniformsInternal(pbfSolverShader, smoothingRadius, deltaTime);



        internal void PassBindReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>

            BindReadSoa(shader, kernel, soa);



        internal void PassBindWriteSoaIndexed(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>

            BindWriteSoaIndexed(shader, kernel, soa);



        internal void PassBindFallingReadSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>

            BindFallingReadSoa(shader, kernel, soa);



        internal void PassBindFallingWorldAppendSoa(ComputeShader shader, int kernel, ParticleSoaBuffers soa) =>

            BindFallingWorldAppendSoa(shader, kernel, soa);



        internal void PassBindDensityCacheRw(ComputeShader shader, int kernel) =>

            BindDensityCacheRw(shader, kernel);



        internal void PassBindDensityCacheRead(ComputeShader shader, int kernel) =>

            BindDensityCacheRead(shader, kernel);



        internal void PassBindDensityCacheDensitiesOnly(ComputeShader shader, int kernel) =>

            BindDensityCacheDensitiesOnly(shader, kernel);



        internal void PassDispatchIndirectArgsSetup() => DispatchIndirectArgsSetup();



        internal void ExecuteWorldFallingOnlyFrame(uint activeCount, float deltaTime) =>

            ExecuteWorldFallingOnlyFrameInternal(activeCount, deltaTime);



        internal void ExecuteContainerFluidFrame(uint activeCount, float deltaTime) =>

            ExecuteContainerFluidFrameInternal(activeCount, deltaTime);



        internal void ExecuteBucketSphFrame(uint activeCount, float deltaTime) =>

            ExecuteBucketSphFrameInternal(activeCount, deltaTime);



        internal uint FetchSoaActiveCount(ParticleSoaBuffers soa) => FetchActiveCount(soa);

    }

}


