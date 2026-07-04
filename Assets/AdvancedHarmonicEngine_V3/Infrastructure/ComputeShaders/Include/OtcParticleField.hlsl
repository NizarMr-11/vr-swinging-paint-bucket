#ifndef OTC_PARTICLE_FIELD_INCLUDED
#define OTC_PARTICLE_FIELD_INCLUDED

// Unified particle-to-bucket classification field (Phase 1a).
//
// This header is the single analytic source of truth for "where is this particle relative to the
// bucket" that will eventually replace the six independent boolean predicates in
// OpenTopCylinderBoundary.hlsl. In Phase 1a NOTHING consumes it yet: the legacy predicates stay live
// and this field is validated for parity against them (see OtcFieldParityTests). Consumers migrate in
// Phase 1b.
//
// REQUIREMENTS (must be declared/included by the parent shader BEFORE this file):
//   - OpenTopCylinderBoundary.hlsl (provides OTC_FLOOR_GATE_TOLERANCE, OTC_RADIAL_BOUNDARY_EPS)
//   - float _ContainerRadius, float _ContainerHeight
// All positions passed to OtcSampleField are CONTAINER-LOCAL (caller applies _ContainerWorldToLocal).
//
// Geometry model:
//   - Cup cavity = open-top cylinder interior { r <= R, y >= 0 }, UNBOUNDED above (slosh above the
//     rim is still "inside the column"), matching OtcParticipatesInPbf's no-upper-bound semantics.
//   - Holes are SURFACE-CLIPPED cuts (NOT infinite cylinders): a local opening of depth
//     D = _HoleClipDepthScale * radius near the surface, subtracted via CSG max(cup, -hole).
//       * Floor hole: axis = local +Y, disc opening spanning y in [0, D] over (x,z) center.
//       * Side hole:  axis = derived radial (precomputed unit axis), pipe of depth D inward from the
//                     wall only (no exterior tunnel, no cross-bucket bore).

#ifndef OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED
#error "Include OpenTopCylinderBoundary.hlsl before OtcParticleField.hlsl (needs OTC_* tolerances)."
#endif

#define OTC_MAX_HOLES 16u

#define OTC_HOLE_SURFACE_FLOOR 0u
#define OTC_HOLE_SURFACE_SIDE  1u

// Region flag bits. In the parity phase these mirror the legacy predicates exactly (holeCount == 0);
// holes clear containment bits and set THROUGH_HOLE where the CSG carves an opening.
#define OTC_REGION_PARTICIPATES_PBF   (1u << 0)  // == OtcParticipatesInPbf
#define OTC_REGION_INSIDE_VOLUME      (1u << 1)  // == OtcInsideVolumeForFloorGate
#define OTC_REGION_INSIDE_RIGID_CARRY (1u << 2)  // == OtcIsInsideForRigidCarry
#define OTC_REGION_CANVAS_CLAIM       (1u << 3)  // == OtcCanvasCollisionApplies
#define OTC_REGION_BEAM               (1u << 4)  // beamBelowFloor (OtcBeamDespawnApplies minus canvasY)
#define OTC_REGION_THROUGH_HOLE       (1u << 5)  // new: particle sits in a hole opening

// Hole storage (fixed-size cbuffer array; 16-hole ceiling per design). Uploaded once at setup.
//   _Holes[i].xyz    = local-space position (projected onto surface at setup)
//   _Holes[i].w      = radius
//   _HoleAxis[i].xyz = precomputed unit axis (Floor: (0,1,0); Side: outward radial) — no per-particle normalize
//   _HoleAxis[i].w   = surface enum (0 = floor, 1 = side)
float4 _Holes[OTC_MAX_HOLES];
float4 _HoleAxis[OTC_MAX_HOLES];
uint   _HoleCount;
float  _HoleClipDepthScale;

// Phase 3 smooth participation blend. Fixed world-space (metres) width of the band over which a
// particle's PBF force participation ramps 0 -> 1 as its CSG signedDist crosses the boundary.
// 0 => exact legacy boolean behaviour (parity fallback). Default authored at 0.08 m to exceed the
// worst-case single-frame sweep penetration (~8 cm/frame at ~5 m/s) so a fast sweep still lands on
// the ramp, not a snap. Consumed only by PBF force sites; events (canvas/beam/carry) stay boolean.
float  _OtcParticipationFadeWidth;

struct OtcFieldSample
{
    float  signedDist;   // < 0 inside the cup cavity (after hole subtraction)
    float3 normal;       // outward gradient of the CSG field (unit-ish; unused until clamp migration)
    uint   region;       // OTC_REGION_* bit flags
};

