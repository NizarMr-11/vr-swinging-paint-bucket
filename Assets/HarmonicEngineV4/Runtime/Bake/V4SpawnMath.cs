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
        /// Even cubic-lattice fill of a vertical cylinder (bucket-local: floor center origin, +Y up).
        /// Same algorithm as <see cref="LatticeFillSphere"/>: integer spacing grid, then shape filter.
        /// </summary>
        public static List<Vector3> LatticeFillCylinder(
            float innerRadius,
            float yMin,
            float yMax,
            float particlesPerCubicMeter)
        {
            var points = new List<Vector3>();
            if (innerRadius <= 1e-6f || yMax <= yMin + 1e-6f)
            {
                return points;
            }

            float spacing = SpacingFromDensity(particlesPerCubicMeter);
            int stepsR = Mathf.CeilToInt(innerRadius / spacing);
            int stepsY = Mathf.CeilToInt((yMax - yMin) / spacing);
            float r2 = innerRadius * innerRadius;

            for (int x = -stepsR; x <= stepsR; x++)
            for (int yi = 0; yi <= stepsY; yi++)
            for (int z = -stepsR; z <= stepsR; z++)
            {
                var local = new Vector3(x * spacing, yMin + yi * spacing, z * spacing);
                if (local.y > yMax + 1e-5f)
                {
                    continue;
                }

                float radial2 = local.x * local.x + local.z * local.z;
                if (radial2 <= r2)
                {
                    points.Add(local);
                }
            }

            return points;
        }

        /// <summary>Backward-compatible alias.</summary>
        public static List<Vector3> LatticeFillCylinderSlab(
            float innerRadius,
            float yMin,
            float yMax,
            float particlesPerCubicMeter) =>
            LatticeFillCylinder(innerRadius, yMin, yMax, particlesPerCubicMeter);

        public static float CylinderSlabVolume(float innerRadius, float yMin, float yMax)
        {
            float h = Mathf.Max(0f, yMax - yMin);
            return Mathf.PI * innerRadius * innerRadius * h;
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
