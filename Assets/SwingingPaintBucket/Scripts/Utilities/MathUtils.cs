using UnityEngine;

namespace SwingingPaintBucket.Utilities
{
    public static class MathUtils
    {
        // Torricelli's theorem: v = Cd * sqrt(2 * g * h)
        public static float TorricelliExitVelocity(float dischargeCoeff, float gravity, float height)
        {
            if (height <= 0f) return 0f;
            return dischargeCoeff * Mathf.Sqrt(2f * gravity * height);
        }

        public static float CircleArea(float radius)
            => Mathf.PI * radius * radius;


        public static Vector3 PolarToCartesian(float theta, float ropeLength, Vector3 pivot)
        {
            float x = ropeLength * Mathf.Sin(theta);
            float y = -ropeLength * Mathf.Cos(theta);
            return pivot + new Vector3(x, y, 0f);
        }

  
        public static Vector3 PendulumVelocity(float theta, float omega, float ropeLength)
        {
            float vx = ropeLength * Mathf.Cos(theta) * omega;
            float vy = ropeLength * Mathf.Sin(theta) * omega;
            return new Vector3(vx, vy, 0f);
        }
    }
}
