using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Spawns particles on a regular 3D grid for reproducible SPH / spatial-hash verification.
    /// </summary>
    public static class HarmonicLatticeSpawner
    {
        public static int SpawnLattice(PipelineExecutionController pipeline, HarmonicLatticeSpawnSettings settings)
        {
            if (pipeline == null || settings == null)
            {
                return 0;
            }

            int nx = math.max(0, settings.gridDimensions.x);
            int ny = math.max(0, settings.gridDimensions.y);
            int nz = math.max(0, settings.gridDimensions.z);
            int total = nx * ny * nz;
            if (total <= 0)
            {
                return 0;
            }

            int remaining = pipeline.MaxCapacity - (int)pipeline.GetActiveParticleCount();
            int count = math.min(total, math.max(0, remaining));
            if (count <= 0)
            {
                return 0;
            }

            float spacing = settings.spacing > 0f ? settings.spacing : pipeline.CellSize;
            float3 center = settings.center;
            float3 origin = center - new float3(nx - 1, ny - 1, nz - 1) * spacing * 0.5f;
            uint packedColor = FluidParticleFactory.PackColor(settings.spawnColor);
            var particles = new FluidParticle[count];

            int written = 0;
            for (int z = 0; z < nz && written < count; z++)
            {
                for (int y = 0; y < ny && written < count; y++)
                {
                    for (int x = 0; x < nx && written < count; x++)
                    {
                        float3 pos = origin + new float3(x, y, z) * spacing;
                        particles[written++] = FluidParticleFactory.FromWorldPosition(
                            pos,
                            settings.initialVelocity,
                            settings.restDensity,
                            packedColor);
                    }
                }
            }

            return pipeline.AppendParticles(particles, written);
        }
    }
}
