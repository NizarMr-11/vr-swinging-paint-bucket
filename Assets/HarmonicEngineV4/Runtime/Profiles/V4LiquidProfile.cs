using UnityEngine;

namespace HarmonicEngineV4.Profiles
{
    /// <summary>
    /// Bundles every tunable that changes "what kind of liquid this is" (spec section 10).
    /// Attached per SpawnZone with a global fallback on the pipeline root; solver, canvas
    /// and zone-force code read constants from here via a GPU profile lookup buffer.
    /// </summary>
    [CreateAssetMenu(menuName = "HarmonicEngineV4/Liquid Profile", fileName = "V4LiquidProfile")]
    public sealed class V4LiquidProfile : ScriptableObject
    {
        public string profileName = "Liquid";

        [Header("PBF")]
        [Tooltip("PBF density target (kg/m3-ish, relative units).")]
        public float restDensity = 1000f;

        [Tooltip("XSPH viscosity coefficient (0..1).")]
        [Range(0f, 1f)] public float viscosity = 0.25f;

        [Tooltip("Cohesion (surface tension) force strength.")]
        public float cohesion = 0.5f;

        [Header("Color")]
        [Tooltip("Color diffusion rate k (spec section 6).")]
        [Range(0f, 1f)] public float colorDiffusionRate = 0.05f;

        [Header("Canvas")]
        [Tooltip("Velocity threshold below which a particle registers on the canvas (spec section 7).")]
        public float settleEpsilon = 0.05f;

        [Tooltip("Saturating paint depth cap per canvas cell (spec section 7).")]
        public float canvasMaxDepth = 4f;

        [Tooltip("Canvas impact speed (m/s) above which a particle is absorbed on contact as an impact splat instead of bouncing.")]
        [Min(0f)] public float impactAbsorbSpeed = 0.8f;

        [Tooltip("Extra splat radius and paint depth per m/s of impact speed (impact splash spread).")]
        [Min(0f)] public float impactSplashScale = 0.35f;

        [Header("Bucket surface")]
        [Tooltip("Bucket-wall slide friction (0 = frictionless).")]
        [Range(0f, 1f)] public float surfaceFriction = 0.2f;

        [Tooltip("Bucket-wall bounce (0 = no bounce).")]
        [Range(0f, 1f)] public float surfaceRestitution = 0.1f;

        [Header("Zone forces")]
        [Tooltip("Zone force strength by level: evaluated at t=0 (hole Zone 0 target exit speed), t=1 (Zone 1 pull), t=2 (Zone 2 pull), t=3 (top-band downward scale).")]
        public AnimationCurve zoneStrengthByLevel = new AnimationCurve(
            new Keyframe(0f, 2.5f),
            new Keyframe(1f, 1.5f),
            new Keyframe(2f, 0.5f),
            new Keyframe(3f, 1f));

        public float ZoneStrength(int level)
        {
            return zoneStrengthByLevel != null ? zoneStrengthByLevel.Evaluate(level) : 0f;
        }
    }
}
