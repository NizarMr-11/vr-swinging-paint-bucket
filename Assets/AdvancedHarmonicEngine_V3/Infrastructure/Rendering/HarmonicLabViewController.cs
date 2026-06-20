using HarmonicEngine.Diagnostics;
using SwingingPaintBucket.Debugging;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngine.Infrastructure.Rendering
{
    public enum HarmonicLabViewMode
    {
        PerformanceStats = 1,
        Diagnostic = 2
    }

    /// <summary>
    /// Lab hotkeys: Alpha1 = pipeline stats + SSFR fluid, Alpha2 = AOP diagnostic + debug points.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarmonicLabViewController : MonoBehaviour
    {
        [SerializeField] private HarmonicPipelineStatsOverlay statsOverlay;
        [SerializeField] private HarmonicDiagnosticHost diagnosticHost;
        [SerializeField] private HarmonicFluidVisualController fluidVisualController;
        [SerializeField] private HarmonicLabViewMode initialMode = HarmonicLabViewMode.PerformanceStats;
        [SerializeField] private bool showModeHint = true;

        private HarmonicLabViewMode _activeMode;
        private GUIStyle _hintStyle;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            ApplyMode(initialMode);
        }

        private void Update()
        {
            if (WasKeyPressed(KeyCode.Alpha1) || WasKeyPressed(KeyCode.Keypad1))
            {
                ApplyMode(HarmonicLabViewMode.PerformanceStats);
            }
            else if (WasKeyPressed(KeyCode.Alpha2) || WasKeyPressed(KeyCode.Keypad2))
            {
                ApplyMode(HarmonicLabViewMode.Diagnostic);
            }
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null)
            {
                return false;
            }

            Key? key = KeyCodeToInputSystemKey(keyCode);
            return key.HasValue && Keyboard.current[key.Value].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Key? KeyCodeToInputSystemKey(KeyCode keyCode) =>
            keyCode switch
            {
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Keypad1 => Key.Numpad1,
                KeyCode.Keypad2 => Key.Numpad2,
                _ => null
            };
#endif

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

            string modeLabel = _activeMode == HarmonicLabViewMode.PerformanceStats
                ? "1: Stats + Fluid"
                : "2: Diagnostic + Points";
            GUI.Label(new Rect(Screen.width - 220f, 8f, 210f, 24f), modeLabel, _hintStyle);
        }

        public void ApplyMode(HarmonicLabViewMode mode)
        {
            _activeMode = mode;
            ResolveReferences();

            bool statsMode = mode == HarmonicLabViewMode.PerformanceStats;

            if (statsOverlay != null)
            {
                statsOverlay.SetVisible(statsMode);
            }

            if (diagnosticHost != null)
            {
                diagnosticHost.SetOverlayVisible(!statsMode);
            }

            if (fluidVisualController != null)
            {
                fluidVisualController.VisualMode = statsMode
                    ? HarmonicFluidVisualMode.ScreenSpaceFluid
                    : HarmonicFluidVisualMode.DebugPoints;
            }
        }

        private void ResolveReferences()
        {
            if (statsOverlay == null)
            {
                statsOverlay = FindFirstObjectByType<HarmonicPipelineStatsOverlay>();
            }

            if (diagnosticHost == null)
            {
                diagnosticHost = FindFirstObjectByType<HarmonicDiagnosticHost>();
            }

            if (fluidVisualController == null)
            {
                fluidVisualController = FindFirstObjectByType<HarmonicFluidVisualController>();
            }
        }
    }
}
