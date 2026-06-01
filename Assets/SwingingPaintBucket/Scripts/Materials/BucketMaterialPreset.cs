using SwingingPaintBucket.Materials;

namespace SwingingPaintBucket.Materials
{
    public static class BucketMaterialPreset
    {
        //sensible values for discharge coefficients, paint loss rates, and absorption rates for each material type

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


        public static float GetDischargeCoefficent(BucketMaterialType material)
            => DischargeCoefficients[(int)material];

        public static float GetPaintLossRate(BucketMaterialType material)
            => PaintLossRates[(int)material];

  
        public static float GetAbsorptionRate(BucketMaterialType material)
            => AbsorptionRates[(int)material];
    }
}
