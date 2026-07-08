using UnityEngine;

namespace SwingingPaintBucket.Bucket
{
    /// <summary>
    /// Builds a simple open-top pail mesh at runtime (and in the Editor) so the bucket has an
    /// actual bucket-like shape instead of a placeholder primitive. No external 3D model is
    /// required: the body is a tapered cylinder (frustum), with a bottom cap, a small nozzle
    /// spout underneath (matching ParticleEmitter's LocalNozzleOffset), and a curved handle.
    ///
    /// Attach this to the same child object that used to hold the placeholder Cube mesh
    /// (e.g. "BucketMesh"), as a child of the object that has BucketController/PendulumSimulator.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(LineRenderer))]
    public class BucketMeshGenerator : MonoBehaviour
    {
        [Header("Bucket Shape")]
        [Tooltip("Radius at the open top rim, in metres.")]
        [Range(0.05f, 0.5f)] public float TopRadius = 0.18f;

        [Tooltip("Radius at the closed bottom, in metres. Slightly smaller than the top for a classic pail taper.")]
        [Range(0.03f, 0.45f)] public float BottomRadius = 0.13f;

        [Tooltip("Height of the bucket body, in metres.")]
        [Range(0.1f, 1f)] public float Height = 0.5f;

        [Tooltip("How far below this object's origin the bottom of the bucket sits. Should line up with " +
                 "ParticleEmitter's Local Nozzle Offset so the spout looks like it's actually emitting the paint.")]
        public float TopY = 0f;

        [Range(8, 64)] public int RadialSegments = 24;

        [Header("Spout / Nozzle")]
        public bool ShowSpout = true;
        [Range(0.005f, 0.05f)] public float SpoutRadius = 0.015f;
        [Range(0.01f, 0.1f)] public float SpoutLength = 0.05f;

        [Header("Handle")]
        public bool ShowHandle = true;
        [Range(0.01f, 0.05f)] public float HandleWidth = 0.02f;
        [Range(0.1f, 0.4f)] public float HandleArcHeight = 0.18f;

        [Header("Live Paint Color (optional)")]
        [Tooltip("If assigned, the bucket's interior/body tint follows the bucket's current paint color.")]
        public BucketController LinkedBucket;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private LineRenderer _handleRenderer;
        private Material _runtimeMaterial;

        private void Awake()
        {
            try
            {
                Build();
                Debug.Log($"[BucketMeshGenerator] Bucket mesh built successfully on '{gameObject.name}'.", this);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BucketMeshGenerator] Failed to build bucket mesh on '{gameObject.name}': {ex}", this);
            }
        }

        private void OnValidate()
        {
            // Guard against running mesh generation on prefab assets that aren't part of a loaded scene.
            if (!gameObject.scene.IsValid())
                return;

            // IMPORTANT: OnValidate must never create/destroy/reparent GameObjects or Components —
            // Unity explicitly disallows that here (it produced the "DestroyImmediate ... not permitted"
            // and "SendMessage cannot be called during OnValidate" errors, plus duplicate BucketSpout
            // objects). Everything below only WRITES PROPERTIES on components that already exist
            // (mesh data, material color, LineRenderer positions/material) which is safe. Only the
            // spout — which calls CreatePrimitive/Destroy — is restricted to Awake/Play mode.
            BuildMeshMaterialAndHandle();
        }

        private void Update()
        {
            if (LinkedBucket == null || _runtimeMaterial == null)
                return;

            _runtimeMaterial.color = LinkedBucket.CurrentPaintColor;
        }

        /// <summary>
        /// Full build including the spout GameObject. Only ever call this at runtime (Awake/Start) —
        /// never from OnValidate, which is not allowed to create/destroy objects.
        /// </summary>
        public void Build()
        {
            float bottomY = BuildMeshMaterialAndHandle();
            if (float.IsNaN(bottomY))
                return; // BuildMeshMaterialAndHandle already logged why

            BuildSpout(bottomY);
        }

