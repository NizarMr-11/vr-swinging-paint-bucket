using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Rendering
{
    public enum HarmonicLabViewMode
    {
        ScreenSpaceFluid = 1,
        DebugPoints = 2
    }

    /// <summary>
    /// Lab hotkeys: Alpha1 = SSFR fluid, Alpha2 = debug points. Overlay visibility is controlled by
    /// <see cref="HarmonicEngine.Diagnostics.HarmonicPipelineDiagnosticsController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarmonicLabViewController : MonoBehaviour
    {
        [SerializeField] private HarmonicFluidVisualController fluidVisualController;
        [SerializeField] private HarmonicLabViewMode initialMode = HarmonicLabViewMode.ScreenSpaceFluid;
        [SerializeField] private bool showModeHint = true;

        private HarmonicLabViewMode _activeMode;
        private GUIStyle _hintStyle;

        private void Start()
        {
            ApplyMode(initialMode);
        }

        private void Update()
        {
            if (WasKeyPressed(KeyCode.Alpha1) || WasKeyPressed(KeyCode.Keypad1))
            {
                ApplyMode(HarmonicLabViewMode.ScreenSpaceFluid);
            }
            else if (WasKeyPressed(KeyCode.Alpha2) || WasKeyPressed(KeyCode.Keypad2))
            {
                ApplyMode(HarmonicLabViewMode.DebugPoints);
            }
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current == null)
            {
                return false;
            }

            UnityEngine.InputSystem.Key? key = keyCode switch
            {
                KeyCode.Alpha1 => UnityEngine.InputSystem.Key.Digit1,
                KeyCode.Alpha2 => UnityEngine.InputSystem.Key.Digit2,
                KeyCode.Keypad1 => UnityEngine.InputSystem.Key.Numpad1,
                KeyCode.Keypad2 => UnityEngine.InputSystem.Key.Numpad2,
                _ => null
            };
            return key.HasValue && UnityEngine.InputSystem.Keyboard.current[key.Value].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!showModeHint)
            {
                return;
            }

            _hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold
            };
            _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            string modeLabel = _activeMode == HarmonicLabViewMode.ScreenSpaceFluid
                ? "1: SSFR Fluid"
                : "2: Debug Points";
            GUI.Label(new Rect(Screen.width - 200f, 8f, 190f, 24f), modeLabel, _hintStyle);
        }

        public void ApplyMode(HarmonicLabViewMode mode)
        {
            _activeMode = mode;
            if (fluidVisualController == null)
            {
                fluidVisualController = FindFirstObjectByType<HarmonicFluidVisualController>();
            }

            if (fluidVisualController != null)
            {
                fluidVisualController.VisualMode = mode == HarmonicLabViewMode.ScreenSpaceFluid
                    ? HarmonicFluidVisualMode.ScreenSpaceFluid
                    : HarmonicFluidVisualMode.DebugPoints;
            }
        }
    }
}
