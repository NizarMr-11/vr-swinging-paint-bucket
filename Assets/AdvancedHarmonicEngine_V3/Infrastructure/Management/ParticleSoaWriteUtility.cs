using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public static class ParticleSoaWriteUtility
    {
        public static void WriteParticles(ParticleSoaBuffers soa, FluidParticle[] particles, int sourceOffset, int destOffset, int count)
        {
            if (soa == null || particles == null || count <= 0)
            {
                return;
            }

            var block0 = new Vector4[count];
            var block1 = new Vector4[count];
            var colors = new uint[count];
            var wetness = new float[count];

            for (int i = 0; i < count; i++)
            {
                FluidParticle p = particles[sourceOffset + i];
                block0[i] = new Vector4(p.Position.x, p.Position.y, p.Position.z, p.Density);
                block1[i] = new Vector4(p.Velocity.x, p.Velocity.y, p.Velocity.z, p.Pressure);
                colors[i] = p.PackedColorRGBA;
                wetness[i] = p._Padding.x;
            }

            soa.Block0.SetData(block0, 0, destOffset, count);
            soa.Block1.SetData(block1, 0, destOffset, count);
            soa.PackedColors.SetData(colors, 0, destOffset, count);
            soa.Wetness.SetData(wetness, 0, destOffset, count);
        }
    }
}
