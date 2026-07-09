using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Diameter bail handle on the bucket rim. The arch spans opposite rim points through
    /// the center; the rope attaches at the arch apex. Bucket +Y still points toward pivot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4Bucket))]
    public sealed class V4BucketHangEar : MonoBehaviour
    {
        [Min(0.02f)] public float earRise = 0.18f;
        [Tooltip("Extra lift for the rope attach above the bail tube top.")]
        [Min(0f)] public float ropeAttachLift = 0.10f;
        [Tooltip("Rotates the bail plane around bucket +Y (degrees).")]
        [Range(0f, 360f)] public float yawDegrees = 90f;
        [Min(6)] public int archSegments = 28;
        [Min(0.004f)] public float tubeRadius = 0.015f;

        private V4Bucket _bucket;

        /// <summary>Bucket-local Y from floor center to rope attach (above bail tube top).</summary>
        public float AttachLocalY
        {
            get
            {
                CacheBucket();
                float rimAttach = _bucket != null ? _bucket.height + earRise : earRise;
                return rimAttach + tubeRadius + ropeAttachLift;
            }
        }

        /// <summary>Bucket-local position of the rope attach point (arch apex above rim center).</summary>
        public Vector3 AttachLocalPosition
        {
            get
            {
                Vector3 apex = new Vector3(0f, AttachLocalY, 0f);
                if (Mathf.Abs(yawDegrees) > 1e-3f)
                {
                    apex = Quaternion.Euler(0f, yawDegrees, 0f) * apex;
                }

                return apex;
            }
        }

        public void GetBailPoints(float outerRimRadius, float rimHeight, out Vector3 footA, out Vector3 footB, out Vector3 apex)
        {
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            footA = yaw * new Vector3(outerRimRadius, rimHeight, 0f);
            footB = yaw * new Vector3(-outerRimRadius, rimHeight, 0f);
            apex = yaw * new Vector3(0f, rimHeight + earRise, 0f);
        }

        public Vector3 GetAttachWorld(Transform bucketTransform)
        {
            return bucketTransform.position + bucketTransform.rotation * AttachLocalPosition;
        }

        private void CacheBucket()
        {
            if (_bucket == null)
            {
                _bucket = GetComponent<V4Bucket>();
            }
        }
    }
}
