using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Torricelli exit-speed tuning for hole eject (main-branch equivalent).
    /// vExit = dischargeCoefficient * sqrt(2 * g * headHeight).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V4TorricelliEjectionSettings : MonoBehaviour
    {
        [Range(0.01f, 1f)] public float dischargeCoefficient = 0.75f;
        [Min(0f)] public float headHeight = 0.4f;
        [Min(0f)] public float gravity = 9.81f;

        public float ExitSpeed
        {
            get
            {
                if (headHeight <= 1e-6f)
                {
                    return 0f;
                }

                return dischargeCoefficient * Mathf.Sqrt(2f * gravity * headHeight);
            }
        }
    }
}