// Packed per-particle classification written by ClassifyParticleFieldKernel, read by PBF/carry.
struct OtcParticleClassification
{
    float  signedDist;
    uint   regionFlags;
    float2 pad;
};

#ifndef OTC_PARTICLE_FIELD_CLASSIFY_PASS
StructuredBuffer<OtcParticleClassification> _ParticleField;
#endif

// --- Primitive SDFs (container-local) -------------------------------------------------------------

// Open-top cylinder cavity: intersection of { r <= R } and { y >= 0 }, unbounded above.
float OtcCavitySdf(float3 localPos)
{
    float radial = length(localPos.xz) - _ContainerRadius; // < 0 inside radius
    float floorHalf = -localPos.y;                          // < 0 above floor; open top (no upper bound)
    return max(radial, floorHalf);
}

// Floor hole: vertical disc opening spanning y in [0, depth] over center (x,z).
float OtcHoleSdfFloor(float3 localPos, float3 holePos, float radius, float depth)
{
    float radial = length(localPos.xz - holePos.xz) - radius;
    float slab = max(localPos.y - depth, -localPos.y); // < 0 for 0 < y < depth
    return max(radial, slab);
}

// Side hole: radial pipe of depth `depth` inward from the wall surface only.
float OtcHoleSdfSide(float3 localPos, float3 holePos, float3 axis, float radius, float depth)
{
    float3 tangent = float3(-axis.z, 0.0, axis.x);
    float3 v = localPos - holePos;
    float2 perp = float2(dot(v, tangent), v.y); // components perpendicular to the radial axis
    float radial = length(perp) - radius;
    float vIn = -dot(v, axis);                  // 0 at wall, > 0 inward
    float slab = max(-vIn, vIn - depth);        // < 0 for 0 < vIn < depth (inward only)
    return max(radial, slab);
}

float OtcHoleSdf(float3 localPos, uint i)
{
    float3 holePos = _Holes[i].xyz;
    float radius = _Holes[i].w;
    float depth = _HoleClipDepthScale * radius;
    // surface stored as float in _HoleAxis[i].w (0 = floor, 1 = side); compare as float to avoid
    // a signed/unsigned warning against the uint OTC_HOLE_SURFACE_* enum values.
    if (_HoleAxis[i].w < 0.5)
    {
        return OtcHoleSdfFloor(localPos, holePos, radius, depth);
    }
    return OtcHoleSdfSide(localPos, holePos, _HoleAxis[i].xyz, radius, depth);
}

// Full CSG distance: cavity with holes subtracted. max(cup, max_i(-hole_i)).
float OtcFieldDistance(float3 localPos)
{
    float d = OtcCavitySdf(localPos);
    [loop]
    for (uint i = 0u; i < _HoleCount; i++)
    {
        d = max(d, -OtcHoleSdf(localPos, i));
    }
    return d;
}

// --- Full sample ---------------------------------------------------------------------------------

OtcFieldSample OtcSampleField(float3 localPos)
{
    float cavity = OtcCavitySdf(localPos);
    float dist = cavity;
    [loop]
    for (uint i = 0u; i < _HoleCount; i++)
    {
        dist = max(dist, -OtcHoleSdf(localPos, i));
    }

    // Central-difference normal on the CSG field (not consumed in Phase 1a; kept for the eventual
    // clamp-from-normal migration). Cheap enough for the non-hot classify pass.
    const float e = 1e-3;
    float3 n = float3(
        OtcFieldDistance(localPos + float3(e, 0, 0)) - OtcFieldDistance(localPos - float3(e, 0, 0)),
        OtcFieldDistance(localPos + float3(0, e, 0)) - OtcFieldDistance(localPos - float3(0, e, 0)),
        OtcFieldDistance(localPos + float3(0, 0, e)) - OtcFieldDistance(localPos - float3(0, 0, e)));
    float nLen = length(n);
    n = nLen > 1e-8 ? n / nLen : float3(0, 1, 0);

    // Region flags — parity-exact reconstruction of the legacy predicates (axis-separated logic,
    // legacy tolerances). These are NOT derived from `dist`, because the legacy predicates use
    // independent radial/vertical tests and different epsilons that a single scalar cannot reproduce.
    float r = length(localPos.xz);
    bool insideRadius = r <= _ContainerRadius;
    bool insideRadiusEps = r <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS;
    bool aboveFloorBand = localPos.y >= -OTC_FLOOR_GATE_TOLERANCE;
    bool belowFloorBand = localPos.y < -OTC_FLOOR_GATE_TOLERANCE;
    bool withinHeight = localPos.y <= _ContainerHeight;
    bool aboveFloorStrict = localPos.y >= 0.0;
    bool outsideFootprint = r > _ContainerRadius;
    bool beamBelowFloor = (r <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS) && belowFloorBand;

    uint region = 0u;
    if (insideRadius && aboveFloorBand)                 region |= OTC_REGION_PARTICIPATES_PBF;
    if (aboveFloorBand && withinHeight && insideRadiusEps) region |= OTC_REGION_INSIDE_VOLUME;
    if (aboveFloorStrict && withinHeight && insideRadius)  region |= OTC_REGION_INSIDE_RIGID_CARRY;
    if (outsideFootprint || beamBelowFloor)             region |= OTC_REGION_CANVAS_CLAIM;
    if (beamBelowFloor)                                 region |= OTC_REGION_BEAM;

    // Hole modification: a cavity-interior point pushed outside by CSG is in an opening. Clear
    // containment (it is leaving), flag it, and mark it canvas-claimable so it can be caught later.
    bool throughHole = (cavity < 0.0) && (dist > 0.0);
    if (throughHole)
    {
        region &= ~(OTC_REGION_PARTICIPATES_PBF | OTC_REGION_INSIDE_VOLUME | OTC_REGION_INSIDE_RIGID_CARRY);
        region |= OTC_REGION_THROUGH_HOLE;
        region |= OTC_REGION_CANVAS_CLAIM;
    }

    OtcFieldSample s;
    s.signedDist = dist;
    s.normal = n;
    s.region = region;
    return s;
}

