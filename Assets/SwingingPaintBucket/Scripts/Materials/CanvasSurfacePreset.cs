namespace SwingingPaintBucket.Materials
{
    public static class CanvasSurfacePreset
    {
        private static readonly float[] SpreadMultiplier =
        {
            0.80f, // Fabric: absorbs paint, medium soft spread
            0.55f, // Wood: porous surface, less lateral spread
            1.35f, // Metal: non-absorbent, paint spreads/slides more
            0.70f  // Paper: absorbs quickly, controlled spread
        };

        private static readonly float[] OpacityMultiplier =
        {
            0.85f, // Fabric: slightly muted by absorption
            0.75f, // Wood: strongest color loss
            0.95f, // Metal: color stays visible on surface
            0.90f  // Paper: mostly visible, slightly absorbed
        };

        private static readonly float[] AbsorptionMultiplier =
        {
            0.35f, // Fabric
            0.55f, // Wood
            0.05f, // Metal
            0.45f  // Paper
        };

        private static readonly float[] SplashMultiplier =
        {
            0.60f, // Fabric: splashes are damped
            0.35f, // Wood: porous surface suppresses splash
            1.10f, // Metal: more splash because it does not absorb
            0.25f  // Paper: quick absorption reduces splash
        };

        public static float GetSpreadMultiplier(CanvasSurfaceType type) => SpreadMultiplier[(int)type];
        public static float GetOpacityMultiplier(CanvasSurfaceType type) => OpacityMultiplier[(int)type];
        public static float GetAbsorptionMultiplier(CanvasSurfaceType type) => AbsorptionMultiplier[(int)type];
        public static float GetSplashMultiplier(CanvasSurfaceType type) => SplashMultiplier[(int)type];
    }
}
