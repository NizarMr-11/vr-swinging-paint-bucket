using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// CPU readback helpers for GPU particle buffer verification (Phase 1+ tests).
    /// </summary>
    public static class GpuParticleReadbackUtility
    {
        public static FluidParticle[] ReadParticles(ComputeBuffer buffer, int count)
        {
            if (buffer == null || count <= 0)
            {
                return System.Array.Empty<FluidParticle>();
            }

            int readCount = math.min(count, buffer.count);
            var particles = new FluidParticle[readCount];
            buffer.GetData(particles, 0, 0, readCount);
            return particles;
        }

        public static bool IsFinite(in FluidParticle particle)
        {
            return IsFiniteFloat3(particle.Position)
                && IsFiniteFloat3(particle.Velocity)
                && float.IsFinite(particle.Density)
                && float.IsFinite(particle.Pressure);
        }

        public static bool IsNearRestDensity(
            in FluidParticle particle,
            float restDensity,
            float toleranceFraction = 0.15f)
        {
            if (!float.IsFinite(particle.Density) || restDensity <= 0f)
            {
                return false;
            }

            float min = restDensity * (1f - toleranceFraction);
            float max = restDensity * (1f + toleranceFraction);
            return particle.Density >= min && particle.Density <= max;
        }

        public static int CountCoreParticles(in FluidParticle[] particles, float3 center, float maxDistance)
        {
            if (particles == null || particles.Length == 0 || maxDistance <= 0f)
            {
                return 0;
            }

            float maxDistanceSq = maxDistance * maxDistance;
            int count = 0;
            for (int i = 0; i < particles.Length; i++)
            {
                if (math.distancesq(particles[i].Position, center) <= maxDistanceSq)
                {
                    count++;
                }
            }

            return count;
        }

        public static bool MatchesSpawnState(
            in FluidParticle particle,
            uint expectedPackedColor,
            float expectedDensity,
            float velocityEpsilon = 1e-4f,
            float densityEpsilon = 1e-3f)
        {
            if (!IsFinite(particle))
            {
                return false;
            }

            if (particle.PackedColorRGBA != expectedPackedColor)
            {
                return false;
            }

            if (math.abs(particle.Density - expectedDensity) > densityEpsilon)
            {
                return false;
            }

            if (math.length(particle.Velocity) > velocityEpsilon)
            {
                return false;
            }

            return math.abs(particle.Pressure) <= densityEpsilon;
        }

        private static bool IsFiniteFloat3(float3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
