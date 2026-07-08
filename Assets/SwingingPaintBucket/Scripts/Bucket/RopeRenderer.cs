using SwingingPaintBucket.Pendulum;
using UnityEngine;

namespace SwingingPaintBucket.Bucket
{
    /// <summary>
    /// Draws the rope as a simple line from the pendulum's pivot point to the bucket's live position.
    /// The physics (PendulumSimulator) already computes an accurate 3D bob position and an elastic
    /// EffectiveRopeLength — this script's only job is to make that visible, since nothing previously
    /// drew a rope at all.
    ///
    /// Attach to the same object as PendulumSimulator (e.g. "Bucket"). A LineRenderer is auto-added.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RopeRenderer : MonoBehaviour
    {
        [Tooltip("Usually auto-found on the same object. Assign manually if the rope should follow a different pendulum.")]
        public PendulumSimulator Pendulum;

        [Range(0.002f, 0.05f)] public float RopeWidth = 0.015f;
        public Color RopeColor = new Color(0.35f, 0.25f, 0.15f); // rope-brown

        [Tooltip("Small anchor sphere drawn at the pivot point so the attachment point reads clearly too.")]
        public bool ShowPivotAnchor = true;
        [Range(0.02f, 0.15f)] public float PivotAnchorRadius = 0.05f;

        private LineRenderer _line;
        private Transform _pivotAnchorVisual;
        private Material _ropeMaterial;

        private void Awake()
        {
            if (Pendulum == null)
                Pendulum = GetComponent<PendulumSimulator>();

            _line = GetComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = RopeWidth;
            _line.endWidth = RopeWidth;

            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                _ropeMaterial = new Material(shader) { name = "RopeRuntimeMaterial" };
                _ropeMaterial.color = RopeColor;
                _ropeMaterial.SetFloat("_Glossiness", 0.1f);
                _ropeMaterial.SetFloat("_Metallic", 0f);
                _line.sharedMaterial = _ropeMaterial;
            }

            if (ShowPivotAnchor)
                BuildPivotAnchor();
        }

        private void BuildPivotAnchor()
        {
            GameObject anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchor.name = "RopePivotAnchor";
            Collider col = anchor.GetComponent<Collider>();
            if (col != null) Destroy(col);

            anchor.transform.SetParent(null, true); // pivot is a world-space point, not relative to the swinging bucket
            anchor.transform.localScale = Vector3.one * (PivotAnchorRadius * 2f);

            Renderer rend = anchor.GetComponent<Renderer>();
            if (rend != null && _ropeMaterial != null)
                rend.sharedMaterial = _ropeMaterial;

            _pivotAnchorVisual = anchor.transform;
        }

        private void LateUpdate()
        {
            if (Pendulum == null || _line == null)
                return;

            Vector3 pivot = Pendulum.PivotPoint;
            _line.SetPosition(0, pivot);
            _line.SetPosition(1, transform.position);

            if (_pivotAnchorVisual != null)
                _pivotAnchorVisual.position = pivot;
        }

        private void OnDestroy()
        {
            if (_pivotAnchorVisual != null)
                Destroy(_pivotAnchorVisual.gameObject);
        }
    }
}
