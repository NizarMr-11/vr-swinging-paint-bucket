// ============================================================
// ملف : SimulationConstants.cs
// المجلد : Scripts/Core/
// الغرض : تخزين جميع الثوابت المستخدمة في المحاكاة
//         في مكان واحد لتسهيل التعديل لاحقاً
// ============================================================

namespace SwingingPaintBucket.Core
{
    public static class SimulationConstants
    {
        // الجاذبية الافتراضية (م/ث²)
        public const float DefaultGravity = 9.81f;

        // الخطوة الزمنية الثابتة للفيزياء
        public const float FixedTimeStep = 0.02f;

        // معامل التدفق الافتراضي (Discharge Coefficient)
        public const float DefaultDischargeCoefficent = 0.7f;

        // أقل حجم طلاء قبل التوقف
        public const float MinPaintVolume = 0.0001f;

        // أقل ارتفاع طلاء قبل التوقف
        public const float MinPaintHeight = 0.001f;
    }
}
