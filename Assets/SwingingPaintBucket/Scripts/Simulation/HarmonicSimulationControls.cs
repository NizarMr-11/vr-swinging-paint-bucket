using HarmonicEngine.Infrastructure.Management;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SwingingPaintBucket.Simulation
{
    /// <summary>
    /// Keyboard / desktop controls for simulation lifecycle and quality tiers.
    /// VR UI can call the same public methods on SimulationManager.
    /// </summary>
    public class HarmonicSimulationControls : MonoBehaviour
    {
        [SerializeField] private SimulationManager simulationManager;

        private void Awake()
        {
            if (simulationManager == null)
            {
                simulationManager = FindFirstObjectByType<SimulationManager>();
            }
        }

        private void Update()
        {
            if (simulationManager == null)
            {
                return;
            }

            if (WasKeyPressed(KeyCode.Space))
            {
                simulationManager.StartSimulation();
            }

            if (WasKeyPressed(KeyCode.P))
            {
                simulationManager.PauseSimulation();
            }

            if (WasKeyPressed(KeyCode.R))
            {
                simulationManager.ResetSimulation();
            }

            if (WasKeyPressed(KeyCode.Alpha1))
            {
                simulationManager.SetQualityTier(HarmonicQualityTier.Low);
            }

            if (WasKeyPressed(KeyCode.Alpha2))
            {
                simulationManager.SetQualityTier(HarmonicQualityTier.Medium);
            }

            if (WasKeyPressed(KeyCode.Alpha3))
            {
                simulationManager.SetQualityTier(HarmonicQualityTier.High);
            }

            if (WasKeyPressed(KeyCode.Alpha4))
            {
                simulationManager.SetQualityTier(HarmonicQualityTier.Cinematic);
            }

            if (WasKeyPressed(KeyCode.B))
            {
                simulationManager.SetSimulationMode(HarmonicSimulationMode.BakeRecord);
            }

            if (WasKeyPressed(KeyCode.V))
            {
                simulationManager.SetSimulationMode(HarmonicSimulationMode.BakePlayback);
            }

            if (WasKeyPressed(KeyCode.L))
            {
                simulationManager.SetSimulationMode(HarmonicSimulationMode.Live);
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
                KeyCode.Space => Key.Space,
                KeyCode.P => Key.P,
                KeyCode.R => Key.R,
                KeyCode.Alpha1 => Key.Digit1,
                KeyCode.Alpha2 => Key.Digit2,
                KeyCode.Alpha3 => Key.Digit3,
                KeyCode.Alpha4 => Key.Digit4,
                KeyCode.B => Key.B,
                KeyCode.V => Key.V,
                KeyCode.L => Key.L,
                _ => null
            };
#endif
    }
}
