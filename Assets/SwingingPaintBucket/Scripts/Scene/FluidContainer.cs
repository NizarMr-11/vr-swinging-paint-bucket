using HarmonicEngine.Infrastructure.Management;
using UnityEngine;
using UnityEngine.Rendering;

namespace SwingingPaintBucket.Scene
{
    /// <summary>
    /// Open-top cylindrical bucket: GPU SPH particles collide with its walls/floor while a hollow
    /// visual mesh shows the container interior. The base sits at this transform's position.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [ExecuteAlways]
    public class FluidContainer : MonoBehaviour
    {
        private const string VisualChildName = "ContainerVisual";

        [Header("References")]
        [SerializeField] private PipelineExecutionController pipeline;

        [Header("Cylinder (open top)")]
        [SerializeField, Min(0.05f)] private float radius = 0.55f;
        [SerializeField, Min(0.05f)] private float height = 1.1f;
        [SerializeField, Min(0.005f)] private float wallThickness = 0.06f;

        [Header("Boundary response")]
        [Tooltip("0 = particles stop at the wall/floor, 1 = full bounce.")]
        [SerializeField, Range(0f, 1f)] private float restitution = 0.1f;
        [Tooltip("Tangential velocity retained on contact; 1 = frictionless slide.")]
        [SerializeField, Range(0f, 1f)] private float friction = 0.85f;
        [Tooltip("Soft inward penalty strength that keeps SPH samples off the walls/floor.")]
        [SerializeField, Min(0f)] private float wallStiffness = 400f;

        [Header("Visual")]
        [SerializeField] private bool createVisualMesh = true;
        [SerializeField] private Material visualMaterial;
        [SerializeField, Range(0.05f, 1f)] private float visualAlpha = 0.22f;
        [SerializeField, Min(8)] private int visualSegments = 48;

        [Header("Behaviour")]
        [Tooltip("Enable the pipeline's container-fluid mode automatically on Start.")]
        [SerializeField] private bool enableContainerModeOnStart = true;
        [Tooltip("Re-push bounds every frame so a moving/resized container stays in sync.")]
        [SerializeField] private bool continuouslyUpdate;

        private Transform _visualTransform;
        private MeshFilter _visualMeshFilter;
        private Material _runtimeVisualMaterial;
        private Mesh _bucketMesh;

        public float Radius => radius;
        public float Height => height;
        public Vector3 Center => transform.position;
        public float FloorY => transform.position.y;
        public float RimY => transform.position.y + height;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            SyncVisualMesh();
            BootstrapPipeline();
        }

        private void Start()
        {
            BootstrapPipeline();
        }

        private void BootstrapPipeline()
        {
            if (pipeline == null)
            {
                return;
            }

            ApplyToPipeline();
            if (enableContainerModeOnStart)
            {
                pipeline.SetContainerFluidEnabled(true);
                pipeline.EnableExternalIngestion(true);
            }
        }

        private void LateUpdate()
        {
            if (continuouslyUpdate && pipeline != null)
            {
                ApplyToPipeline();
            }
        }

        /// <summary>
        /// Push the current cylinder bounds and boundary response to the pipeline.
        /// </summary>
        public void ApplyToPipeline()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (pipeline == null)
            {
                return;
            }

            pipeline.SetContainerFluid(Center, radius, FloorY, RimY, restitution, friction, wallStiffness);
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.05f, radius);
            height = Mathf.Max(0.05f, height);
            wallThickness = Mathf.Clamp(wallThickness, 0.005f, radius * 0.45f);
            SyncVisualMesh();
        }

        private void OnDestroy()
        {
            if (_runtimeVisualMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeVisualMaterial);
                }
                else
                {
                    DestroyImmediate(_runtimeVisualMaterial);
                }
            }

            if (_bucketMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_bucketMesh);
                }
                else
                {
                    DestroyImmediate(_bucketMesh);
                }
            }
        }

        private void SyncVisualMesh()
        {
            if (!createVisualMesh)
            {
                Transform existing = transform.Find(VisualChildName);
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }

                return;
            }

            EnsureVisualChild();
            RebuildBucketMesh();
            ApplyVisualMaterial();
        }

        private void EnsureVisualChild()
        {
            Transform existing = transform.Find(VisualChildName);
            if (existing == null)
            {
                var go = new GameObject(VisualChildName);
                go.transform.SetParent(transform, false);
                _visualMeshFilter = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                _visualTransform = go.transform;
            }
            else
            {
                _visualTransform = existing;
                existing.gameObject.SetActive(true);
                _visualMeshFilter = existing.GetComponent<MeshFilter>();
                if (_visualMeshFilter == null)
                {
                    _visualMeshFilter = existing.gameObject.AddComponent<MeshFilter>();
                }

                if (existing.GetComponent<MeshRenderer>() == null)
                {
                    existing.gameObject.AddComponent<MeshRenderer>();
                }
            }

            _visualTransform.localScale = Vector3.one;
            _visualTransform.localPosition = Vector3.zero;
            _visualTransform.localRotation = Quaternion.identity;

            Collider collider = _visualTransform.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
        }

        private void RebuildBucketMesh()
        {
            if (_bucketMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_bucketMesh);
                }
                else
                {
                    DestroyImmediate(_bucketMesh);
                }
            }

            _bucketMesh = OpenTopBucketMeshBuilder.Create(radius, height, wallThickness, visualSegments);
            _visualMeshFilter.sharedMesh = _bucketMesh;
        }

        private void ApplyVisualMaterial()
        {
            var renderer = _visualTransform.GetComponent<MeshRenderer>();
            if (visualMaterial != null)
            {
                renderer.sharedMaterial = visualMaterial;
                return;
            }

            if (_runtimeVisualMaterial == null)
            {
                _runtimeVisualMaterial = CreateBucketMaterial(visualAlpha);
            }
            else
            {
                ApplyAlphaToMaterial(_runtimeVisualMaterial, visualAlpha);
            }

            renderer.sharedMaterial = _runtimeVisualMaterial;
        }

        private static Material CreateBucketMaterial(float alpha)
        {
            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader);
            if (shader.name.Contains("Standard"))
            {
                ConfigureStandardTransparent(material);
            }

            material.SetInt("_Cull", (int)CullMode.Off);
            ApplyAlphaToMaterial(material, alpha);
            return material;
        }

        private static void ConfigureStandardTransparent(Material material)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void ApplyAlphaToMaterial(Material material, float alpha)
        {
            // Neutral glass tone — distinct from the blue particle debug points.
            Color color = new Color(0.82f, 0.84f, 0.88f, alpha);
            if (material.HasProperty("_Color"))
            {
                material.color = color;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (createVisualMesh)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.6f);
            DrawWireCylinder(Center, radius, height, 48);
        }

        private static void DrawWireCylinder(Vector3 baseCenter, float r, float h, int segments)
        {
            Vector3 topCenter = baseCenter + Vector3.up * h;
            Vector3 prevBottom = baseCenter + new Vector3(r, 0f, 0f);
            Vector3 prevTop = topCenter + new Vector3(r, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 nextBottom = baseCenter + offset;
                Vector3 nextTop = topCenter + offset;

                Gizmos.DrawLine(prevBottom, nextBottom);
                Gizmos.DrawLine(prevTop, nextTop);

                if (i % 6 == 0)
                {
                    Gizmos.DrawLine(nextBottom, nextTop);
                }

                prevBottom = nextBottom;
                prevTop = nextTop;
            }
        }
    }
}
