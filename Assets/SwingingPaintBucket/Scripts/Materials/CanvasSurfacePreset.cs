namespace SwingingPaintBucket.Materials
{
    public static class CanvasSurfacePreset
    {
        private static readonly float[] SpreadMultiplier = {
            1.8f,  // Fabric — انتشار كبير وناعم
            1.0f,  // Wood   — متوسط
            0.5f,  // Metal  — صغير وحاد
            1.2f   // Paper  — متوسط مع نعومة
        };

        private static readonly float[] OpacityMultiplier = {
            0.80f, // Fabric — يمتص بعض اللون
            0.65f, // Wood   — يمتص أكثر
            1.00f, // Metal  — لون كامل
            0.90f  // Paper  — شبه كامل
        };

        public static float GetSpreadMultiplier(CanvasSurfaceType type)
            => SpreadMultiplier[(int)type];

        public static float GetOpacityMultiplier(CanvasSurfaceType type)
            => OpacityMultiplier[(int)type];
    }
}