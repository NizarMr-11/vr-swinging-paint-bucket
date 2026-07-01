using HarmonicEngine.Domain.Models;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    /// <summary>
    /// Routes one pipeline frame to container PBF, WCSPH, bucket SPH, or world-falling paths.
    /// </summary>
    internal sealed class HarmonicSimulationFrameRouter
    {
        private readonly OpenTopCylinderPbfSimulationPass _pbfPass = new();
        private readonly FallingWorldSimulationPass _fallingPass = new();

        public void Execute(HarmonicPipelineController host, float deltaTime)
        {
            if (!host.SimulationActive
                || host.SimulationMode == HarmonicSimulationMode.BakePlayback
                || !host.AreShadersReadyForPasses()
                || host.PingPong == null)
            {
                // PingPong is null right after a domain reload (e.g. live recompile in Play mode)
                // before buffers are re-initialized; skip the frame instead of throwing.
                return;
            }

            host.BeginPipelineFrame();

            uint activeCount = host.SanitizeAndRepairActiveCount();
            host.SetCachedInternalCount(activeCount);
            if (activeCount == 0)
            {
                host.OnEmptyPipelineFrame();
                return;
            }

            if (host.WorldFallingOnly)
            {
                host.ExecuteWorldFallingOnlyFrame(activeCount, deltaTime);
                host.PublishPipelineFrameDiagnostic(host.CachedInternalCount);
                return;
            }

            if (host.ContainerFluidEnabled)
            {
                if (host.UsePbf)
                {
                    _pbfPass.Execute(host, new HarmonicSimulationContext
                    {
                        ActiveCount = activeCount,
                        DeltaTime = deltaTime
                    });
                }
                else
                {
                    host.ExecuteContainerFluidFrame(activeCount, deltaTime);
                }

                _fallingPass.Execute(host, deltaTime);
                host.PublishPipelineFrameDiagnostic(host.CachedInternalCount);
                return;
            }

            host.ExecuteBucketSphFrame(activeCount, deltaTime);
            host.PublishPipelineFrameDiagnostic(host.CachedInternalCount);
        }
    }
}
