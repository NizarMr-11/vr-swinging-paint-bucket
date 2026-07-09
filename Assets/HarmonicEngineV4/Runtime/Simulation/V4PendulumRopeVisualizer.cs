using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>Draws a single fixed-length rope segment from pivot to bucket.</summary>
    [DisallowMultipleComponent]
    public sealed class V4PendulumRopeVisualizer : MonoBehaviour
    {
        public Color ropeColor = new Color(0.55f, 0.35f, 0.15f, 1f);
        [Min(0.001f)] public float ropeWidth = 0.02f;

        private LineRenderer _line;
        private V4SphericalPendulumController _pendulum;

        private void Awake()
        {
            _pendulum = GetComponent<V4SphericalPendulumController>();
            EnsureLineRenderer();
        }

        private void OnEnable()
        {
            EnsureLineRenderer();
            if (_line != null)
            {
                _line.enabled = _pendulum != null && _pendulum.enabled;
            }
        }

        private void LateUpdate()
        {
            V4BucketMotionSettings motionSettings = GetComponent<V4BucketMotionSettings>();
            if (motionSettings != null && motionSettings.UseGpuPendulum)
            {
                if (_line != null)
                {
                    _line.enabled = false;
                }

                return;
            }

            if (_pendulum == null || !_pendulum.enabled || _line == null)
            {
                if (_line != null)
                {
                    _line.enabled = false;
                }

                return;
            }

            _line.enabled = true;
            _line.startWidth = ropeWidth;
            _line.endWidth = ropeWidth;
            _line.startColor = ropeColor;
            _line.endColor = ropeColor;
            _line.SetPosition(0, _pendulum.PivotPoint);
            _line.SetPosition(1, transform.position);
        }

        private void EnsureLineRenderer()
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                if (_line == null)
                {
                    _line = gameObject.AddComponent<LineRenderer>();
                }

                _line.useWorldSpace = true;
                _line.positionCount = 2;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
                _line.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }
}
