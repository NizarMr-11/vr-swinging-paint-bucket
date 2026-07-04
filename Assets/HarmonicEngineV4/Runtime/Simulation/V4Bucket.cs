using System.Collections.Generic;
using HarmonicEngineV4.Bake;
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

        [Tooltip("Ring step between d0->d1 and d1->d2 (bucket-local meters).")]
        [Min(0.001f)] public float ringSpacing = 0.08f;

        [Tooltip("Shrink overlapping zone rings at bake time instead of failing the bake.")]
        public bool autoShrinkRings = true;

        [Header("Zones")]
        [Tooltip("Height of the Level 2 top band measured down from the rim.")]
        [Min(0f)] public float topBandHeight = 0.15f;

        private Vector3 _prevPosition;
        private Quaternion _prevRotation;
        private bool _hasPrev;

        /// <summary>World-space linear velocity of the bucket origin, from the last sampled frame.</summary>
        public Vector3 LinearVelocity { get; private set; }

        /// <summary>World-space angular velocity (radians/sec), from the last sampled frame.</summary>
        public Vector3 AngularVelocity { get; private set; }

        public Matrix4x4 LocalToWorld => transform.localToWorldMatrix;
        public Matrix4x4 WorldToLocal => transform.worldToLocalMatrix;

        /// <summary>Called by the pipeline once per frame before dispatch to derive frame velocities.</summary>
        public void SampleKinematics(float deltaTime)
        {
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
            }

            _prevPosition = transform.position;
            _prevRotation = transform.rotation;
            _hasPrev = true;
        }

        public V4BucketBake.Result BakeHoles()
        {
            return V4BucketBake.BakeHoles(holes, ringSpacing, autoShrinkRings);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            DrawWireCylinder(innerRadius, height);
            Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
            foreach (V4HoleDef hole in holes)
            {
                Gizmos.DrawWireSphere(hole.localPosition, hole.radius);
            }

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            DrawWireCircle(height - topBandHeight, innerRadius);
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

        private static void DrawWireCircle(float y, float radius)
        {
            const int segments = 32;
            Vector3 prev = new Vector3(radius, y, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var next = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
