using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        public bool TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint count)
        {
            soa = _pingPong?.ReadSet;
            count = _cachedInternalCount;
            return soa != null;
        }

        public bool TryGetFallingParticleSoa(out ParticleSoaBuffers soa, out uint count) =>
            TryGetInternalParticleSoa(out soa, out count);

        public bool TryGetDensityCacheBuffers(out ComputeBuffer densities, out ComputeBuffer pressures, out uint count)
        {
            densities = _bufferDensityCacheDensities;
            pressures = _bufferDensityCachePressures;
            count = _cachedInternalCount;
            return densities != null && pressures != null;
        }

        private int[] _stratifiedSampleIndices;

        private static int[] BuildStratifiedSampleIndices(int activeCount, int sampleCount)
        {
            sampleCount = Mathf.Min(sampleCount, activeCount);
            if (sampleCount <= 0)
            {
                return System.Array.Empty<int>();
            }

            if (sampleCount == 1)
            {
                return new[] { 0 };
            }

            var indices = new int[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                indices[i] = i * (activeCount - 1) / (sampleCount - 1);
            }

            return indices;
        }

        private void MaybeSampleParticlePositionsInternal(ParticleSoaBuffers soa, uint activeCount, string stage)
        {
            if (positionSampleInterval <= 0 || soa == null || activeCount == 0)
            {
                return;
            }

            if (++_framesSincePositionSample < positionSampleInterval)
            {
                return;
            }

            _framesSincePositionSample = 0;

            int sampleCount = Mathf.Min(positionSampleCount, (int)activeCount);
            if (sampleCount <= 0)
            {
                return;
            }

            if (_diagSampleBuffer == null || _diagSampleBuffer.Length < sampleCount)
            {
                _diagSampleBuffer = new FluidParticle[Mathf.Max(sampleCount, positionSampleCount)];
            }

            int[] indices = BuildStratifiedSampleIndices((int)activeCount, sampleCount);
            if (_stratifiedSampleIndices == null || _stratifiedSampleIndices.Length < indices.Length)
            {
                _stratifiedSampleIndices = new int[Mathf.Max(indices.Length, positionSampleCount)];
            }

            System.Array.Copy(indices, _stratifiedSampleIndices, indices.Length);
            FluidParticle[] sampled = GpuParticleReadbackUtility.ReadParticlesAtIndices(soa, indices);
            System.Array.Copy(sampled, _diagSampleBuffer, sampleCount);

            float minY = float.MaxValue, maxY = float.MinValue, sumY = 0f, sumSpeed = 0f, maxSpeed = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float y = _diagSampleBuffer[i].Position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                sumY += y;
                float speed = math.length(_diagSampleBuffer[i].Velocity);
                sumSpeed += speed;
                maxSpeed = Mathf.Max(maxSpeed, speed);
            }

            float decayed = Mathf.Lerp(_estimatedMaxSpeed, maxSpeed, 0.3f);
            _estimatedMaxSpeed = Mathf.Max(decayed, 5f);

            if (perfDiagnosticsMuted
                || !HarmonicDiagnosticHub.Enabled
                || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            float3 p0 = _diagSampleBuffer[0].Position;
            PublishStageDiagnosticInternal(
                $"{stage}.sample",
                $"n={sampleCount} minY={minY:F2} maxY={maxY:F2} avgY={(sumY / sampleCount):F2} " +
                $"avgSpeed={(sumSpeed / sampleCount):F2} p0=({p0.x:F2},{p0.y:F2},{p0.z:F2})");

            if (stage == "containerPbf")
            {
                StorePbfColumnSample(minY, maxY, sumY / sampleCount, sumSpeed / sampleCount, maxSpeed);
            }
        }

        private void SeedTestParticlesIfEmpty()
        {
            uint count = FetchActiveCount(_soaInternalA);
            if (count > 0)
            {
                return;
            }

            int spawnCount = Mathf.Clamp(testParticleCount, 1, maxCapacity);
            _seedParticles ??= new FluidParticle[spawnCount];
            if (_seedParticles.Length < spawnCount)
            {
                _seedParticles = new FluidParticle[spawnCount];
            }

            var rng = new Unity.Mathematics.Random(0xC0FFEEu);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 offset = rng.NextFloat3Direction() * rng.NextFloat(0f, testSpawnRadius);
                _seedParticles[i] = new FluidParticle
                {
                    Position = offset,
                    Velocity = float3.zero,
                    Density = sphSolver.RestDensity,
                    Pressure = 0f,
                    PackedColorRGBA = 0xFFFFFFFFu
                };
            }

            ParticleSoaWriteUtility.WriteParticles(_soaInternalA, _seedParticles, 0, 0, spawnCount);
            _soaInternalA.SetCounterValue((uint)spawnCount);
            _soaInternalB.SetCounterValue(0);
            _cachedInternalCount = (uint)spawnCount;
            RecordRunSpawnInfo(new HarmonicRunSpawnInfo
            {
                method = "seedTest",
                spawnCount = spawnCount,
                spacing = testSpawnRadius
            });
        }
    }
}
