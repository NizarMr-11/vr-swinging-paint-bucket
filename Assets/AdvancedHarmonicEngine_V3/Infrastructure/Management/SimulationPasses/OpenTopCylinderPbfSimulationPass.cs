using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    internal sealed class OpenTopCylinderPbfSimulationPass
    {
        private static readonly ProfilerMarker MarkerPbfPredict = new("Harmonic.PbfPredict");
        private static readonly ProfilerMarker MarkerPbfDensity = new("Harmonic.PbfDensity");
        private static readonly ProfilerMarker MarkerPbfLambda = new("Harmonic.PbfLambda");
        private static readonly ProfilerMarker MarkerPbfSolve = new("Harmonic.PbfSolve");
        private static readonly ProfilerMarker MarkerPbfApply = new("Harmonic.PbfApply");

        private readonly SpatialHashBuildPass _spatialHash = new();

        public void Execute(HarmonicPipelineController host, HarmonicSimulationContext ctx)
        {
            uint activeCount = ctx.ActiveCount;
            float deltaTime = Mathf.Min(ctx.DeltaTime, host.ContainerFluidMaxTimeStep);
            int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / host.ContainerFluidMaxTimeStep), 1, 2);
            float subDt = deltaTime / steps;

            for (int step = 0; step < steps; step++)
            {
                host.ComputeFrameSortSize(activeCount);
                _spatialHash.Build(host, host.PingPong.ReadSet, activeCount);
                activeCount = RunSubstep(host, activeCount, subDt, preserveFloorGate: step > 0);
            }

            // Reset the gate so no other PbfSolver dispatch path (e.g. isolated test harnesses that
            // reuse this shared shader without the frame-start membership flag) inherits it enabled.
            host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.FloorClampContinuityEnabled, 0);
            host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.FloorClampPreserveGate, 0);

            host.SetCachedInternalCount(host.RepairParticleCount(host.PingPong.ReadSet));
            host.SetLastCanvasHitCount(host.FetchBufferActiveCount(host.CanvasHitsBuffer));
            host.MaybeSampleParticlePositions(host.PingPong.ReadSet, host.CachedInternalCount, "containerPbf");
            host.MaybeLogPbfConvergence();
            host.MaybeLogPbfTelemetry(deltaTime, steps, subDt);

            if (!host.PerfDiagnosticsMuted)
            {
                host.PublishStageDiagnostic(
                    "containerPbf",
                    $"active={host.CachedInternalCount} sortSize={host.FrameSortSize} R={host.ContainerFluidRadius:F2} " +
                    $"floorY={host.ContainerFluidFloorY:F2} rimY={host.ContainerFluidRimY:F2} substeps={steps} " +
                    $"pbfIters={host.PbfIterations} solver=PBF");
            }
        }

        private uint RunSubstep(HarmonicPipelineController host, uint activeCount, float deltaTime, bool preserveFloorGate)
        {
            float smoothingRadius = host.SphSmoothingRadius;
            host.ApplyPbfUniforms(smoothingRadius, deltaTime);

            // Enable the volume-continuity floor-clamp gate for this pass. Predict samples frame-start
            // container membership into _OldBlock0[i].w on the first substep only; later substeps
            // preserve that flag so the gate reflects frame-start geometry, not intra-frame motion.
            host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.FloorClampContinuityEnabled, 1);
            host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.FloorClampPreserveGate, preserveFloorGate ? 1 : 0);

            using (MarkerPbfPredict.Auto())
            {
                host.PassBindReadSoa(host.PbfSolverShader, host.KernelPbfPredict, host.PingPong.ReadSet);
                host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.OldBlock0, host.PbfScratch.OldBlock0);
                host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
                host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
                host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
                host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                host.PbfSolverShader.DispatchIndirect(host.KernelPbfPredict, host.IndirectArgsBuffer, 0);
            }

            _spatialHash.Build(host, host.PbfScratch.PredictedBlock0, activeCount);

            for (int iter = 0; iter < host.PbfIterations; iter++)
            {
                using (MarkerPbfDensity.Auto())
                {
                    host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
                    host.PassBindDensityCacheRw(host.PbfSolverShader, host.KernelPbfDensity);
                    host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                    host.PbfSolverShader.DispatchIndirect(host.KernelPbfDensity, host.IndirectArgsBuffer, 0);
                }

                using (MarkerPbfLambda.Auto())
                {
                    host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
                    host.PassBindDensityCacheRead(host.PbfSolverShader, host.KernelPbfLambda);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.Lambdas, host.PbfScratch.Lambdas);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.GradSqSum, host.PbfScratch.GradSqSum);
                    host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                    host.PbfSolverShader.DispatchIndirect(host.KernelPbfLambda, host.IndirectArgsBuffer, 0);
                }

                using (MarkerPbfSolve.Auto())
                {
                    host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
                    host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.Lambdas, host.PbfScratch.Lambdas);
                    // _OldBlock0.w carries the frame-start floor-clamp gate flag read by PbfFloorClampAllowed.
                    host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.OldBlock0, host.PbfScratch.OldBlock0);
                    host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                    host.PbfSolverShader.DispatchIndirect(host.KernelPbfSolve, host.IndirectArgsBuffer, 0);
                }
            }

            using (MarkerPbfApply.Auto())
            {
                host.PassBindReadSoa(host.PbfSolverShader, host.KernelPbfApply, host.PingPong.ReadSet);
                host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.OldBlock0, host.PbfScratch.OldBlock0);
                host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
                host.PassBindDensityCacheDensitiesOnly(host.PbfSolverShader, host.KernelPbfApply);
                host.PassBindWriteSoaIndexed(host.PbfSolverShader, host.KernelPbfApply, host.PingPong.WriteSet);
                host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.CanvasHitAppend, host.CanvasHitsBuffer);
                host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                host.PbfSolverShader.DispatchIndirect(host.KernelPbfApply, host.IndirectArgsBuffer, 0);
            }

            host.PingPong.WriteSet.SetCounterValue(activeCount);
            host.PingPong.Swap();
            return host.RepairParticleCount(host.PingPong.ReadSet);
        }
    }
}
