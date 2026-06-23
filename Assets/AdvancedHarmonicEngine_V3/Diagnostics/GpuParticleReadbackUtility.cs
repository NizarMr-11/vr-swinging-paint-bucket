using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// CPU readback helpers for GPU particle buffer verification (Phase 1+ tests).
    /// </summary>
    public static class GpuParticleReadbackUtility
    {
        public static FluidParticle[] ReadParticles(ParticleSoaBuffers soa, int count)
        {
            if (soa == null || count <= 0)
            {
                return System.Array.Empty<FluidParticle>();
            }

            int readCount = math.min(count, soa.Block0.count);
            var block0 = new Vector4[readCount];
            var block1 = new Vector4[readCount];
            var colors = new uint[readCount];
            var wetness = new float[readCount];

            soa.Block0.GetData(block0, 0, 0, readCount);
            soa.Block1.GetData(block1, 0, 0, readCount);
            soa.PackedColors.GetData(colors, 0, 0, readCount);
            soa.Wetness.GetData(wetness, 0, 0, readCount);

            var particles = new FluidParticle[readCount];
            for (int i = 0; i < readCount; i++)
            {
                particles[i] = new FluidParticle
                {
                    Position = new float3(block0[i].x, block0[i].y, block0[i].z),
                    Velocity = new float3(block1[i].x, block1[i].y, block1[i].z),
                    Density = block0[i].w,
                    Pressure = block1[i].w,
                    PackedColorRGBA = colors[i],
                    _Padding = new float3(wetness[i], 0f, 0f)
                };
            }

            return particles;
        }

        public static FluidParticle[] ReadDensityCache(ComputeBuffer densities, ComputeBuffer pressures, int count)
        {
            if (densities == null || pressures == null || count <= 0)
            {
                return System.Array.Empty<FluidParticle>();
            }

            int readCount = math.min(count, math.min(densities.count, pressures.count));
            var densityData = new float[readCount];
            var pressureData = new float[readCount];
            densities.GetData(densityData, 0, 0, readCount);
            pressures.GetData(pressureData, 0, 0, readCount);

            var particles = new FluidParticle[readCount];
            for (int i = 0; i < readCount; i++)
            {
                particles[i] = new FluidParticle
                {
                    Density = densityData[i],
                    Pressure = pressureData[i]
                };
            }

            return particles;
        }

        public static FluidParticle[] ReadParticlesAtIndices(ParticleSoaBuffers soa, int[] indices)
        {
            if (soa == null || indices == null || indices.Length == 0)
            {
                return System.Array.Empty<FluidParticle>();
            }

            var block0 = new Vector4[1];
            var block1 = new Vector4[1];
            var particles = new FluidParticle[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= soa.Block0.count)
                {
                    continue;
                }

                soa.Block0.GetData(block0, 0, idx, 1);
                soa.Block1.GetData(block1, 0, idx, 1);
                particles[i] = new FluidParticle
                {
                    Position = new float3(block0[0].x, block0[0].y, block0[0].z),
                    Velocity = new float3(block1[0].x, block1[0].y, block1[0].z),
                    Density = block0[0].w,
                    Pressure = block1[0].w
                };
            }

            return particles;
        }

        public static float[] ReadFloatBufferSample(ComputeBuffer buffer, int[] indices)
        {
            if (buffer == null || indices == null || indices.Length == 0)
            {
                return System.Array.Empty<float>();
            }

            var result = new float[indices.Length];
            var single = new float[1];
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= buffer.count)
                {
                    continue;
                }

                buffer.GetData(single, 0, idx, 1);
                result[i] = single[0];
            }

            return result;
        }

        public readonly struct ScalarStats
        {
            public readonly float Min;
            public readonly float Max;
            public readonly float Avg;

            public ScalarStats(float min, float max, float avg)
            {
                Min = min;
                Max = max;
                Avg = avg;
            }
        }

        public static ScalarStats ComputeScalarStats(float[] values, bool useAbs = false)
        {
            if (values == null || values.Length == 0)
            {
                return new ScalarStats(0f, 0f, 0f);
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float v = useAbs ? math.abs(values[i]) : values[i];
                if (!float.IsFinite(v))
                {
                    continue;
                }

                min = math.min(min, v);
                max = math.max(max, v);
                sum += v;
                count++;
            }

            if (count == 0)
            {
                return new ScalarStats(0f, 0f, 0f);
            }

            return new ScalarStats(min, max, sum / count);
        }

        public static ScalarStats ComputePbfConstraintStats(float[] densities, float restDensity)
        {
            if (densities == null || densities.Length == 0 || restDensity <= 0f)
            {
                return new ScalarStats(0f, 0f, 0f);
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < densities.Length; i++)
            {
                if (!float.IsFinite(densities[i]))
                {
                    continue;
                }

                float constraint = densities[i] / restDensity - 1f;
                min = math.min(min, constraint);
                max = math.max(max, constraint);
                sum += constraint;
                count++;
            }

            if (count == 0)
            {
                return new ScalarStats(0f, 0f, 0f);
            }

            return new ScalarStats(min, max, sum / count);
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
