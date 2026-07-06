using System.Runtime.InteropServices;

namespace HarmonicEngineV4.Profiles
{
    /// <summary>GPU mirror of V4LiquidProfile. Must match V4GpuProfile in V4Profiles.hlsl (56 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct V4GpuProfileData
    {
        public float restDensity;
        public float viscosity;
        public float cohesion;
        public float colorDiffusionRate;
        public float settleEpsilon;
        public float canvasMaxDepth;
        public float surfaceFriction;
        public float surfaceRestitution;
        public float zoneStrength0;
        public float zoneStrength1;
        public float zoneStrength2;
        public float topBandScale;
        public float impactAbsorbSpeed;
        public float impactSplashScale;

        public const int Stride = sizeof(float) * 14;

        /// <summary>
        /// Converts an authored profile to GPU form. restDensity is rescaled to the
        /// solver's lattice rest density (computed numerically from spawn spacing and
        /// smoothing radius) with the authored value acting as a relative multiplier
        /// against the water baseline of 1000.
        /// </summary>
        public static V4GpuProfileData From(V4LiquidProfile profile, float latticeRestDensity)
        {
            return new V4GpuProfileData
            {
                restDensity = latticeRestDensity * (profile.restDensity / 1000f),
                viscosity = profile.viscosity,
                cohesion = profile.cohesion,
                colorDiffusionRate = profile.colorDiffusionRate,
                settleEpsilon = profile.settleEpsilon,
                canvasMaxDepth = profile.canvasMaxDepth,
                surfaceFriction = profile.surfaceFriction,
                surfaceRestitution = profile.surfaceRestitution,
                zoneStrength0 = profile.ZoneStrength(0),
                zoneStrength1 = profile.ZoneStrength(1),
                zoneStrength2 = profile.ZoneStrength(2),
                topBandScale = profile.ZoneStrength(3),
                impactAbsorbSpeed = profile.impactAbsorbSpeed,
                impactSplashScale = profile.impactSplashScale
            };
        }
    }
}
