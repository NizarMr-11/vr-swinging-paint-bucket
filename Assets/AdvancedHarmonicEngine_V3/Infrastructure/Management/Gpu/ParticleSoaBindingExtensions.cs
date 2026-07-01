using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Gpu
{
    public static class ParticleSoaBindingExtensions
    {
        public static void BindReadSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.Block0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.Block1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.PackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.Wetness, soa.Wetness);
        }

        public static void BindPositionsOnly(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.Block0, soa.Block0);
        }

        public static void BindWriteSoaIndexed(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.WriteBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.WriteBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.WritePackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.WriteWetness, soa.Wetness);
        }

        public static void BindInternalAppendSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.InternalBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.InternalBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.InternalPackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.InternalWetness, soa.Wetness);
        }

        public static void BindFallingAppendSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingPackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingWetness, soa.Wetness);
        }

        public static void BindFallingReadSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingReadBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingReadBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingReadPackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingReadWetness, soa.Wetness);
        }

        public static void BindFallingWorldAppendSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingAppendBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingAppendBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingAppendPackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.FallingAppendWetness, soa.Wetness);
        }

        public static void BindDragTargetSoa(this ComputeShader shader, int kernel, ParticleSoaBuffers soa)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.TargetBlock0, soa.Block0);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.TargetBlock1, soa.Block1);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.TargetPackedColors, soa.PackedColors);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.TargetWetness, soa.Wetness);
        }

        public static void BindDensityCacheRw(this ComputeShader shader, int kernel, ComputeBuffer densities, ComputeBuffer pressures)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.DensityCacheDensities, densities);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.DensityCachePressures, pressures);
        }

        public static void BindDensityCacheRead(this ComputeShader shader, int kernel, ComputeBuffer densities, ComputeBuffer pressures)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.DensityCacheDensities, densities);
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.DensityCachePressures, pressures);
        }

        public static void BindDensityCacheDensitiesOnly(this ComputeShader shader, int kernel, ComputeBuffer densities)
        {
            shader.SetBuffer(kernel, HarmonicShaderPropertyIds.DensityCacheDensities, densities);
        }
    }
}
