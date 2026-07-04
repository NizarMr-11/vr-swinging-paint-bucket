using UnityEngine;

namespace SwingingPaintBucket.Simulation
{
    public class EnvironmentController : MonoBehaviour
    {
        [Header("الرياح")]
        public Vector3 WindForce = Vector3.zero;

        [Header("الحرارة والرطوبة")]
        [Range(0f, 50f)]
        public float Temperature = 20f;

        [Range(0f, 100f)]
        public float Humidity = 50f;

        public float GetViscosityMultiplier()
        {
            float tempEffect = 1f - (Temperature - 20f) * 0.015f;
            float humidityEffect = 1f + (Humidity - 50f) * 0.015f;

            return tempEffect * humidityEffect;
        }
    }
}