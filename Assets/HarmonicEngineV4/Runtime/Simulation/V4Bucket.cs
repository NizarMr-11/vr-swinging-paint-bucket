using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Bucket authoring component (spec section 1). Geometry is baked once at Start;
    /// per frame only the transform (plus derived linear/angular velocity) is sent to
    /// the GPU. Bucket-local space: origin at floor center, +Y up, rim at y = height.
    /// </summary>
    public sealed class V4Bucket : MonoBehaviour
    {
        [Header("Geometry")]
        [Min(0.01f)] public float innerRadius = 0.5f;
        [Min(0.01f)] public float height = 1f;
        [Min(0.001f)] public float wallThickness = 0.05f;

        [Header("Holes")]
        public List<V4HoleDef> holes = new List<V4HoleDef>();

        [Header("Height layers")]
        [Tooltip("Horizontal slabs stacked from the floor up. Empty = auto-split from Top Band Height.")]
        public List<V4LayerDef> heightLayers = new List<V4LayerDef>();

        [Tooltip("Legacy fallback when Height Layers is empty: rim slab thickness.")]
        [Min(0f)] public float topBandHeight = 0.15f;

        [Tooltip("Deprecated: spherical ring spacing is no longer used.")]
        [Min(0.001f)] public float ringSpacing = 0.08f;

        [Tooltip("Deprecated: kept for old scenes.")]
        public bool autoShrinkRings = true;

        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private bool _hasPrev;

        /// <summary>World-space linear velocity of the bucket origin, from the last sampled frame.</summary>
        public Vector3 LinearVelocity { get; private set; }

        /// <summary>World-space angular velocity (radians/sec), from the last sampled frame.</summary>
        public Vector3 AngularVelocity { get; private set; }

        /// <summary>World-space angular acceleration (rad/s²), analytic or derived.</summary>
        public Vector3 AngularAcceleration { get; private set; }

        public bool UseAnalyticKinematics { get; private set; }

        public Matrix4x4 LocalToWorld => transform.localToWorldMatrix;
        public Matrix4x4 WorldToLocal => transform.worldToLocalMatrix;

        /// <summary>Called by the pipeline once per frame before dispatch to derive frame velocities.</summary>
        public void SampleKinematics(float deltaTime)
        {
            if (UseAnalyticKinematics)
            {
                _prevPosition = transform.position;
                _prevRotation = transform.rotation;
                _hasPrev = true;
                return;
            }

            if (_hasPrev && deltaTime > 1e-6f)
            {
                LinearVelocity = (transform.position - _prevPosition) / deltaTime;

                Quaternion delta = transform.rotation * Quaternion.Inverse(_prevRotation);
                delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
                if (angleDeg > 180f)
                {
                    angleDeg -= 360f;
                }

                AngularVelocity = float.IsNaN(axis.x)
                    ? Vector3.zero
                    : axis.normalized * (angleDeg * Mathf.Deg2Rad / deltaTime);
                AngularAcceleration = Vector3.zero;
            }

            _prevPosition = transform.position;
            _prevRotation = transform.rotation;
            _hasPrev = true;
        }

        /// <summary>Feed exact velocities from the pendulum driver instead of finite differencing.</summary>
        public void SetAnalyticKinematics(
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            Vector3 angularAcceleration = default,
            bool useAnalytic = true)
        {
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            AngularAcceleration = angularAcceleration;
            UseAnalyticKinematics = useAnalytic;
        }

        public void ClearAnalyticKinematics()
        {
            UseAnalyticKinematics = false;
            AngularAcceleration = Vector3.zero;
        }

        public V4BucketBake.Result BakeBucket()
        {
            return V4BucketBake.BakeBucket(holes, heightLayers, height, topBandHeight);
        }

        public V4BucketBake.Result BakeHoles()
        {
            return BakeBucket();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            DrawWireCylinder(innerRadius, height);

            V4BakedLayer[] layers = V4BucketBake.BakeLayers(heightLayers, height, topBandHeight);
            for (int i = 0; i < layers.Length; i++)
            {
                float t = layers.Length > 1 ? i / (float)(layers.Length - 1) : 0f;
                Color layerColor = i < heightLayers.Count ? heightLayers[i].color : Color.HSVToRGB(t * 0.55f + 0.05f, 0.85f, 1f);
                Gizmos.color = new Color(layerColor.r, layerColor.g, layerColor.b, 0.9f);
                if (layers[i].yMin > 1e-4f)
                {
                    DrawWireCircle(layers[i].yMin, innerRadius);
                }

                DrawWireCircle(layers[i].yMax, innerRadius);
            }

            Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
            foreach (V4HoleDef hole in holes)
            {
                if (hole.radius <= 1e-4f)
                {
                    continue;
                }

                DrawWireCircle(hole.localPosition.y, hole.radius, hole.localPosition.x, hole.localPosition.z);
            }
        }

        private static void DrawWireCylinder(float radius, float h)
        {
            DrawWireCircle(0f, radius);
            DrawWireCircle(h, radius);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(p, p + Vector3.up * h);
            }
        }

        private static void DrawWireCircle(float y, float radius, float centerX = 0f, float centerZ = 0f)
        {
            const int segments = 32;
            Vector3 prev = new Vector3(centerX + radius, y, centerZ);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var next = new Vector3(centerX + Mathf.Cos(a) * radius, y, centerZ + Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
