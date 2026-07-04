using System.Collections.Generic;
using UnityEngine;

namespace HarmonicEngineV4.Bake
{
    /// <summary>
    /// Spawn zone bake math (spec section 2.3): particle count = f(volume, global density),
    /// even 3D lattice distribution inside a spherical zone. Deterministic - no randomness -
    /// so runs with the same config are bit-identical.
    /// </summary>
    public static class V4SpawnMath
    {
        /// <summary>Lattice spacing for a given number density (particles per cubic meter).</summary>
        public static float SpacingFromDensity(float particlesPerCubicMeter)
        {
            return Mathf.Pow(1f / Mathf.Max(particlesPerCubicMeter, 1e-3f), 1f / 3f);
        }

        /// <summary>Particle radius = half the lattice spacing (particles just touch at rest).</summary>
        public static float ParticleRadiusFromDensity(float particlesPerCubicMeter)
        {
            return SpacingFromDensity(particlesPerCubicMeter) * 0.5f;
        }

        public static float SphereVolume(float radius)
        {
            return 4f / 3f * Mathf.PI * radius * radius * radius;
        }

        /// <summary>Approximate count before lattice generation (volume x density).</summary>
        public static int ExpectedCount(float volume, float particlesPerCubicMeter)
        {
            return Mathf.Max(0, Mathf.RoundToInt(volume * particlesPerCubicMeter));
        }

        /// <summary>
        /// Even cubic-lattice fill of a sphere. The actual generated count converges to
        /// volume x density as the zone grows; every point is strictly inside the sphere.
        /// </summary>
        public static List<Vector3> LatticeFillSphere(Vector3 center, float radius, float particlesPerCubicMeter)
        {
            var points = new List<Vector3>();
            float spacing = SpacingFromDensity(particlesPerCubicMeter);
            int steps = Mathf.CeilToInt(radius / spacing);
            float r2 = radius * radius;

            for (int x = -steps; x <= steps; x++)
            for (int y = -steps; y <= steps; y++)
            for (int z = -steps; z <= steps; z++)
            {
                var offset = new Vector3(x * spacing, y * spacing, z * spacing);
                if (offset.sqrMagnitude <= r2)
                {
                    points.Add(center + offset);
                }
            }

            return points;
        }
    }
}
