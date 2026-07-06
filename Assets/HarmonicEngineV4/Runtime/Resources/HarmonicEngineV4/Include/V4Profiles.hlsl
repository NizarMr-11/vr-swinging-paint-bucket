#ifndef HARMONIC_V4_PROFILES_INCLUDED
#define HARMONIC_V4_PROFILES_INCLUDED

// GPU mirror of V4LiquidProfile (spec section 10). One entry per profile asset;
// particles reference an entry via the profile index bits in their flags.
// CPU-side struct: V4GpuProfileData (68 bytes, must match).
struct V4GpuProfile
{
    float restDensity;
    float viscosity;
    float cohesion;
    float colorDiffusionRate;
    float settleEpsilon;
    float canvasMaxDepth;
    float surfaceFriction;
    float surfaceRestitution;
    float zoneStrength0;   // Zone 0 eject exit speed target
    float zoneStrength1;   // Zone 1 pull strength
    float zoneStrength2;   // Zone 2 pull strength
    float topBandScale;    // Level 2 downward scale multiplier
    float impactAbsorbSpeed;  // canvas impact speed that triggers instant absorption
    float impactSplashScale;  // splat radius/depth growth per m/s of impact speed
    float pbfKCorr;        // artificial pressure strength (s_corr)
    float pbfNCorr;        // artificial pressure exponent
    float pbfDeltaQScale;  // s_corr reference distance as fraction of h
};

#endif // HARMONIC_V4_PROFILES_INCLUDED
