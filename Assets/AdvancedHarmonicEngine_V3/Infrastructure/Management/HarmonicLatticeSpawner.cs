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

        /// <summary>
        /// Uniform lattice inside an open-top cylinder: floorY to fillTopY, horizontal radius limit.
        /// </summary>
        public static int SpawnContainerCylinderFill(
            PipelineExecutionController pipeline,
            Vector3 center,
            float floorY,
            float fillTopY,
            float radius,
            float spacing,
            float restDensity,
            Color spawnColor,
            Vector3 initialVelocity)
        {
            if (pipeline == null || radius <= 0f || spacing <= 0f || fillTopY <= floorY)
            {
                return 0;
            }

            float radiusSq = radius * radius;
            float3 center3 = center;
            uint packedColor = FluidParticleFactory.PackColor(spawnColor);
            int remaining = pipeline.MaxCapacity - (int)pipeline.GetActiveParticleCount();
            if (remaining <= 0)
            {
                return 0;
            }

            int ySteps = Mathf.Max(1, Mathf.FloorToInt((fillTopY - floorY) / spacing) + 1);
            int radialSteps = Mathf.Max(1, Mathf.FloorToInt((2f * radius) / spacing) + 1);
            int capacityGuess = ySteps * radialSteps * radialSteps;
            var particles = new FluidParticle[Mathf.Min(capacityGuess, remaining)];

            int written = 0;
            for (int iy = 0; iy < ySteps && written < remaining; iy++)
            {
                float y = floorY + iy * spacing;
                if (y > fillTopY + spacing * 0.5f)
                {
                    break;
                }

                for (int ix = 0; ix < radialSteps && written < remaining; ix++)
                {
                    float x = center.x - radius + ix * spacing;
                    for (int iz = 0; iz < radialSteps && written < remaining; iz++)
                    {
                        float z = center.z - radius + iz * spacing;
                        float2 offset = new float2(x - center.x, z - center.z);
                        if (math.lengthsq(offset) > radiusSq)
                        {
                            continue;
                        }

                        particles[written++] = FluidParticleFactory.FromWorldPosition(
                            new Vector3(x, y, z),
                            initialVelocity,
                            restDensity,
                            packedColor);
                    }
                }
            }

            if (written == 0)
            {
                return 0;
            }

            return pipeline.AppendParticles(particles, written);
        }
    }
}
