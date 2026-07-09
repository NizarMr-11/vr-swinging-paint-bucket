using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Simple keyboard bucket controller for the lab scene: move with WASD/QE,
    /// tilt with arrow keys, spin with R/F, reset with Space.
    /// Linear and angular motion ramp up and coast down via MoveTowards acceleration.
    /// </summary>
    public sealed class V4BucketMotionController : MonoBehaviour
    {
        [Tooltip("When false, keyboard bucket control is ignored (used by networked clients).")]
        public bool inputEnabled = true;

        [Min(0f)] public float moveSpeed = 1.2f;
        [Min(0f)] public float tiltSpeed = 60f;
        [Min(0f)] public float spinSpeed = 120f;

        [Min(0f)] public float linearAcceleration = 2f;
        [Min(0f)] public float maxLinearSpeed = 1.2f;
        [Min(0f)] public float angularAcceleration = 80f;
        [Min(0f)] public float maxAngularSpeed = 120f;

        [Tooltip("How sluggish the initial speed ramp feels (0 = linear, 1 = heaviest slow start).")]
        [Range(0f, 1f)] public float buildupHeaviness = 0.85f;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _spinAngle;
        private float _tiltX;
        private float _tiltZ;
        private Vector3 _currentLinearVel;
        private Vector3 _currentAngularVel;

        private void Start()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        private static float BuildupAccelScale(Vector3 current, Vector3 target, float maxSpeed)
        {
            if (target.sqrMagnitude < 1e-8f)
            {
                return 1f;
            }

            float targetMag = target.magnitude;
            float alignedSpeed = Mathf.Max(0f, Vector3.Dot(current, target / targetMag));
            float t = Mathf.Clamp01(alignedSpeed / Mathf.Max(maxSpeed, 1e-6f));
            float curved = Mathf.Lerp(0.1f, 1f, t * t * t);
            return curved;
        }

        private static float AccelStep(float acceleration, float dt, Vector3 current, Vector3 target, float maxSpeed, float heaviness)
        {
            float scale = Mathf.Lerp(1f, BuildupAccelScale(current, target, maxSpeed), heaviness);
            return acceleration * dt * scale;
        }

        private void ApplyMotion(float dt, Vector3 moveInput, float tiltXAxis, float tiltZAxis, float spinAxis, bool reset)
        {
            if (reset)
            {
                transform.position = _homePosition;
                transform.rotation = _homeRotation;
                _tiltX = _tiltZ = _spinAngle = 0f;
                _currentLinearVel = Vector3.zero;
                _currentAngularVel = Vector3.zero;
                return;
            }

            Vector3 targetLinearVel = moveInput.sqrMagnitude > 0f
                ? moveInput.normalized * maxLinearSpeed
                : Vector3.zero;
            _currentLinearVel = Vector3.MoveTowards(
                _currentLinearVel,
                targetLinearVel,
                AccelStep(linearAcceleration, dt, _currentLinearVel, targetLinearVel, maxLinearSpeed, buildupHeaviness));
            transform.position += _currentLinearVel * dt;

            Vector3 targetAngularVel = new Vector3(
                tiltXAxis * maxAngularSpeed,
                spinAxis * maxAngularSpeed,
                tiltZAxis * maxAngularSpeed);
            _currentAngularVel = Vector3.MoveTowards(
                _currentAngularVel,
                targetAngularVel,
                AccelStep(angularAcceleration, dt, _currentAngularVel, targetAngularVel, maxAngularSpeed, buildupHeaviness));

            _tiltX += _currentAngularVel.x * dt;
            _spinAngle += _currentAngularVel.y * dt;
            _tiltZ += _currentAngularVel.z * dt;

            _tiltX = Mathf.Clamp(_tiltX, -80f, 80f);
            _tiltZ = Mathf.Clamp(_tiltZ, -80f, 80f);
            transform.rotation = Quaternion.Euler(_tiltX, _spinAngle, _tiltZ);
        }

#if ENABLE_INPUT_SYSTEM
        private static float Axis(Key positive, Key negative)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return 0f;
            }

            return (kb[positive].isPressed ? 1f : 0f) - (kb[negative].isPressed ? 1f : 0f);
        }

        private static bool ResetPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
        }

        private void Update()
        {
            if (!inputEnabled)
            {
                return;
            }

            // While the right mouse button is held, the fly camera owns WASD/QE.
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                return;
            }

            float dt = Time.deltaTime;

            var move = new Vector3(
                Axis(Key.D, Key.A),
                Axis(Key.E, Key.Q),
                Axis(Key.W, Key.S));

            ApplyMotion(
                dt,
                move,
                Axis(Key.UpArrow, Key.DownArrow),
                Axis(Key.LeftArrow, Key.RightArrow),
                Axis(Key.R, Key.F),
                ResetPressed());
        }
#else
        private void Update()
        {
            if (!inputEnabled)
            {
                return;
            }

            // While the right mouse button is held, the fly camera owns WASD/QE.
            if (Input.GetMouseButton(1))
            {
                return;
            }

            float dt = Time.deltaTime;

            var move = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));

            ApplyMotion(
                dt,
                move,
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.R) ? 1f : 0f) - (Input.GetKey(KeyCode.F) ? 1f : 0f),
                Input.GetKeyDown(KeyCode.Space));
        }
#endif
    }
}
