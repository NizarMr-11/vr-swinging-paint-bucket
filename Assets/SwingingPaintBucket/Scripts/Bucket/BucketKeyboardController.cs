using SwingingPaintBucket.Scene;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SwingingPaintBucket.Bucket
{
    /// <summary>
    /// Tilt-only keyboard control for the lab fluid container (JKIL). No translation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FluidContainer))]
    public sealed class BucketKeyboardController : MonoBehaviour
    {
        [SerializeField] private float tiltSpeed = 45f;
        [SerializeField] private float maxPitchDegrees = 75f;
        [SerializeField] private bool requirePlayMode = true;

        private FluidContainer _container;
        private float _pitch;
        private float _roll;

        private void Awake()
        {
            _container = GetComponent<FluidContainer>();
        }

        private void FixedUpdate()
        {
            if (requirePlayMode && !Application.isPlaying)
            {
                return;
            }

            Vector2 tiltInput = ReadTiltInput();
            if (tiltInput.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            float pitchDelta = tiltInput.x * tiltSpeed * dt;
            float rollDelta = tiltInput.y * tiltSpeed * dt;

            _pitch = Mathf.Clamp(_pitch + pitchDelta, -maxPitchDegrees, maxPitchDegrees);
            _roll += rollDelta;
            transform.localRotation = Quaternion.Euler(_pitch, 0f, _roll);
        }

        private static Vector2 ReadTiltInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            float pitch = 0f;
            float roll = 0f;
            if (Keyboard.current.iKey.isPressed)
            {
                pitch += 1f;
            }

            if (Keyboard.current.kKey.isPressed)
            {
                pitch -= 1f;
            }

            if (Keyboard.current.jKey.isPressed)
            {
                roll += 1f;
            }

            if (Keyboard.current.lKey.isPressed)
            {
                roll -= 1f;
            }

            return new Vector2(pitch, roll);
#else
            float pitch = 0f;
            float roll = 0f;
            if (Input.GetKey(KeyCode.I))
            {
                pitch += 1f;
            }

            if (Input.GetKey(KeyCode.K))
            {
                pitch -= 1f;
            }

            if (Input.GetKey(KeyCode.J))
            {
                roll += 1f;
            }

            if (Input.GetKey(KeyCode.L))
            {
                roll -= 1f;
            }

            return new Vector2(pitch, roll);
#endif
        }
    }
}
