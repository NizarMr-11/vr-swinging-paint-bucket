using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.Rendering
{
    /// <summary>
    /// Editor-style fly camera for the lab scene. Hold the RIGHT MOUSE BUTTON to take
    /// control: mouse look + WASD to fly, Q/E down/up, Shift for speed boost, scroll
    /// wheel to change the base speed. While the right button is not held, WASD stays
    /// free for the bucket motion controller.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class V4FlyCamera : MonoBehaviour
    {
        [Min(0.1f)] public float moveSpeed = 1.5f;
        [Min(0.1f)] public float boostMultiplier = 3f;
        [Min(0.01f)] public float lookSensitivity = 0.15f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse == null || kb == null)
            {
                return;
            }

            bool flying = mouse.rightButton.isPressed;
            Cursor.lockState = flying ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !flying;
            if (!flying)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                moveSpeed = Mathf.Clamp(moveSpeed * (scroll > 0f ? 1.15f : 1f / 1.15f), 0.1f, 20f);
            }

            Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = new Vector3(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            float speed = moveSpeed * (kb.leftShiftKey.isPressed ? boostMultiplier : 1f);
            transform.position += transform.rotation * move * (speed * Time.deltaTime);
        }
#else
        private void Update()
        {
            bool flying = Input.GetMouseButton(1);
            Cursor.lockState = flying ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !flying;
            if (!flying)
            {
                return;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                moveSpeed = Mathf.Clamp(moveSpeed * (scroll > 0f ? 1.15f : 1f / 1.15f), 0.1f, 20f);
            }

            _yaw += Input.GetAxis("Mouse X") * lookSensitivity * 10f;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity * 10f, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);
            transform.position += transform.rotation * move * (speed * Time.deltaTime);
        }
#endif
    }
}
