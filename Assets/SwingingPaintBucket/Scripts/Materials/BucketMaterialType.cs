// ============================================================
// ملف : BucketMaterialType.cs
// المجلد : Scripts/Materials/
// الغرض : تعريف أنواع مواد الدلو المتاحة في المحاكاة
//         كل نوع له قيم فيزيائية مختلفة تؤثر على التدفق
// ============================================================

namespace SwingingPaintBucket.Materials
{
    public enum BucketMaterialType
    {
        /// <summary>
        /// بلاستيك — تدفق جيد، فقدان طلاء قليل، لا امتصاص
        /// </summary>
        Plastic,

        /// <summary>
        /// معدن أملس — أفضل تدفق، أقل فقدان، لا امتصاص
        /// </summary>
        SmoothMetal,

        /// <summary>
        /// معدن خشن — تدفق منخفض، يلوّث الطلاء بالشوائب
        /// </summary>
        RoughMetal,

        /// <summary>
        /// خشب — أسوأ تدفق، يمتص الطلاء من الجدران
        /// </summary>
        Wood
    }
}
