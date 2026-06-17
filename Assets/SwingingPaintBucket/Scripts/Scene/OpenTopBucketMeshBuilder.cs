using System.Collections.Generic;
using UnityEngine;

namespace SwingingPaintBucket.Scene
{
    /// <summary>
    /// Builds a hollow open-top cylindrical bucket mesh: floor disk + inner/outer wall shell, no top cap.
    /// </summary>
    public static class OpenTopBucketMeshBuilder
    {
        public static Mesh Create(float outerRadius, float height, float wallThickness, int segments)
        {
            segments = Mathf.Max(16, segments);
            wallThickness = Mathf.Clamp(wallThickness, 0.01f, outerRadius * 0.45f);
            float innerRadius = outerRadius - wallThickness;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            AddFloorDisk(vertices, normals, triangles, innerRadius, segments);

            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                Vector3 outerBottom0 = CylinderPoint(outerRadius, a0, 0f);
                Vector3 outerBottom1 = CylinderPoint(outerRadius, a1, 0f);
                Vector3 outerTop0 = CylinderPoint(outerRadius, a0, height);
                Vector3 outerTop1 = CylinderPoint(outerRadius, a1, height);
                Vector3 innerBottom0 = CylinderPoint(innerRadius, a0, 0f);
                Vector3 innerBottom1 = CylinderPoint(innerRadius, a1, 0f);
                Vector3 innerTop0 = CylinderPoint(innerRadius, a0, height);
                Vector3 innerTop1 = CylinderPoint(innerRadius, a1, height);

                Vector3 outward0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 outward1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                AddQuad(vertices, normals, triangles, outerBottom0, outerBottom1, outerTop1, outerTop0, outward0);
                AddQuad(vertices, normals, triangles, innerTop0, innerTop1, innerBottom1, innerBottom0, -outward0);

                Vector3 up = Vector3.up;
                AddQuad(vertices, normals, triangles, outerTop0, outerTop1, innerTop1, innerTop0, up);
                AddQuad(vertices, normals, triangles, innerBottom0, innerBottom1, outerBottom1, outerBottom0, -up);
            }

            var mesh = new Mesh { name = "OpenTopBucket" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 CylinderPoint(float radius, float angle, float y)
        {
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        private static void AddFloorDisk(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            float radius,
            int segments)
        {
            int centerIndex = vertices.Count;
            vertices.Add(Vector3.zero);
            normals.Add(Vector3.up);

            int ringStart = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                vertices.Add(CylinderPoint(radius, angle, 0f));
                normals.Add(Vector3.up);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = ringStart + i;
                int b = ringStart + ((i + 1) % segments);
                triangles.Add(centerIndex);
                triangles.Add(a);
                triangles.Add(b);
            }
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
