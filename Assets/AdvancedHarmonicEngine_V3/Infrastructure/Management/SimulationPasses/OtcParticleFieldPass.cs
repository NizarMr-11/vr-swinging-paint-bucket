using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    /// <summary>
    /// Dispatches ClassifyParticleFieldKernel and binds the resulting SRV on consumer kernels.
    /// </summary>
    internal sealed class OtcParticleFieldPass
    {
        private static readonly ProfilerMarker MarkerClassify = new("Harmonic.OtcClassifyField");

        public void ClassifyFromBlock0(HarmonicPipelineController host, ComputeBuffer sourceBlock0, uint activeCount)
        {
            if (host.OtcParticleFieldShader == null || host.KernelOtcClassify < 0 || activeCount == 0)
            {
                return;
            }

            using (MarkerClassify.Auto())
            {
                host.ApplyOtcFieldUniforms(host.OtcParticleFieldShader);
                host.OtcParticleFieldShader.SetBuffer(
                    host.KernelOtcClassify,
                    HarmonicShaderPropertyIds.SourceBlock0,
                    sourceBlock0);
                host.OtcParticleFieldShader.SetBuffer(
                    host.KernelOtcClassify,
                    HarmonicShaderPropertyIds.ParticleFieldRw,
                    host.PbfScratch.ParticleField);
                host.OtcParticleFieldShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                host.OtcParticleFieldShader.SetInt(HarmonicShaderPropertyIds.MaxParticleCount, host.MaxCapacity);
                int groups = Mathf.CeilToInt(activeCount / 64f);
                host.OtcParticleFieldShader.Dispatch(host.KernelOtcClassify, groups, 1, 1);
            }
        }

        public static void BindFieldRead(ComputeShader shader, int kernel, ComputeBuffer particleField)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.ParticleField, particleField);
        }
    }
}
