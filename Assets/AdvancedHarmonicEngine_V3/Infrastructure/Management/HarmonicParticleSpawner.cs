using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Engine-level spawn facade: samples a <see cref="HarmonicSpawnRegion"/> and appends the
    /// colored particles to the pipeline. This is the single place that turns a declarative
    /// region into GPU particles, so emitters (ParticleRainDirector, ShapeVolumeEmitter, ...)
    /// and direct engine callers all behave consistently.
    /// </summary>
    public static class HarmonicParticleSpawner
    {
        /// <summary>
        /// Samples <paramref name="region"/> and appends the resulting colored particles.
        /// Returns the number actually appended (clamped to remaining capacity).
        /// </summary>
        public static int Spawn(PipelineExecutionController pipeline, HarmonicSpawnRegion region)
        {
            if (pipeline == null || region == null)
            {
                return 0;
            }

            int remaining = pipeline.MaxCapacity - (int)pipeline.GetActiveParticleCount();
            int count = Mathf.Clamp(region.particleCount, 0, Mathf.Max(0, remaining));
            if (count <= 0)
            {
                return 0;
            }

            var positions = new float3[count];
            int sampled = region.SamplePositions(positions, count);
            if (sampled <= 0)
            {
                return 0;
            }

            uint color = FluidParticleFactory.PackColor(region.spawnColor);
            var particles = new FluidParticle[sampled];
            for (int i = 0; i < sampled; i++)
            {
                particles[i] = FluidParticleFactory.FromWorldPosition(
                    (Vector3)positions[i], region.initialVelocity, region.restDensity, color);
            }

            return pipeline.AppendParticles(particles, sampled);
        }
    }
}
