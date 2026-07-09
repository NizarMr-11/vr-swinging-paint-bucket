using System.Collections.Generic;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Rendering
{
    /// <summary>
    /// Procedural visual mesh for a V4Bucket: inner/outer wall, rim, floor and bottom,
    /// with the authored holes cut out of the surface so they are visible at runtime.
    /// Purely cosmetic - collision and zones still come from the analytic geometry.
    /// Rendered semi-transparent so the fluid inside stays visible.
    /// </summary>
    [RequireComponent(typeof(V4Bucket))]
    [DisallowMultipleComponent]
    public sealed class V4BucketMeshRenderer : MonoBehaviour
    {
        [Range(24, 256)] public int radialSegments = 128;
        [Range(4, 64)] public int heightSegments = 24;
        [Range(4, 64)] public int discRings = 24;
        [Range(0.05f, 1f)] public float alpha = 0.45f;
        public Color bucketColor = new Color(0.75f, 0.78f, 0.82f, 1f);

        private V4Bucket _bucket;
        private V4PipelineRoot _pipeline;
        private GameObject _meshGo;
        private Mesh _mesh;
        private Material _material;
        private MaterialPropertyBlock _propertyBlock;
        private static readonly int WorldOriginId = Shader.PropertyToID("_V4BucketWorldOrigin");
        private static readonly int WorldRotationId = Shader.PropertyToID("_V4BucketWorldRotation");

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();

        private void Start()
        {
            _bucket = GetComponent<V4Bucket>();
            _pipeline = GetComponentInParent<V4PipelineRoot>();
            if (_pipeline == null)
            {
                _pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            _material = CreateMaterial();
            _mesh = BuildMesh();
            _propertyBlock = new MaterialPropertyBlock();

            _meshGo = new GameObject("V4BucketVisual");
            _meshGo.transform.SetParent(transform, false);
            _meshGo.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = _meshGo.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void LateUpdate()
        {
            if (_meshGo == null || _propertyBlock == null)
            {
                return;
            }

            V4GpuBucketDriver driver = _pipeline != null ? _pipeline.GpuBucketDriver : null;
            if (driver != null && driver.IsInitialized)
            {
                Quaternion rotation = transform.rotation;
                _propertyBlock.SetVector(WorldOriginId, transform.position);
                _propertyBlock.SetVector(
                    WorldRotationId,
                    new Vector4(rotation.x, rotation.y, rotation.z, rotation.w));
                _meshGo.GetComponent<MeshRenderer>().SetPropertyBlock(_propertyBlock);
                return;
            }

            _meshGo.transform.localPosition = Vector3.zero;
            _meshGo.transform.localRotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (_meshGo != null)
            {
                Destroy(_meshGo);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private Material CreateMaterial()
        {
            var material = new Material(Shader.Find("Standard")) { name = "V4BucketVisual" };
            Color c = bucketColor;
            c.a = alpha;
            material.color = c;

            // Standard shader transparent setup.
            material.SetFloat("_Mode", 3f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        // -------------------------------------------------------------------
        // Mesh construction. Bucket-local space: floor inside surface at y=0,
        // open rim at y=height, wall band R..R+t, floor slab -t..0.
        // -------------------------------------------------------------------

        private Mesh BuildMesh()
        {
            _vertices.Clear();
            _normals.Clear();
            _triangles.Clear();

            float r = _bucket.innerRadius;
            float t = _bucket.wallThickness;
            float h = _bucket.height;

            // Walls (inner surface faces inward, outer faces outward).
            BuildWall(r, 0f, h, inward: true);
            BuildWall(r + t, -t, h, inward: false);

            // Rim ring at the open top.
            BuildFlatRing(h, r, r + t, Vector3.up, holeCutouts: false);

            // Floor (inside, faces up) and bottom (outside, faces down) with hole cutouts.
            BuildDisc(0f, r, Vector3.up);
            BuildDisc(-t, r + t, Vector3.down);

            // Double-sided: append every triangle with reversed winding so the bucket
            // is visible from inside and outside (the Standard shader is back-culled).
            int triCount = _triangles.Count;
            for (int i = 0; i < triCount; i += 3)
            {
                _triangles.Add(_triangles[i]);
                _triangles.Add(_triangles[i + 2]);
                _triangles.Add(_triangles[i + 1]);
            }

            var mesh = new Mesh { name = "V4BucketVisual" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private bool IsInsideAnyHole(Vector3 localPoint)
        {
            List<Bake.V4HoleDef> holes = _bucket.holes;
            for (int i = 0; i < holes.Count; i++)
            {
                if (Vector3.Distance(localPoint, holes[i].localPosition) <= holes[i].radius)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildWall(float radius, float yBottom, float yTop, bool inward)
        {
            for (int s = 0; s < radialSegments; s++)
            {
                float a0 = s * Mathf.PI * 2f / radialSegments;
                float a1 = (s + 1) * Mathf.PI * 2f / radialSegments;

                for (int v = 0; v < heightSegments; v++)
                {
                    float y0 = Mathf.Lerp(yBottom, yTop, (float)v / heightSegments);
                    float y1 = Mathf.Lerp(yBottom, yTop, (float)(v + 1) / heightSegments);

                    Vector3 center = new Vector3(
                        Mathf.Cos((a0 + a1) * 0.5f) * radius,
                        (y0 + y1) * 0.5f,
                        Mathf.Sin((a0 + a1) * 0.5f) * radius);
                    if (IsInsideAnyHole(center))
                    {
                        continue;
                    }

                    Vector3 p00 = new Vector3(Mathf.Cos(a0) * radius, y0, Mathf.Sin(a0) * radius);
                    Vector3 p10 = new Vector3(Mathf.Cos(a1) * radius, y0, Mathf.Sin(a1) * radius);
                    Vector3 p01 = new Vector3(Mathf.Cos(a0) * radius, y1, Mathf.Sin(a0) * radius);
                    Vector3 p11 = new Vector3(Mathf.Cos(a1) * radius, y1, Mathf.Sin(a1) * radius);

                    Vector3 n0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 n1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    if (inward)
                    {
                        n0 = -n0;
                        n1 = -n1;
                    }

                    AddQuad(p00, p10, p01, p11, n0, n1, n0, n1, flip: inward);
                }
            }
        }

        private void BuildFlatRing(float y, float rInner, float rOuter, Vector3 normal, bool holeCutouts)
        {
            for (int s = 0; s < radialSegments; s++)
            {
                float a0 = s * Mathf.PI * 2f / radialSegments;
                float a1 = (s + 1) * Mathf.PI * 2f / radialSegments;

                Vector3 c0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                Vector3 c1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                Vector3 p00 = c0 * rInner + Vector3.up * y;
                Vector3 p10 = c1 * rInner + Vector3.up * y;
                Vector3 p01 = c0 * rOuter + Vector3.up * y;
                Vector3 p11 = c1 * rOuter + Vector3.up * y;

                if (holeCutouts && IsInsideAnyHole((p00 + p11) * 0.5f))
                {
                    continue;
                }

                AddQuad(p00, p10, p01, p11, normal, normal, normal, normal, flip: normal.y < 0f);
            }
        }

        private void BuildDisc(float y, float radius, Vector3 normal)
        {
            for (int ring = 0; ring < discRings; ring++)
            {
                float r0 = radius * ring / discRings;
                float r1 = radius * (ring + 1) / discRings;

                for (int s = 0; s < radialSegments; s++)
                {
                    float a0 = s * Mathf.PI * 2f / radialSegments;
                    float a1 = (s + 1) * Mathf.PI * 2f / radialSegments;

                    Vector3 c0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    Vector3 c1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

                    Vector3 center = new Vector3(
                        Mathf.Cos((a0 + a1) * 0.5f) * (r0 + r1) * 0.5f,
                        y,
                        Mathf.Sin((a0 + a1) * 0.5f) * (r0 + r1) * 0.5f);
                    // Holes pierce the whole floor slab: test against the XZ footprint so
                    // both the y=0 floor and the y=-t bottom get the cutout.
                    Vector3 footprint = new Vector3(center.x, 0f, center.z);
                    if (IsInsideAnyHoleXZ(footprint))
                    {
                        continue;
                    }

                    Vector3 p00 = c0 * r0 + Vector3.up * y;
                    Vector3 p10 = c1 * r0 + Vector3.up * y;
                    Vector3 p01 = c0 * r1 + Vector3.up * y;
                    Vector3 p11 = c1 * r1 + Vector3.up * y;

                    AddQuad(p00, p10, p01, p11, normal, normal, normal, normal, flip: normal.y < 0f);
                }
            }
        }

        private bool IsInsideAnyHoleXZ(Vector3 footprint)
        {
            List<Bake.V4HoleDef> holes = _bucket.holes;
            for (int i = 0; i < holes.Count; i++)
            {
                // Only bottom holes (normal pointing mostly down) cut the floor discs.
                if (holes[i].outwardNormal.y > -0.5f)
                {
                    continue;
                }

                Vector3 hp = holes[i].localPosition;
                float dx = footprint.x - hp.x;
                float dz = footprint.z - hp.z;
                if (dx * dx + dz * dz <= holes[i].radius * holes[i].radius)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddQuad(
            Vector3 p00, Vector3 p10, Vector3 p01, Vector3 p11,
            Vector3 n00, Vector3 n10, Vector3 n01, Vector3 n11,
            bool flip)
        {
            int baseIndex = _vertices.Count;
            _vertices.Add(p00);
            _vertices.Add(p10);
            _vertices.Add(p01);
            _vertices.Add(p11);
            _normals.Add(n00);
            _normals.Add(n10);
            _normals.Add(n01);
            _normals.Add(n11);

            if (flip)
            {
                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 3);
            }
            else
            {
                _triangles.Add(baseIndex);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 2);
                _triangles.Add(baseIndex + 1);
                _triangles.Add(baseIndex + 3);
                _triangles.Add(baseIndex + 2);
            }
        }
    }
}