// --- Smooth participation weight (Phase 3) -------------------------------------------------------

// Continuous PBF participation weight from the CSG signed distance.
//   signedDist <= -fadeWidth : w = 1 (fully inside, stock PBF)
//   signedDist  =  0         : w = 0 (at the boundary)
//   signedDist >= 0          : w = 0 (outside / through-hole / beam)
// This REPLACES the hard OTC_REGION_PARTICIPATES_PBF boolean at PBF force sites once consumers are
// migrated (later steps). A fadeWidth of 0 reproduces the boolean gate exactly (parity fallback).
float OtcFieldParticipationWeightFromDist(float signedDist)
{
    if (_OtcParticipationFadeWidth <= 1e-6)
    {
        return signedDist < 0.0 ? 1.0 : 0.0;
    }
    return smoothstep(0.0, _OtcParticipationFadeWidth, -signedDist);
}

// --- Buffer + inline final-position accessors (Phase 1b) -----------------------------------------

#ifndef OTC_PARTICLE_FIELD_CLASSIFY_PASS
bool OtcFieldParticipatesInPbf(uint particleIndex)
{
    return (_ParticleField[particleIndex].regionFlags & OTC_REGION_PARTICIPATES_PBF) != 0u;
}

// Per-particle smooth participation weight (reads the classified field buffer).
float OtcFieldParticipationWeight(uint particleIndex)
{
    return OtcFieldParticipationWeightFromDist(_ParticleField[particleIndex].signedDist);
}

bool OtcFieldInsideVolume(uint particleIndex)
{
    return (_ParticleField[particleIndex].regionFlags & OTC_REGION_INSIDE_VOLUME) != 0u;
}

bool OtcFieldInsideRigidCarry(uint particleIndex)
{
    return (_ParticleField[particleIndex].regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0u;
}
#endif

bool OtcFieldCanvasClaimAtLocal(float3 localPos)
{
    return (OtcSampleField(localPos).region & OTC_REGION_CANVAS_CLAIM) != 0u;
}

bool OtcFieldBeamDespawnAtWorld(float3 worldPos, float canvasPlaneY)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    OtcFieldSample s = OtcSampleField(localPos);
    return (s.region & OTC_REGION_BEAM) != 0u && worldPos.y <= canvasPlaneY;
}

// Hole openings carve through the floor/wall surface; legacy OtcClampToContainer still treats the
// footprint as solid. Suppress floor clamp under any floor-hole disc so fluid can fall through the
// opening (THROUGH_HOLE alone is insufficient below y=0 where cavity SDF turns positive).
bool OtcFieldSuppressesFloorClamp(float3 localPos)
{
    if (_HoleCount == 0u)
    {
        return false;
    }

    [loop]
    for (uint i = 0u; i < _HoleCount; i++)
    {
        if ((uint)_HoleAxis[i].w != OTC_HOLE_SURFACE_FLOOR)
        {
            continue;
        }

        float3 holePos = _Holes[i].xyz;
        float holeRadius = _Holes[i].w;
        float2 delta = localPos.xz - holePos.xz;
        if (length(delta) <= holeRadius)
        {
            return true;
        }
    }

    return false;
}

#endif // OTC_PARTICLE_FIELD_INCLUDED
