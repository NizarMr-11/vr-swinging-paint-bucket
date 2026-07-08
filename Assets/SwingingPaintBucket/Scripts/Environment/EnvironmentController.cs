using UnityEngine;

namespace SwingingPaintBucket.Simulation
{
    public class EnvironmentController : MonoBehaviour
    {
        [Header("الرياح")]
        [Tooltip("Wind acceleration in m/s^2, applied to both the pendulum swing and falling paint. " +
                 "Keep this small relative to gravity (9.81): a light breeze is roughly 0.5-2, a strong gust " +
                 "is roughly 3-5. Values anywhere near or above gravity will overpower it and make the swing " +
                 "and the paint trajectory look broken. The vertical (Y) component should normally stay at 0 " +
                 "since real wind blows sideways, not up/down.")]
        public Vector3 WindForce = Vector3.zero;

        [Header("الحرارة والرطوبة")]
        [Range(0f, 50f)]
        public float Temperature = 20f;

        [Range(0f, 100f)]
        public float Humidity = 50f;

        [Tooltip("Safety clamp so an accidental huge wind value in the Inspector cannot silently break the physics.")]
        [Range(0f, 8f)] public float MaxWindMagnitude = 5f;

        public float GetViscosityMultiplier()
        {
            float tempEffect = 1f - (Temperature - 20f) * 0.015f;
            float humidityEffect = 1f + (Humidity - 50f) * 0.015f;

            return tempEffect * humidityEffect;
        }

        private void OnValidate()
        {
            // Wind is horizontal by definition here; a vertical wind component isn't physically
            // meaningful for this simulation and tends to be the cause of "explodes into the air"
            // style bugs when someone drags a slider by mistake.
            WindForce.y = 0f;

            if (WindForce.magnitude > MaxWindMagnitude)
                WindForce = WindForce.normalized * MaxWindMagnitude;
        }
    }
}