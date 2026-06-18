using System;
using HarmonicEngine.Domain.Adapters;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Declarative "where (and what color) to spawn particles" description handed to the engine.
    /// A volume (box / sphere / capsule / mesh) is uniformly filled and every particle gets the
    /// region's <see cref="spawnColor"/>. Queue several regions with different colors to get
    /// multiple colored fluid bodies that interact once the simulation runs.
    /// </summary>
    [Serializable]
    public class HarmonicSpawnRegion
    {
        [Header("Shape")]
        public ShapeVolumeType shape = ShapeVolumeType.Sphere;
        public Vector3 center = Vector3.zero;
        public Quaternion rotation = Quaternion.identity;

        [Header("Primitive size")]
        public Vector3 boxSize = Vector3.one;
        public float sphereRadius = 0.5f;
        public float capsuleRadius = 0.5f;
        public float capsuleHeight = 2f;

        [Header("Mesh volume (shape == Mesh)")]
        public Mesh mesh;
        public Matrix4x4 meshToWorld = Matrix4x4.identity;
        [Min(1)] public int meshMaxAttemptsPerPoint = 32;

        [Header("Particles")]
        [Min(0)] public int particleCount = 4096;
        public float restDensity = 1000f;
        public Vector3 initialVelocity = Vector3.zero;
        public Color spawnColor = Color.white;
        public uint seed = 12345u;

        /// <summary>Samples up to <paramref name="count"/> world-space positions into the buffer; returns the number written.</summary>
        public int SamplePositions(float3[] outPositions, int count)
        {
            if (outPositions == null || count <= 0)
            {
                return 0;
            }

            switch (shape)
            {
                case ShapeVolumeType.Box:
                    return ShapeVolumeSampler.SampleBox(center, boxSize, rotation, count, seed, outPositions);
                case ShapeVolumeType.Sphere:
                    return ShapeVolumeSampler.SampleSphere(center, sphereRadius, count, seed, outPositions);
                case ShapeVolumeType.Capsule:
                    return ShapeVolumeSampler.SampleCapsule(center, capsuleRadius, capsuleHeight, rotation, count, seed, outPositions);
                case ShapeVolumeType.Mesh:
                    return SampleMesh(outPositions, count);
                default:
                    return 0;
            }
        }

        private int SampleMesh(float3[] outPositions, int count)
        {
            if (mesh == null)
            {
                Debug.LogWarning("[HarmonicSpawnRegion] Mesh shape selected but no mesh assigned.");
                return 0;
            }

            Vector3[] localVerts = mesh.vertices;
            int[] tris = mesh.triangles;
            if (localVerts.Length == 0 || tris.Length == 0)
            {
                return 0;
            }

            var worldVerts = new Vector3[localVerts.Length];
            var worldBounds = new Bounds(meshToWorld.MultiplyPoint3x4(localVerts[0]), Vector3.zero);
            for (int i = 0; i < localVerts.Length; i++)
            {
                worldVerts[i] = meshToWorld.MultiplyPoint3x4(localVerts[i]);
                worldBounds.Encapsulate(worldVerts[i]);
            }

            return MeshVolumeSampler.SampleInsideMesh(
                worldBounds, worldVerts, tris, count, seed, meshMaxAttemptsPerPoint, outPositions);
        }
    }
}
