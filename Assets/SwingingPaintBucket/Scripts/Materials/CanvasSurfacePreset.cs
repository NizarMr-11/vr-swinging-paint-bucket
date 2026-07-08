namespace SwingingPaintBucket.Materials
{
    public static class CanvasSurfacePreset
    {
        private static readonly float[] SpreadMultiplier = {
            0.4f,  // Fabric — انتشار كبير وناعم
            1.0f,  // Wood   — متوسط
            2.5f,  // Metal  — صغير وحاد
            0.6f   // Paper  — متوسط مع نعومة
        };

        private static readonly float[] OpacityMultiplier = {
            1.00f, // Fabric — يمتص بعض اللون
            0.85f, // Wood   — يمتص أكثر
            0.60f, // Metal  — لون كامل
            1.00f  // Paper  — شبه كامل
        };

        public static float GetSpreadMultiplier(CanvasSurfaceType type)
            => SpreadMultiplier[(int)type];

        public static float GetOpacityMultiplier(CanvasSurfaceType type)
            => OpacityMultiplier[(int)type];
    }
}