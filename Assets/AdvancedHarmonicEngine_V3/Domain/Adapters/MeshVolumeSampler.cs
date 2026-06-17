using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Domain.Adapters
{
    /// <summary>
    /// Fills an arbitrary triangle mesh volume with uniformly distributed points using
    /// rejection sampling inside the mesh bounds plus a robust ray-parity point-in-mesh test.
    /// Vertices/triangles are expected in the space the bounds and output should be in
    /// (typically world space after transforming the mesh).
    /// </summary>
    public static class MeshVolumeSampler
    {
        /// <summary>
        /// Ray-parity containment test (Möller–Trumbore). Casts a ray along +X and counts triangle
        /// crossings; an odd count means the point is inside the closed mesh.
        /// </summary>
        public static bool IsPointInsideMesh(float3 point, Vector3[] vertices, int[] triangles)
        {
            if (vertices == null || triangles == null || triangles.Length < 3)
            {
                return false;
            }

            // Skewed (non-axis-aligned) direction so the ray does not graze shared triangle
            // edges/diagonals on axis-aligned meshes, which would corrupt the parity count.
            float3 dir = math.normalize(new float3(0.5773f, 0.5774f, 0.5775f));
            int crossings = 0;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                float3 v0 = vertices[triangles[t]];
                float3 v1 = vertices[triangles[t + 1]];
                float3 v2 = vertices[triangles[t + 2]];
                if (RayHitsTriangle(point, dir, v0, v1, v2))
                {
                    crossings++;
                }
            }

            return (crossings & 1) == 1;
        }

        /// <summary>
        /// Rejection-samples <paramref name="count"/> points inside the mesh volume. Returns the number
        /// of points actually written (may be fewer than requested if attempts are exhausted).
        /// </summary>
        public static int SampleInsideMesh(
            Bounds bounds,
            Vector3[] vertices,
            int[] triangles,
            int count,
            uint seed,
            int maxAttemptsPerPoint,
            float3[] outPositions)
        {
            if (outPositions == null || vertices == null || triangles == null)
            {
                return 0;
            }

            int target = math.min(math.max(count, 0), outPositions.Length);
            int attemptsBudget = math.max(1, maxAttemptsPerPoint) * math.max(1, target);
            var rng = new Unity.Mathematics.Random(seed == 0u ? 0x9E3779B9u : seed);

            float3 min = bounds.min;
            float3 max = bounds.max;
            int written = 0;
            int attempts = 0;
            while (written < target && attempts < attemptsBudget)
            {
                attempts++;
                float3 candidate = new float3(
                    rng.NextFloat(min.x, max.x),
                    rng.NextFloat(min.y, max.y),
                    rng.NextFloat(min.z, max.z));

                if (IsPointInsideMesh(candidate, vertices, triangles))
                {
                    outPositions[written++] = candidate;
                }
            }

            return written;
        }

        private static bool RayHitsTriangle(float3 origin, float3 dir, float3 v0, float3 v1, float3 v2)
        {
            const float epsilon = 1e-7f;
            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;
            float3 h = math.cross(dir, edge2);
            float a = math.dot(edge1, h);
            if (a > -epsilon && a < epsilon)
            {
                return false;
            }

            float f = 1f / a;
            float3 s = origin - v0;
            float u = f * math.dot(s, h);
            if (u < 0f || u > 1f)
            {
                return false;
            }

            float3 q = math.cross(s, edge1);
            float v = f * math.dot(dir, q);
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            float tHit = f * math.dot(edge2, q);
            return tHit > epsilon;
        }
    }
}
