using HarmonicEngine.Domain.Adapters;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class MeshVolumeSamplerTests
    {
        private static void BuildUnitCube(out Vector3[] verts, out int[] tris)
        {
            // Axis-aligned cube spanning -0.5..0.5 with outward-facing (CCW) triangles.
            verts = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), // 0
                new Vector3( 0.5f, -0.5f, -0.5f), // 1
                new Vector3( 0.5f,  0.5f, -0.5f), // 2
                new Vector3(-0.5f,  0.5f, -0.5f), // 3
                new Vector3(-0.5f, -0.5f,  0.5f), // 4
                new Vector3( 0.5f, -0.5f,  0.5f), // 5
                new Vector3( 0.5f,  0.5f,  0.5f), // 6
                new Vector3(-0.5f,  0.5f,  0.5f), // 7
            };

            tris = new[]
            {
                0, 2, 1, 0, 3, 2, // -Z
                4, 5, 6, 4, 6, 7, // +Z
                0, 1, 5, 0, 5, 4, // -Y
                3, 7, 6, 3, 6, 2, // +Y
                0, 4, 7, 0, 7, 3, // -X
                1, 2, 6, 1, 6, 5, // +X
            };
        }

        [Test]
        public void IsPointInsideMesh_CenterIsInside()
        {
            BuildUnitCube(out Vector3[] verts, out int[] tris);
            Assert.IsTrue(MeshVolumeSampler.IsPointInsideMesh(new float3(0f, 0f, 0f), verts, tris));
        }

        [Test]
        public void IsPointInsideMesh_FarPointIsOutside()
        {
            BuildUnitCube(out Vector3[] verts, out int[] tris);
            Assert.IsFalse(MeshVolumeSampler.IsPointInsideMesh(new float3(5f, 5f, 5f), verts, tris));
            Assert.IsFalse(MeshVolumeSampler.IsPointInsideMesh(new float3(0f, 0f, 2f), verts, tris));
        }

        [Test]
        public void SampleInsideMesh_AllPointsWithinCube()
        {
            BuildUnitCube(out Vector3[] verts, out int[] tris);
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var outPositions = new float3[256];

            int n = MeshVolumeSampler.SampleInsideMesh(bounds, verts, tris, 256, 314u, 64, outPositions);

            Assert.Greater(n, 0);
            for (int i = 0; i < n; i++)
            {
                float3 p = outPositions[i];
                Assert.LessOrEqual(math.abs(p.x), 0.5f + 1e-3f);
                Assert.LessOrEqual(math.abs(p.y), 0.5f + 1e-3f);
                Assert.LessOrEqual(math.abs(p.z), 0.5f + 1e-3f);
                Assert.IsTrue(MeshVolumeSampler.IsPointInsideMesh(p, verts, tris));
            }
        }
    }
}
