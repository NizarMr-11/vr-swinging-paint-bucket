// ============================================================
// ملف : PendulumSimulator.cs
// المجلد : Scripts/Pendulum/
// الغرض : محاكاة حركة البندول الكروي (الدلو المتأرجح)
//         بدون أي استخدام لـ Unity Physics
//
// المعادلات المستخدمة:
//   α = -(g / L) × sin(θ) - (b × ω)     التسارع الزاوي مع التخامد
//   ω = ω + α × dt                        تحديث السرعة الزاوية
//   θ = θ + ω × dt                        تحديث الزاوية
//   x = L × sin(θ)                        تحويل للفضاء ثلاثي الأبعاد
//   y = -L × cos(θ)
//
// لماذا FixedUpdate وليس Update؟
//   FixedUpdate يُستدعى كل 0.02 ثانية ثابتة بغض النظر عن سرعة الجهاز
//   وهذا ضروري لأن معادلات الفيزياء تعتمد على dt ثابت وموثوق
//
// التبعيات : SimulationConstants
// ============================================================

using UnityEngine;
using SwingingPaintBucket.Core;

namespace SwingingPaintBucket.Pendulum
{
    public class PendulumSimulator : MonoBehaviour
    {
        // ---- الإعدادات القابلة للتعديل من Inspector ----

        [Header("خصائص الحبل")]
        [Tooltip("طول الحبل بالمتر")]
        [Range(0.5f, 20f)]
        public float RopeLength = 5f;

        [Header("البيئة")]
        [Tooltip("قيمة الجاذبية — يمكن تغييرها لمحاكاة بيئات مختلفة")]
        [Range(0f, 20f)]
        public float Gravity = SimulationConstants.DefaultGravity;

        [Tooltip("معامل التخامد — يمثل مقاومة الهواء واحتكاك الحبل")]
        [Range(0f, 1f)]
        public float DampingCoefficient = 0.05f;

        [Header("الحالة الابتدائية")]
        [Tooltip("الزاوية الابتدائية بالدرجات")]
        [Range(-180f, 180f)]
        public float InitialAngleDegrees = 45f;

        [Tooltip("السرعة الزاوية الابتدائية")]
        public float InitialAngularVelocity = 0f;

        [Header("نقطة التعليق")]
        public Vector3 PivotPoint = Vector3.zero;

        // ---- المتغيرات الداخلية للمحاكاة ----

        /// <summary>الزاوية الحالية بالراديان</summary>
        private float _theta;

        /// <summary>السرعة الزاوية الحالية</summary>
        private float _omega;

        // ---- Properties للقراءة من الخارج ----

        /// <summary>الزاوية الحالية — تُقرأ من BucketController لتوليد الجسيمات</summary>
        public float Theta => _theta;

        /// <summary>السرعة الزاوية الحالية</summary>
        public float Omega => _omega;

        /// <summary>
        /// سرعة الدلو كمتجه ثلاثي الأبعاد
        /// مشتقة من معادلات الموقع:
        ///   x = L × sin(θ)  →  vx = L × cos(θ) × ω
        ///   y = -L × cos(θ) →  vy = L × sin(θ) × ω
        /// </summary>
        public Vector3 BucketVelocity
        {
            get
            {
                float vx = RopeLength * Mathf.Cos(_theta) * _omega;
                float vy = RopeLength * Mathf.Sin(_theta) * _omega;
                return new Vector3(vx, vy, 0f);
            }
        }

        // ---- Unity Methods ----

        private void Start()
        {
            // تحويل الزاوية من درجات إلى راديان
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = InitialAngularVelocity;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. حساب التسارع الزاوي
            //    المكوّن الأول  : -(g/L) × sin(θ)  قوة الجاذبية المُعيدة للمركز
            //    المكوّن الثاني : -(b × ω)           قوة التخامد المعاكسة للحركة
            float angularAcceleration = -(Gravity / RopeLength) * Mathf.Sin(_theta)
                                        - (DampingCoefficient * _omega);

            // 2. تحديث السرعة الزاوية
            _omega += angularAcceleration * dt;

            // 3. تحديث الزاوية
            _theta += _omega * dt;

            // 4. تحويل الزاوية إلى موقع في الفضاء وتحريك الـ GameObject
            UpdatePosition();
        }

        // ---- Private Methods ----

        private void UpdatePosition()
        {
            float x = RopeLength * Mathf.Sin(_theta);
            float y = -RopeLength * Mathf.Cos(_theta);

            transform.position = PivotPoint + new Vector3(x, y, 0f);
        }

        // ---- Public Methods ----

        /// <summary>
        /// إعادة تعيين البندول لحالته الابتدائية
        /// </summary>
        public void ResetSimulation()
        {
            _theta = InitialAngleDegrees * Mathf.Deg2Rad;
            _omega = InitialAngularVelocity;
            UpdatePosition();
        }
    }
}
