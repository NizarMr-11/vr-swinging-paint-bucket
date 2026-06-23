using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Rendering
{
    internal static class HarmonicParticleSoaShaderBindings
    {
        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int PackedColorsId = Shader.PropertyToID("_PackedColors");
        private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");

        public static void BindSoa(Material material, ParticleSoaBuffers soa, int count)
        {
            material.SetBuffer(Block0Id, soa.Block0);
            material.SetBuffer(PackedColorsId, soa.PackedColors);
            material.SetInt(ParticleCountId, count);
        }
    }
}
