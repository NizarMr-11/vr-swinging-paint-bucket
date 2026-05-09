// ============================================================
// ملف : MathUtils.cs
// المجلد : Scripts/Utilities/
// الغرض : دوال رياضية مساعدة تُستخدم في أكثر من نظام
//         مكان واحد لجميع الحسابات المشتركة
// ============================================================

using UnityEngine;

namespace SwingingPaintBucket.Utilities
{
    public static class MathUtils
    {
        /// <summary>
        /// حساب سرعة خروج السائل بمعادلة Torricelli
        /// v = Cd × √(2 × g × h)
        /// </summary>
        /// <param name="dischargeCoeff">معامل التدفق (0.4 - 0.9)</param>
        /// <param name="gravity">الجاذبية م/ث²</param>
        /// <param name="height">ارتفاع السائل بالمتر</param>
        public static float TorricelliExitVelocity(float dischargeCoeff, float gravity, float height)
        {
            if (height <= 0f) return 0f;
            return dischargeCoeff * Mathf.Sqrt(2f * gravity * height);
        }

        /// <summary>
        /// حساب مساحة دائرة: A = π × r²
        /// </summary>
        public static float CircleArea(float radius)
            => Mathf.PI * radius * radius;

        /// <summary>
        /// تحويل إحداثيات البندول (θ, L) إلى موقع في الفضاء
        /// x = L × sin(θ)
        /// y = -L × cos(θ)
        /// </summary>
        public static Vector3 PolarToCartesian(float theta, float ropeLength, Vector3 pivot)
        {
            float x = ropeLength * Mathf.Sin(theta);
            float y = -ropeLength * Mathf.Cos(theta);
            return pivot + new Vector3(x, y, 0f);
        }

        /// <summary>
        /// حساب سرعة الدلو كمتجه من الزاوية والسرعة الزاوية
        /// vx = L × cos(θ) × ω
        /// vy = L × sin(θ) × ω
        /// </summary>
        public static Vector3 PendulumVelocity(float theta, float omega, float ropeLength)
        {
            float vx = ropeLength * Mathf.Cos(theta) * omega;
            float vy = ropeLength * Mathf.Sin(theta) * omega;
            return new Vector3(vx, vy, 0f);
        }
    }
}
