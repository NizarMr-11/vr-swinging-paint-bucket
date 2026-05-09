// ============================================================
// ملف : BucketController.cs
// المجلد : Scripts/Bucket/
// الغرض : إدارة خصائص الدلو الفيزيائية:
//           - نوع المادة وخصائصها
//           - كمية الطلاء المتبقية
//           - حساب تدفق الطلاء باستخدام معادلة Torricelli
//           - تناقص حجم الطلاء مع كل frame
//
// معادلة Torricelli:
//   v_exit = Cd × √(2 × g × h)
//   Q      = A_nozzle × v_exit
//   volume_this_frame = Q × dt
//
// التبعيات : BucketMaterialType, BucketMaterialPreset,
//            SimulationConstants, PendulumSimulator
// ============================================================

using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Pendulum;

namespace SwingingPaintBucket.Bucket
{
    public class BucketController : MonoBehaviour
    {
        // ---- إعدادات المادة ----

        [Header("نوع المادة")]
        public BucketMaterialType MaterialType = BucketMaterialType.Plastic;

        [Header("قيم قابلة للتعديل اليدوي (تُحمَّل تلقائياً من المادة)")]
        [Tooltip("معامل التدفق — يتأثر بنوع المادة وشكل الثقب")]
        [Range(0.1f, 1f)]
        public float DischargeCoefficent;

        [Tooltip("معدل فقدان الطلاء من جدران الدلو")]
        public float PaintLossRate;

        [Tooltip("معدل امتصاص المادة للطلاء (للخشب فقط)")]
        public float AbsorptionRate;

        // ---- إعدادات الطلاء ----

        [Header("الطلاء")]
        [Tooltip("الحجم الابتدائي للطلاء داخل الدلو (لتر)")]
        public float InitialPaintVolume = 2f;

        [Tooltip("نصف قطر فتحة الثقب بالمتر")]
        [Range(0.001f, 0.05f)]
        public float NozzleRadius = 0.005f;

        // ---- الحالة الداخلية ----

        /// <summary>الحجم المتبقي من الطلاء</summary>
        private float _paintVolume;

        /// <summary>هل لا يزال هناك طلاء؟</summary>
        public bool HasPaint => _paintVolume > SimulationConstants.MinPaintVolume;

        /// <summary>الحجم المتبقي للقراءة من الخارج</summary>
        public float PaintVolume => _paintVolume;

        /// <summary>حجم الطلاء الخارج في الـ frame الحالي</summary>
        public float VolumeThisFrame { get; private set; }

        // ---- المرجع للبندول ----
        private PendulumSimulator _pendulum;

        // ---- Unity Methods ----

        private void Start()
        {
            // تحميل القيم الافتراضية من نوع المادة
            DischargeCoefficent = BucketMaterialPreset.GetDischargeCoefficent(MaterialType);
            PaintLossRate       = BucketMaterialPreset.GetPaintLossRate(MaterialType);
            AbsorptionRate      = BucketMaterialPreset.GetAbsorptionRate(MaterialType);

            _paintVolume = InitialPaintVolume;

            // الحصول على مرجع البندول من نفس الـ GameObject
            _pendulum = GetComponent<PendulumSimulator>();
        }

        private void FixedUpdate()
        {
            VolumeThisFrame = 0f;

            if (!HasPaint) return;

            float dt = Time.fixedDeltaTime;

            // حساب ارتفاع الطلاء داخل الدلو
            // (تبسيط: نعتبر h يتناسب مع الحجم المتبقي)
            float h = _paintVolume;

            if (h < SimulationConstants.MinPaintHeight) return;

            // معادلة Torricelli: سرعة الخروج
            float vExit = DischargeCoefficent * Mathf.Sqrt(2f * SimulationConstants.DefaultGravity * h);

            // مساحة فتحة الثقب: A = π × r²
            float nozzleArea = Mathf.PI * NozzleRadius * NozzleRadius;

            // معدل التدفق الحجمي: Q = A × v
            float flowRate = nozzleArea * vExit;

            // الحجم الخارج في هذه الخطوة الزمنية
            VolumeThisFrame = flowRate * dt;

            // تناقص الطلاء: الخروج + الفقدان + الامتصاص
            _paintVolume -= VolumeThisFrame;
            _paintVolume -= PaintLossRate * dt;
            _paintVolume -= AbsorptionRate * dt;

            // منع القيم السالبة
            _paintVolume = Mathf.Max(0f, _paintVolume);
        }

        // ---- Public Methods ----

        /// <summary>
        /// يُعيد السرعة الابتدائية لجسيم طلاء عند خروجه:
        /// = سرعة الدلو (من البندول) + سرعة Torricelli (للأسفل)
        /// </summary>
        public Vector3 GetParticleInitialVelocity()
        {
            Vector3 bucketVelocity = _pendulum != null
                ? _pendulum.BucketVelocity
                : Vector3.zero;

            float h      = Mathf.Max(_paintVolume, SimulationConstants.MinPaintHeight);
            float vExit  = DischargeCoefficent * Mathf.Sqrt(2f * SimulationConstants.DefaultGravity * h);

            Vector3 torricelliVelocity = Vector3.down * vExit;

            return bucketVelocity + torricelliVelocity;
        }

        /// <summary>
        /// إعادة تعيين الدلو لحالته الابتدائية
        /// </summary>
        public void ResetBucket()
        {
            _paintVolume    = InitialPaintVolume;
            VolumeThisFrame = 0f;

            DischargeCoefficent = BucketMaterialPreset.GetDischargeCoefficent(MaterialType);
            PaintLossRate       = BucketMaterialPreset.GetPaintLossRate(MaterialType);
            AbsorptionRate      = BucketMaterialPreset.GetAbsorptionRate(MaterialType);
        }
    }
}
