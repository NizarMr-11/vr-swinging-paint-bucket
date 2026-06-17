using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SwingingPaintBucket.Camera
{
    /// <summary>
    /// Free-fly camera: WASD move, mouse look, Shift sprint, Esc unlock cursor.
    /// </summary>
    public class GodModeFlyCamera : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private bool lockCursorOnStart = true;

        private float _pitch;
        private float _yaw;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            HandleCursorToggle();
            HandleLook();
            HandleMove();
        }

        private void HandleCursorToggle()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.Escape))
#endif
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }
        }

        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = ReadLookDelta();
            _yaw += delta.x * lookSensitivity;
            _pitch -= delta.y * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMove()
        {
            Vector3 input = ReadMoveInput();
            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float speed = moveSpeed;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
#else
            if (Input.GetKey(KeyCode.LeftShift))
#endif
            {
                speed *= sprintMultiplier;
            }

            Vector3 world = transform.TransformDirection(input.normalized);
            transform.position += world * (speed * Time.deltaTime);
        }

        private static Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }
#endif
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private static Vector3 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
            {
                return Vector3.zero;
            }

            float x = 0f;
            float z = 0f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                z += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                z -= 1f;
            }

            float y = 0f;
            if (Keyboard.current.eKey.isPressed)
            {
                y += 1f;
            }

            if (Keyboard.current.qKey.isPressed)
            {
                y -= 1f;
            }

            return new Vector3(x, y, z);
#else
            return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"))
                + Vector3.up * (Input.GetKey(KeyCode.E) ? 1f : 0f)
                - Vector3.up * (Input.GetKey(KeyCode.Q) ? 1f : 0f);
#endif
        }
    }
}
