#ifndef HARMONIC_V4_BUCKET_ZONES_INCLUDED
#define HARMONIC_V4_BUCKET_ZONES_INCLUDED

// =============================================================================
//  V4BucketZones.hlsl - bucket geometry + zone classification.
//
//  One-to-one transliteration of the CPU reference (V4BucketGeometry.cs and
//  V4ZoneMath.cs). GPU/CPU parity tests assert both sides agree; do not change
//  one side without the other.
//
//  Bucket-local space: origin at floor center, +Y up, rim at y = _BucketHeight.
//  Uniform contract (set by the pipeline every frame; bake-static ones at init):
//    float4x4 _BucketWorldToLocal;
//    float4x4 _BucketLocalToWorld;
//    float    _BucketInnerRadius;
//    float    _BucketHeight;
//    float    _BucketWallThickness;
//    float    _TopBandHeight;
//    uint     _HoleCount;
//    StructuredBuffer<V4Hole> _Holes;   (declared by the including shader)
// =============================================================================

#include "V4Common.hlsl"

struct V4Hole
{
    float3 localPosition;
    float radius;
    float3 outwardNormal;
    float d0;
    float d1;
    float d2;
    float pad0;
    float pad1;
};

// --- Geometry (mirror of V4BucketGeometry.cs) --------------------------------

bool V4BucketIsInside(float3 localPos, float innerRadius, float height)
{
    if (localPos.y < 0.0 || localPos.y > height)
    {
        return false;
    }

    float r2 = localPos.x * localPos.x + localPos.z * localPos.z;
    return r2 <= innerRadius * innerRadius;
}

// Footprint containment for collision (not classification). Includes brief y < 0 lag when
// the bucket moves and fluid trails in world space; deep sub-floor positions are excluded.
bool V4BucketShouldContainInside(float3 localPos, float innerRadius, float height, float wallThickness)
{
    if (localPos.y > height)
    {
        return false;
    }

    float floorLag = max(wallThickness * 4.0, 0.02);
    if (localPos.y < -floorLag)
    {
        return false;
    }

    float r2 = localPos.x * localPos.x + localPos.z * localPos.z;
    return r2 <= innerRadius * innerRadius;
}

bool V4BucketIsInSolidShell(float3 localPos, float innerRadius, float height, float wallThickness)
{
    float outerRadius = innerRadius + wallThickness;
    float r = sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

    bool inWallBand = r > innerRadius && r < outerRadius && localPos.y >= -wallThickness && localPos.y <= height;
    bool inFloorSlab = r <= innerRadius && localPos.y > -wallThickness && localPos.y < 0.0;
    return inWallBand || inFloorSlab;
}

void V4ReflectAgainstNormal(inout float3 vel, float3 normal, float restitution, float friction)
{
    float vn = dot(vel, normal);
    if (vn >= 0.0)
    {
        return;
    }

    float3 normalComponent = normal * vn;
    float3 tangential = vel - normalComponent;
    vel = tangential * (1.0 - friction) - normalComponent * restitution;
}

// Reflect in the wall/floor's moving frame: subtract contact point velocity before
// reflection, then add it back so a co-moving particle sees a stationary boundary.
void V4ReflectAgainstNormalInMovingFrame(
    inout float3 vel,
    float3 normal,
    float3 contactVel,
    float restitution,
    float friction)
{
    float3 relVel = vel - contactVel;
    V4ReflectAgainstNormal(relVel, normal, restitution, friction);
    vel = relVel + contactVel;
}

