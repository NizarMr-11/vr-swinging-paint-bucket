using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private static readonly ProfilerMarker MarkerPbfPredict = new("Harmonic.PbfPredict");
        private static readonly ProfilerMarker MarkerPbfDensity = new("Harmonic.PbfDensity");
        private static readonly ProfilerMarker MarkerPbfLambda = new("Harmonic.PbfLambda");
        private static readonly ProfilerMarker MarkerPbfSolve = new("Harmonic.PbfSolve");
        private static readonly ProfilerMarker MarkerPbfApply = new("Harmonic.PbfApply");

        private void ExecuteContainerPbfFrame(uint activeCount, float deltaTime)
        {
            deltaTime = Mathf.Min(deltaTime, containerFluid.maxTimeStep);
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / containerFluid.maxTimeStep), 1, 2);
            float subDt = deltaTime / steps;

            for (int step = 0; step < steps; step++)
            {
                ComputeFrameSortSize(activeCount);
                BuildSpatialHashGrid(_pingPong.ReadSet, activeCount);
                RunPbfSubstep(activeCount, subDt);
            }

            _cachedInternalCount = SanitizeAndRepairCount(_pingPong.ReadSet);
            _lastFallingDebugCount = 0;
            _lastFallingQuantizeCount = 0;

            MaybeSampleParticlePositions(_pingPong.ReadSet, _cachedInternalCount, "containerPbf");
            MaybeLogPbfTelemetry(deltaTime, steps, subDt);

            if (!perfDiagnosticsMuted)
            {
                PublishStageDiagnostic(
                    "containerPbf",
                    $"active={_cachedInternalCount} sortSize={_frameSortSize} R={containerFluid.radius:F2} " +
                    $"floorY={containerFluid.floorY:F2} rimY={containerFluid.rimY:F2} substeps={steps} " +
                    $"pbfIters={pbfIterations} solver=PBF");
            }
        }

        private void RunPbfSubstep(uint activeCount, float deltaTime)
        {
            float smoothingRadius = sphSolver.SmoothingRadius(cellSize);
            ApplyPbfUniforms(pbfSolverShader, smoothingRadius, deltaTime);

            using (MarkerPbfPredict.Auto())
            {
                BindReadSoa(pbfSolverShader, _kernelPbfPredict, _pingPong.ReadSet);
                pbfSolverShader.SetBuffer(_kernelPbfPredict, OldBlock0Id, _pbfScratch.OldBlock0);
                pbfSolverShader.SetBuffer(_kernelPbfPredict, PredictedBlock0Id, _pbfScratch.PredictedBlock0);
                pbfSolverShader.SetBuffer(_kernelPbfPredict, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                pbfSolverShader.SetBuffer(_kernelPbfPredict, CellStartEndBufferId, _cellStartEndBuffer);
                pbfSolverShader.SetInt(ActiveParticleCountId, (int)activeCount);
                pbfSolverShader.DispatchIndirect(_kernelPbfPredict, _indirectArgsBuffer, 0);
            }

            BuildSpatialHashGrid(_pbfScratch.PredictedBlock0, activeCount);

            for (int iter = 0; iter < pbfIterations; iter++)
            {
                using (MarkerPbfDensity.Auto())
                {
                    pbfSolverShader.SetBuffer(_kernelPbfDensity, PredictedBlock0Id, _pbfScratch.PredictedBlock0);
                    pbfSolverShader.SetBuffer(_kernelPbfDensity, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                    pbfSolverShader.SetBuffer(_kernelPbfDensity, CellStartEndBufferId, _cellStartEndBuffer);
                    BindDensityCacheRw(pbfSolverShader, _kernelPbfDensity);
                    pbfSolverShader.SetInt(ActiveParticleCountId, (int)activeCount);
                    pbfSolverShader.DispatchIndirect(_kernelPbfDensity, _indirectArgsBuffer, 0);
                }

                using (MarkerPbfLambda.Auto())
                {
                    pbfSolverShader.SetBuffer(_kernelPbfLambda, PredictedBlock0Id, _pbfScratch.PredictedBlock0);
                    pbfSolverShader.SetBuffer(_kernelPbfLambda, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                    pbfSolverShader.SetBuffer(_kernelPbfLambda, CellStartEndBufferId, _cellStartEndBuffer);
                    BindDensityCacheRead(pbfSolverShader, _kernelPbfLambda);
                    pbfSolverShader.SetBuffer(_kernelPbfLambda, LambdasId, _pbfScratch.Lambdas);
                    pbfSolverShader.SetInt(ActiveParticleCountId, (int)activeCount);
                    pbfSolverShader.DispatchIndirect(_kernelPbfLambda, _indirectArgsBuffer, 0);
                }

                using (MarkerPbfSolve.Auto())
                {
                    pbfSolverShader.SetBuffer(_kernelPbfSolve, PredictedBlock0Id, _pbfScratch.PredictedBlock0);
                    pbfSolverShader.SetBuffer(_kernelPbfSolve, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                    pbfSolverShader.SetBuffer(_kernelPbfSolve, CellStartEndBufferId, _cellStartEndBuffer);
                    pbfSolverShader.SetBuffer(_kernelPbfSolve, LambdasId, _pbfScratch.Lambdas);
                    pbfSolverShader.SetInt(ActiveParticleCountId, (int)activeCount);
                    pbfSolverShader.DispatchIndirect(_kernelPbfSolve, _indirectArgsBuffer, 0);
                }
            }

            using (MarkerPbfApply.Auto())
            {
                BindReadSoa(pbfSolverShader, _kernelPbfApply, _pingPong.ReadSet);
                pbfSolverShader.SetBuffer(_kernelPbfApply, OldBlock0Id, _pbfScratch.OldBlock0);
                pbfSolverShader.SetBuffer(_kernelPbfApply, PredictedBlock0Id, _pbfScratch.PredictedBlock0);
                BindDensityCacheRead(pbfSolverShader, _kernelPbfApply);
                BindWriteSoaIndexed(pbfSolverShader, _kernelPbfApply, _pingPong.WriteSet);
                pbfSolverShader.SetInt(ActiveParticleCountId, (int)activeCount);
                pbfSolverShader.DispatchIndirect(_kernelPbfApply, _indirectArgsBuffer, 0);
            }

            _pingPong.WriteSet.SetCounterValue(activeCount);
            _pingPong.Swap();
        }

        private void ApplyPbfUniforms(ComputeShader shader, float smoothingRadius, float deltaTime)
        {
            shader.SetInt(GridResolutionId, _frameSortSize);
            shader.SetFloat(CellSizeId, cellSize);
            shader.SetFloat(SmoothingRadiusId, smoothingRadius);
            shader.SetFloat(ParticleMassId, ResolveContainerParticleMass());
            shader.SetFloat(RestDensityId, sphSolver.RestDensity);
            shader.SetFloat(ViscosityId, containerFluid.viscosity);
            shader.SetFloat(PbfEpsilonId, 600f / (smoothingRadius * smoothingRadius));
            shader.SetVector(GravityId, gravity);
            shader.SetVector(ContainerCenterId, containerFluid.center);
            shader.SetFloat(ContainerRadiusId, containerFluid.radius);
            shader.SetFloat(ContainerFloorYId, containerFluid.floorY);
            shader.SetFloat(ContainerRestitutionId, containerFluid.restitution);
            shader.SetFloat(ContainerFrictionId, containerFluid.friction);
            shader.SetFloat(DeltaTimeId, deltaTime);
            shader.SetInt(MaxParticleCountId, maxCapacity);
        }
    }
}
