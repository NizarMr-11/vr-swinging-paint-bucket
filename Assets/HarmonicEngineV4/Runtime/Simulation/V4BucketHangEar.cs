using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Bail-style hang ear on the bucket rim. The rope attaches at the arch apex;
    /// bucket-local +Y still points toward the pivot (perpendicular hang).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4Bucket))]
    public sealed class V4BucketHangEar : MonoBehaviour
    {
        [Min(0.02f)] public float earHalfWidth = 0.12f;
        [Min(0.02f)] public float earRise = 0.12f;
        [Range(0f, 360f)] public float yawDegrees;
        [Min(4)] public int archSegments = 20;
        [Min(0.004f)] public float tubeRadius = 0.012f;

        private V4Bucket _bucket;

        /// <summary>Bucket-local Y from floor center to rope attach (arch apex).</summary>
        public float AttachLocalY
        {
            get
            {
                CacheBucket();
                return _bucket != null ? _bucket.height + earRise : earRise;
            }
        }

        /// <summary>Bucket-local position of the rope attach point (arch apex).</summary>
        public Vector3 AttachLocalPosition
        {
            get
            {
                CacheBucket();
                float radius = _bucket != null ? _bucket.innerRadius : 0.5f;
                Vector3 local = new Vector3(radius, AttachLocalY, 0f);
                if (Mathf.Abs(yawDegrees) > 1e-3f)
                {
                    local = Quaternion.Euler(0f, yawDegrees, 0f) * local;
                }

                return local;
            }
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