// containInside must be true for particles flagged Inside: they are clamped back into
// the cavity no matter how deep they penetrated (a moving bucket can sweep its shell
// through a particle in one step; nearest-face resolution would eject it through the
// wall). Outside-flagged particles use nearest-face resolution: wall/floor-touching
// particles drift epsilon outside via float rounding and are classified Outside for a
// frame - they must come back to the inside face, not be ejected. Open top stays free.
void V4BucketResolveCollision(
    inout float3 localPos,
    inout float3 localVel,
    float innerRadius,
    float height,
    float wallThickness,
    float restitution,
    float friction,
    bool containInside,
    float3 contactVelLocal)
{
    float outerRadius = innerRadius + wallThickness;
    float r = sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

    if (localPos.y > height)
    {
        return;
    }

    if (containInside)
    {
        if (localPos.y < 0.0)
        {
            localPos.y = 0.0;
            V4ReflectAgainstNormalInMovingFrame(localVel, float3(0, 1, 0), contactVelLocal, restitution, friction);
        }

        // Face-contact band (not just r > R): the solver-loop position clamps pin
        // pressurized particles at exactly r = R, so Finalize never sees them beyond
        // the face - without the band their outward jet velocity is never reflected,
        // accumulates across frames, and one flag-flicker frame hops them past the
        // wall mid-plane where nearest-face resolution ejects them.
        if (r >= innerRadius - 1e-4)
        {
            if (r > innerRadius)
            {
                float safeR = max(r, 1e-6);
                float scale = innerRadius / safeR;
                localPos.x *= scale;
                localPos.z *= scale;
            }

            float rNow = max(sqrt(localPos.x * localPos.x + localPos.z * localPos.z), 1e-6);
            float3 radialDir = float3(localPos.x, 0.0, localPos.z) / rNow;
            V4ReflectAgainstNormalInMovingFrame(localVel, -radialDir, contactVelLocal, restitution, friction);
        }

        return;
    }

    if (r <= innerRadius)
    {
        // Outside-flagged particle in the cavity footprint: nearest-face resolution
        // (see CPU reference for the float-noise rationale).
        if (localPos.y < 0.0)
        {
            if (localPos.y > -wallThickness)
            {
                bool fromAbove = localPos.y > -wallThickness * 0.5;
                localPos.y = fromAbove ? 0.0 : -wallThickness;
                float3 normal = fromAbove ? float3(0, 1, 0) : float3(0, -1, 0);
                V4ReflectAgainstNormalInMovingFrame(localVel, normal, contactVelLocal, restitution, friction);
            }

            return;
        }

        return;
    }

    if (r >= outerRadius)
    {
        float safeR = max(r, 1e-6);
        float scale = outerRadius / safeR;
        localPos.x *= scale;
        localPos.z *= scale;

        float3 radialDir = float3(localPos.x, 0.0, localPos.z) / outerRadius;
        V4ReflectAgainstNormalInMovingFrame(localVel, -radialDir, contactVelLocal, restitution, friction);
        return;
    }

    // Outside-flagged particle in the wall band: resolve to the nearer face so
    // wall-touching particles misclassified by float noise come back inside.
    // Lateral slosh can push them slightly below the floor while still in the
    // wall band; lift to the inner floor before the radial resolve.
    if (localPos.y < 0.0)
    {
        localPos.y = 0.0;
        V4ReflectAgainstNormalInMovingFrame(localVel, float3(0, 1, 0), contactVelLocal, restitution, friction);
    }

    float toInner = r - innerRadius;
    float toOuter = outerRadius - r;
    bool resolveToInner = toInner <= toOuter;

    float targetR = resolveToInner ? innerRadius : outerRadius;
    float safeR = max(r, 1e-6);
    float scale = targetR / safeR;
    localPos.x *= scale;
    localPos.z *= scale;

    float3 radialDir = float3(localPos.x, 0.0, localPos.z) / max(targetR, 1e-6);
    float3 wallNormal = resolveToInner ? -radialDir : radialDir;
    V4ReflectAgainstNormalInMovingFrame(localVel, wallNormal, contactVelLocal, restitution, friction);
}

// --- Zones (mirror of V4ZoneMath.cs) -----------------------------------------

uint V4ClassifyAgainstHole(float3 localPos, V4Hole hole)
{
    float dist = distance(localPos, hole.localPosition);
    if (dist <= hole.d0)
    {
        return V4_ZONE_HOLE0;
    }

    if (dist <= hole.d1)
    {
        return V4_ZONE_HOLE1;
    }

    if (dist <= hole.d2)
    {
        return V4_ZONE_HOLE2;
    }

    return V4_ZONE_NONE;
}

// Priority-based classification: Level 1 hole zones first (single nearest owner),
// Level 2 top band only when no hole claims the particle. Returns zone id and
// writes the owning hole index.
uint V4ClassifyZones(
    float3 localPos,
    bool inside,
    StructuredBuffer<V4Hole> holes,
    uint holeCount,
    float bucketHeight,
    float topBandHeight,
    out uint holeIndex)
{
    holeIndex = 0u;
    if (!inside)
    {
        return V4_ZONE_NONE;
    }

    int bestHole = -1;
    float bestDist = 3.402823466e+38;
    for (uint i = 0u; i < holeCount; i++)
    {
        float dist = distance(localPos, holes[i].localPosition);
        if (dist <= holes[i].d2 && dist < bestDist)
        {
            bestDist = dist;
            bestHole = (int)i;
        }
    }

    if (bestHole >= 0)
    {
        holeIndex = (uint)bestHole;
        return V4ClassifyAgainstHole(localPos, holes[bestHole]);
    }

    if (localPos.y >= bucketHeight - topBandHeight)
    {
        return V4_ZONE_TOPBAND;
    }

    return V4_ZONE_NONE;
}

// Zone force direction: Zone 0 ejects along the hole's outward normal;
// Zones 1/2 pull toward the hole opening.
float3 V4ZoneForceDirection(float3 localPos, V4Hole hole, uint zone)
{
    if (zone == V4_ZONE_HOLE0)
    {
        return hole.outwardNormal;
    }

    float3 toHole = hole.localPosition - localPos;
    float len = length(toHole);
    return len > 1e-6 ? toHole / len : hole.outwardNormal;
}

#endif // HARMONIC_V4_BUCKET_ZONES_INCLUDED
