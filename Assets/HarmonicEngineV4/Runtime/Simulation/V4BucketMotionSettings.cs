using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Selects keyboard lab control vs spherical pendulum on the same bucket GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class V4BucketMotionSettings : MonoBehaviour
    {
        public V4BucketMotionMode mode = V4BucketMotionMode.Keyboard;

        private V4BucketMotionController _keyboard;
        private V4SphericalPendulumController _pendulum;

        public bool IsPendulumMode => mode == V4BucketMotionMode.Pendulum;

        private void Awake()
        {
            CacheComponents();
            ApplyMode();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplyMode();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                CacheComponents();
                ApplyMode();
            }
        }

        public void SetMode(V4BucketMotionMode newMode)
        {
            mode = newMode;
            ApplyMode();
        }

        private void CacheComponents()
        {
            if (_keyboard == null)
            {
                _keyboard = GetComponent<V4BucketMotionController>();
            }

            if (_pendulum == null)
            {
                _pendulum = GetComponent<V4SphericalPendulumController>();
            }
        }

        private void ApplyMode()
        {
            bool pendulum = IsPendulumMode;
            V4Bucket bucket = GetComponent<V4Bucket>();

            if (_keyboard != null)
            {
                _keyboard.inputEnabled = !pendulum;
                _keyboard.enabled = !pendulum;
            }

            if (_pendulum != null)
            {
                _pendulum.enabled = pendulum;
            }

            if (bucket != null && !pendulum)
            {
                bucket.ClearAnalyticKinematics();
            }
        }
    }
}
