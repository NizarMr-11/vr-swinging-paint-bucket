// ============================================================
// ملف : BucketMaterialPreset.cs
// المجلد : Scripts/Materials/
// الغرض : تخزين القيم الافتراضية لكل نوع مادة
//         نمط التصميم المستخدم: Preset + Override
//         المستخدم يختار المادة → تُحمَّل القيم تلقائياً
//         ثم يستطيع تعديلها يدوياً إذا أراد دقة أكبر
//
// التبعيات : BucketMaterialType
// ============================================================

using SwingingPaintBucket.Materials;

namespace SwingingPaintBucket.Materials
{
    public static class BucketMaterialPreset
    {
        // ---- القيم الافتراضية لكل مادة ----
        // ملاحظة: هذه قيم معقولة نسبياً (Plausible Simulation)
        // وليست قيماً علمية مختبرية دقيقة

        private static readonly float[] DischargeCoefficients = {
            0.75f,  // Plastic
            0.78f,  // SmoothMetal
            0.55f,  // RoughMetal
            0.45f   // Wood
        };

        private static readonly float[] PaintLossRates = {
            0.02f,  // Plastic
            0.01f,  // SmoothMetal
            0.05f,  // RoughMetal
            0.08f   // Wood
        };

        private static readonly float[] AbsorptionRates = {
            0.00f,  // Plastic — لا امتصاص
            0.00f,  // SmoothMetal — لا امتصاص
            0.00f,  // RoughMetal — لا امتصاص
            0.03f   // Wood — يمتص الطلاء
        };

        /// <summary>
        /// يُعيد معامل التدفق (Cd) لنوع المادة المحدد
        /// </summary>
        public static float GetDischargeCoefficent(BucketMaterialType material)
            => DischargeCoefficients[(int)material];

        /// <summary>
        /// يُعيد معدل فقدان الطلاء لنوع المادة المحدد
        /// </summary>
        public static float GetPaintLossRate(BucketMaterialType material)
            => PaintLossRates[(int)material];

        /// <summary>
        /// يُعيد معدل امتصاص الطلاء لنوع المادة المحدد
        /// </summary>
        public static float GetAbsorptionRate(BucketMaterialType material)
            => AbsorptionRates[(int)material];
    }
}
