using SwingingPaintBucket.Pendulum;
using UnityEngine;

namespace SwingingPaintBucket.Visuals
{
    /// <summary>
    /// Renders the rope as a real textured cylinder between the pendulum pivot and the bucket.
    /// This replaces Gizmo-only rope drawing, so textures are visible in Game view and builds.
    /// No Rigidbody or Collider is required.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class RopeVisualController : MonoBehaviour
    {
        [Header("References")]
        public PendulumSimulator Pendulum;
        public Transform RopeEndPoint;

        [Header("Rope Materials")]
        public Material CottonMaterial;
        public Material NylonMaterial;
        public Material ElasticCordMaterial;
        public Material SteelCableMaterial;

        [Header("Visual Settings")]
        [Min(0.001f)] public float RopeRadiusMeters = 0.015f;
        [Tooltip("How many times the rope texture repeats per metre.")]
        public float TextureRepeatsPerMeter = 1.5f;
        public bool FollowPendulumMaterial = true;

        [Header("Runtime Debug")]
        [SerializeField] private float CurrentRopeLengthDebug;
        [SerializeField] private string CurrentMaterialDebug;

        private MeshRenderer _renderer;

        private void Reset()
        {
            TryAutoFindReferences();
        }

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            TryAutoFindReferences();
        }

        private void LateUpdate()
        {
            UpdateRopeVisual();
        }

        private void OnValidate()
        {
            RopeRadiusMeters = Mathf.Max(0.001f, RopeRadiusMeters);
            TextureRepeatsPerMeter = Mathf.Max(0.01f, TextureRepeatsPerMeter);
            _renderer = GetComponent<MeshRenderer>();
            UpdateRopeVisual();
        }

        public void UpdateRopeVisual()
        {
            if (Pendulum == null)
                TryAutoFindReferences();

            if (Pendulum == null)
                return;

            Transform endPoint = RopeEndPoint != null ? RopeEndPoint : Pendulum.transform;
            Vector3 start = Pendulum.PivotPoint;
            Vector3 end = endPoint.position;
            Vector3 direction = end - start;
            float length = direction.magnitude;

            if (length <= 0.0001f)
                return;

            transform.position = (start + end) * 0.5f;
            transform.up = direction.normalized;

            // Unity Cylinder primitive has height 2 and radius 0.5 at scale 1.
            transform.localScale = new Vector3(RopeRadiusMeters * 2f, length * 0.5f, RopeRadiusMeters * 2f);

            CurrentRopeLengthDebug = length;

            if (FollowPendulumMaterial)
                ApplyMaterial(Pendulum.RopeMaterial, length);
        }

        private void ApplyMaterial(RopeType ropeType, float length)
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();

            Material material = GetMaterialForType(ropeType);
            if (_renderer != null && material != null && _renderer.sharedMaterial != material)
                _renderer.sharedMaterial = material;

            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _renderer.sharedMaterial.mainTextureScale = new Vector2(1f, Mathf.Max(1f, length * TextureRepeatsPerMeter));
                CurrentMaterialDebug = ropeType.ToString();
            }
        }

        private Material GetMaterialForType(RopeType ropeType)
        {
            switch (ropeType)
            {
                case RopeType.Cotton: return CottonMaterial;
                case RopeType.Nylon: return NylonMaterial;
                case RopeType.ElasticCord: return ElasticCordMaterial;
                case RopeType.SteelCable: return SteelCableMaterial;
                default: return CottonMaterial;
            }
        }

        private void TryAutoFindReferences()
        {
            if (Pendulum == null)
                Pendulum = FindAnyObjectByType<PendulumSimulator>();
        }
    }
}