        /// <summary>
        /// Rebuilds the mesh, material and handle LineRenderer. Every operation here only sets
        /// properties on components that are guaranteed to already exist (via RequireComponent),
        /// so this is safe to call from OnValidate in the Editor as well as from Awake at runtime.
        /// Returns the bottom-of-bucket Y (for the spout) or NaN if something's missing.
        /// </summary>
        private float BuildMeshMaterialAndHandle()
        {
            CacheComponents();

            if (_meshFilter == null || _meshRenderer == null)
            {
                Debug.LogError($"[BucketMeshGenerator] '{gameObject.name}' is missing a MeshFilter/MeshRenderer — " +
                                "make sure this script is on the object that actually shows the bucket " +
                                "(e.g. 'BucketMesh'), not its parent.", this);
                return float.NaN;
            }

            BottomRadius = Mathf.Min(BottomRadius, TopRadius);

            Mesh mesh = new Mesh { name = "GeneratedBucketMesh" };
            float bottomY = BuildBody(mesh);
            _meshFilter.sharedMesh = mesh;

            if (_meshRenderer.sharedMaterial == null || _meshRenderer.sharedMaterial.name != "BucketRuntimeMaterial")
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogError("[BucketMeshGenerator] Could not find the 'Standard' shader. Is this project " +
                                   "using a Scriptable Render Pipeline (URP/HDRP)? The mesh shape will still be " +
                                   "correct, but the material won't be created.", this);
                }
                else
                {
                    _runtimeMaterial = new Material(shader) { name = "BucketRuntimeMaterial" };
                    _runtimeMaterial.color = new Color(0.65f, 0.1f, 0.1f);
                    _meshRenderer.sharedMaterial = _runtimeMaterial;
                }
            }
            else
            {
                _runtimeMaterial = _meshRenderer.sharedMaterial;
            }

            BuildHandle();
            return bottomY;
        }

        private void CacheComponents()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_handleRenderer == null) _handleRenderer = GetComponent<LineRenderer>();
            if (LinkedBucket == null) LinkedBucket = GetComponentInParent<BucketController>();
        }

        private float BuildBody(Mesh mesh)
        {
            int seg = Mathf.Max(3, RadialSegments);
            float bottomY = TopY - Height;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            // --- Side wall (outer surface only, painted on both faces via double pass) ---
            for (int pass = 0; pass < 2; pass++)
            {
                // pass 0 = outward-facing normals (visible from outside)
                // pass 1 = inward-facing normals (visible from inside, so the open top isn't hollow-black)
                float normalSign = pass == 0 ? 1f : -1f;
                int startIndex = vertices.Count;

                for (int ring = 0; ring <= 1; ring++)
                {
                    float y = ring == 0 ? bottomY : TopY;
                    float radius = ring == 0 ? BottomRadius : TopRadius;

                    for (int i = 0; i <= seg; i++)
                    {
                        float angle = (i / (float)seg) * Mathf.PI * 2f;
                        float x = Mathf.Cos(angle) * radius;
                        float z = Mathf.Sin(angle) * radius;
                        vertices.Add(new Vector3(x, y, z));
                        normals.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * normalSign);
                        uvs.Add(new Vector2(i / (float)seg, ring));
                    }
                }

                for (int i = 0; i < seg; i++)
                {
                    int bl = startIndex + i;
                    int br = startIndex + i + 1;
                    int tl = startIndex + (seg + 1) + i;
                    int tr = startIndex + (seg + 1) + i + 1;

                    if (pass == 0)
                    {
                        triangles.Add(bl); triangles.Add(tl); triangles.Add(br);
                        triangles.Add(br); triangles.Add(tl); triangles.Add(tr);
                    }
                    else
                    {
                        triangles.Add(bl); triangles.Add(br); triangles.Add(tl);
                        triangles.Add(br); triangles.Add(tr); triangles.Add(tl);
                    }
                }
            }

            // --- Bottom cap (solid, so the bucket doesn't look hollow from below) ---
            int bottomCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int bottomRingStart = vertices.Count;
            for (int i = 0; i <= seg; i++)
            {
                float angle = (i / (float)seg) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * BottomRadius;
                float z = Mathf.Sin(angle) * BottomRadius;
                vertices.Add(new Vector3(x, bottomY, z));
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            for (int i = 0; i < seg; i++)
            {
                triangles.Add(bottomCenterIndex);
                triangles.Add(bottomRingStart + i + 1);
                triangles.Add(bottomRingStart + i);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return bottomY;
        }

        private void BuildSpout(float bottomY)
        {
            // The spout is a separate small child object rather than merged into the main mesh,
            // so ParticleEmitter's Local Nozzle Offset can keep pointing at a clear, visible exit point.
            Transform spout = transform.Find("BucketSpout");
            if (!ShowSpout)
            {
                if (spout != null) spout.gameObject.SetActive(false);
                return;
            }

            GameObject spoutObj;
            if (spout == null)
            {
                spoutObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spoutObj.name = "BucketSpout";
                spoutObj.transform.SetParent(transform, false);

                Collider spoutCollider = spoutObj.GetComponent<Collider>();
                if (spoutCollider != null)
                {
                    if (Application.isPlaying) Destroy(spoutCollider);
                    else DestroyImmediate(spoutCollider);
                }
            }
            else
            {
                spoutObj = spout.gameObject;
                spoutObj.SetActive(true);
            }

            spoutObj.transform.localPosition = new Vector3(0f, bottomY - SpoutLength * 0.5f, 0f);
            spoutObj.transform.localScale = new Vector3(SpoutRadius * 2f, SpoutLength * 0.5f, SpoutRadius * 2f);

            var renderer = spoutObj.GetComponent<Renderer>();
            if (renderer != null && _runtimeMaterial != null)
                renderer.sharedMaterial = _runtimeMaterial;
        }

        private void BuildHandle()
        {
            if (_handleRenderer == null)
                return;

            if (!ShowHandle)
            {
                _handleRenderer.enabled = false;
                return;
            }

            _handleRenderer.enabled = true;
            _handleRenderer.useWorldSpace = false;
            _handleRenderer.startWidth = HandleWidth;
            _handleRenderer.endWidth = HandleWidth;

            const int handleSegments = 12;
            var points = new Vector3[handleSegments + 1];
            float radius = TopRadius * 0.95f;

            for (int i = 0; i <= handleSegments; i++)
            {
                float t = i / (float)handleSegments; // 0..1 across the arc
                float angle = Mathf.Lerp(-Mathf.PI * 0.05f, Mathf.PI * 1.05f, t);
                float x = Mathf.Cos(angle) * radius;
                float archY = Mathf.Sin(t * Mathf.PI) * HandleArcHeight;
                points[i] = new Vector3(x, TopY + archY * 0.3f + 0.02f, 0f);
            }

            _handleRenderer.positionCount = points.Length;
            _handleRenderer.SetPositions(points);

            if (_runtimeMaterial != null)
                _handleRenderer.sharedMaterial = _runtimeMaterial;
        }
    }
}
