using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Simple keyboard bucket controller for the lab scene: move with WASD/QE,
    /// tilt with arrow keys, spin with R/F, reset with Space.
    /// </summary>
    public sealed class V4BucketMotionController : MonoBehaviour
    {
        [Min(0f)] public float moveSpeed = 1.2f;
        [Min(0f)] public float tiltSpeed = 60f;
        [Min(0f)] public float spinSpeed = 120f;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _spinAngle;
        private float _tiltX;
        private float _tiltZ;

        private void Start()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
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
            transform.position += move * (moveSpeed * dt);

            _tiltX += Axis(Key.UpArrow, Key.DownArrow) * tiltSpeed * dt;
            _tiltZ += Axis(Key.LeftArrow, Key.RightArrow) * tiltSpeed * dt;
            _spinAngle += Axis(Key.R, Key.F) * spinSpeed * dt;

            _tiltX = Mathf.Clamp(_tiltX, -80f, 80f);
            _tiltZ = Mathf.Clamp(_tiltZ, -80f, 80f);
            transform.rotation = Quaternion.Euler(_tiltX, _spinAngle, _tiltZ);

            if (ResetPressed())
            {
                transform.position = _homePosition;
                transform.rotation = _homeRotation;
                _tiltX = _tiltZ = _spinAngle = 0f;
            }
        }
#else
        private void Update()
        {
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
            transform.position += move * (moveSpeed * dt);

            _tiltX += ((Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f)) * tiltSpeed * dt;
            _tiltZ += ((Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)) * tiltSpeed * dt;
            _spinAngle += ((Input.GetKey(KeyCode.R) ? 1f : 0f) - (Input.GetKey(KeyCode.F) ? 1f : 0f)) * spinSpeed * dt;

            _tiltX = Mathf.Clamp(_tiltX, -80f, 80f);
            _tiltZ = Mathf.Clamp(_tiltZ, -80f, 80f);
            transform.rotation = Quaternion.Euler(_tiltX, _spinAngle, _tiltZ);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                transform.position = _homePosition;
                transform.rotation = _homeRotation;
                _tiltX = _tiltZ = _spinAngle = 0f;
            }
        }
#endif
    }
}
