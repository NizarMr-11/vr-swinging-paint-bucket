using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    internal sealed class FallingWorldSimulationPass
    {
        private static readonly ProfilerMarker MarkerWorldFalling = new("Harmonic.WorldFalling");

        public void Execute(HarmonicPipelineController host, float deltaTime)
        {
            uint fallingCount = host.RepairParticleCount(host.SoaFalling);
            if (fallingCount == 0 || host.FallingFluidWorldShader == null)
            {
                return;
            }

            host.SoaFallingWorld.SetCounterValue(0);
            host.PassBindFallingReadSoa(host.FallingFluidWorldShader, host.KernelFallingWorld, host.SoaFalling);
            host.PassBindFallingWorldAppendSoa(host.FallingFluidWorldShader, host.KernelFallingWorld, host.SoaFallingWorld);
            host.FallingFluidWorldShader.SetBuffer(host.KernelFallingWorld, HarmonicShaderPropertyIds.CanvasHitAppend, host.CanvasHitsBuffer);
            host.ApplyFallingWorldUniforms(host.FallingFluidWorldShader, fallingCount, deltaTime);
            int groups = Mathf.CeilToInt(fallingCount / 64f);
            using (MarkerWorldFalling.Auto())
            {
                host.FallingFluidWorldShader.Dispatch(host.KernelFallingWorld, groups, 1, 1);
            }

            host.SetLastCanvasHitCount(host.FetchBufferActiveCount(host.CanvasHitsBuffer));
            fallingCount = host.RepairParticleCount(host.SoaFallingWorld);
            host.SetLastFallingDebugCount(fallingCount);
            host.SetLastFallingQuantizeCount(fallingCount);
            host.SwapFallingParticleBuffers();

            if (host.VerbosePipelineDiagnostics && !host.PerfDiagnosticsMuted)
            {
                host.PublishStageDiagnostic(
                    "containerFalling",
                    $"falling={fallingCount} canvasHits={host.LastCanvasHitCount} planeY={host.CanvasPlaneY:F2} dt={deltaTime:F4}");
            }
        }
    }
}
