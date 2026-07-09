using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>Draws a single fixed-length rope segment from pivot to the hang-ear apex.</summary>
    [DisallowMultipleComponent]
    public sealed class V4PendulumRopeVisualizer : MonoBehaviour
    {
        public Color ropeColor = new Color(0.55f, 0.35f, 0.15f, 1f);
        [Min(0.001f)] public float ropeWidth = 0.02f;

        private LineRenderer _line;
        private V4SphericalPendulumController _pendulum;
        private V4BucketMotionSettings _motionSettings;

        private void Awake()
        {
            _pendulum = GetComponent<V4SphericalPendulumController>();
            _motionSettings = GetComponent<V4BucketMotionSettings>();
            EnsureLineRenderer();
        }

        private void OnEnable()
        {
            EnsureLineRenderer();
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            if (_pendulum == null || !_pendulum.enabled || _line == null)
            {
                RefreshVisibility();
                return;
            }

            _line.enabled = true;
            _line.startWidth = ropeWidth;
            _line.endWidth = ropeWidth;
            _line.startColor = ropeColor;
            _line.endColor = ropeColor;
            _line.SetPosition(0, _pendulum.PivotPoint);
            _line.SetPosition(1, _pendulum.GetHangPointWorld());
        }

        private void RefreshVisibility()
        {
            if (_line == null)
            {
                return;
            }

            bool show = _motionSettings != null && _motionSettings.IsPendulumMode
                && _pendulum != null
                && _pendulum.enabled;
            _line.enabled = show;
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
                _line.alignment = LineAlignment.View;
                _line.textureMode = LineTextureMode.Stretch;

                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                _line.material = new Material(shader) { color = ropeColor };
            }
        }
    }
}
