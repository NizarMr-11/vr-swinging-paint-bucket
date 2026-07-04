#ifndef OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED
#define OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED

// Uniforms expected by including shader:
// float4x4 _ContainerLocalToWorld, _ContainerWorldToLocal
// int _ContainerUsesOrientation
// float _ContainerHeight, _ContainerRadius, _ContainerFloorY, _ContainerRimY
// float3 _ContainerCenter, _ContainerFloorPivot
// float _ContainerRestitution, _ContainerFriction

// Shared tolerance so floor (r <= radius) and wall (r > radius) meet without an FP gap
// at the corner (diagnostic: r = 0.55000010 with y < 0 missed floor, wall skipped xz-only).
static const float OTC_RADIAL_BOUNDARY_EPS = 1e-4f;
static const float OTC_WALL_CROSS_MARGIN = 1e-4f;
static const float OTC_WALL_CAPTURE_MARGIN = 0.05f;

// Downward floor tolerance. Mirrors OTC_RADIAL_BOUNDARY_EPS philosophy: a resting particle pinned at
// localY = 0 by last frame's clamp can read back a hair below zero after the world<->local FP
// round-trip, so we admit a thin band below the floor before treating a particle as "below the cup"
// (excluded from PBF, subject to canvas collision). Genuine sub-floor "beam" / swept debris sits well
// below this band. Shared by the Zone-B floor-clamp continuity gate and the Zone-A beam predicates.
static const float OTC_FLOOR_GATE_TOLERANCE = 0.01f; // 1 cm; tunable

// Rigid-body carry: inside the solid cylinder footprint up to the rim.
bool OtcIsInsideForRigidCarry(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    return localPos.y >= 0.0 && localPos.y <= _ContainerHeight
        && length(localPos.xz) <= _ContainerRadius;
}

// Spilled over the rim: above the top plane AND outside the wall cylinder.
bool OtcIsSpilledOutside(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    float radialDist = length(localPos.xz);
    return localPos.y > _ContainerHeight && radialDist > _ContainerRadius;
}

// Outside the cylindrical footprint (exterior wall, spilled runoff, under-bucket free fall).
bool OtcIsOutsideFootprint(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    return length(localPos.xz) > _ContainerRadius;
}

// PBF only within the footprint so fluid cannot couple across solid walls, AND at/above the floor so
// sub-floor "beam" particles (radially inside the column but never in the 3D cup) do not couple into
// the density/pressure solve. NO upper bound: slosh above the rim (y > height, r <= radius) still
// participates so it can re-enter the cup through the open top. This is the Zone-A companion to the
// Zone-B floor-clamp gate — same OTC_FLOOR_GATE_TOLERANCE band below the floor.
bool OtcParticipatesInPbf(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    return length(localPos.xz) <= _ContainerRadius
        && localPos.y >= -OTC_FLOOR_GATE_TOLERANCE;
}

// Canvas collision claim (Option A, beam-only): a particle should be caught by the canvas whenever it
// is NOT genuinely inside the cup for collision purposes — either radially outside the footprint
// (existing spill/exterior behavior) OR under the column but below the floor band (the Zone-A beam,
// radially inside yet never in the 3D cup). Slosh above the rim (localY > height, r <= radius) is
// intentionally NOT captured here: it stays aloft and re-enters PBF via the open top (see
// SpillPhysicsTests.ExteriorParticle_ReEntersPbfViaOpenTop_WhenTrajectoryCrossesFootprintAboveRim).
bool OtcCanvasCollisionApplies(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    float radialDist = length(localPos.xz);
    bool outsideFootprint = radialDist > _ContainerRadius;
    bool beamBelowFloor = radialDist <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS
        && localPos.y < -OTC_FLOOR_GATE_TOLERANCE;
    return outsideFootprint || beamBelowFloor;
}

// Beam despawn claim (Zone-A cleanup): a particle that is the sub-floor beam (radially inside the
// footprint but below the floor band, i.e. the beamBelowFloor half of OtcCanvasCollisionApplies) AND
// has reached/passed the canvas plane. Such particles are excluded from PBF (no self-repulsion) and,
// once arrested at the canvas, only stack into a degenerate pile — so integration drops them instead
// of writing a survivor (their canvas hit is recorded separately before the drop). Deliberately
// beam-only: exterior/rim-spill particles (radialDist > radius) are NOT despawned here so their
// existing lifecycle is untouched. canvasPlaneY is passed in so this header stays independent of any
// particular shader's canvas uniform.
bool OtcBeamDespawnApplies(float3 worldPos, float canvasPlaneY)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    float radialDist = length(localPos.xz);
    bool beamBelowFloor = radialDist <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS
        && localPos.y < -OTC_FLOOR_GATE_TOLERANCE;
    return beamBelowFloor && worldPos.y <= canvasPlaneY;
}

bool OtcRadialInsideFloorFootprint(float radialDist)
{
    return radialDist <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS;
}

bool OtcRadialOutsideWallCylinder(float radialDist)
{
    return radialDist > _ContainerRadius - OTC_RADIAL_BOUNDARY_EPS;
}

// Volume containment used to gate the floor clamp by continuity: was this particle genuinely
// inside the cup (0 <= y <= rim, r <= radius) — not merely under the infinite radial column — at
// frame start? Shares OtcIsInsideForRigidCarry's containment so carry and floor-clamp agree on
// "in the cup", plus the small downward floor tolerance for FP robustness.
bool OtcInsideVolumeForFloorGate(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    return localPos.y >= -OTC_FLOOR_GATE_TOLERANCE && localPos.y <= _ContainerHeight
        && length(localPos.xz) <= _ContainerRadius + OTC_RADIAL_BOUNDARY_EPS;
}

void OtcApplyWallClampInLocal(inout float3 localPos, inout float3 localVel, float radialDist)
{
    if (localPos.y > _ContainerHeight || !OtcRadialOutsideWallCylinder(radialDist) || radialDist < 1e-5)
    {
        return;
    }

    float2 n = localPos.xz / radialDist;
    localPos.xz = n * _ContainerRadius;

    float vn = dot(localVel.xz, n);
    if (abs(vn) > 1e-6)
    {
        float2 vNormal = vn * n;
        float2 vTangent = localVel.xz - vNormal;
        localVel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
    }
}

void OtcApplyWallClampInLocalBounded(
    inout float3 localPos,
    inout float3 localVel,
    float radialDist,
    float prevRadialDist)
{
    if (localPos.y > _ContainerHeight || !OtcRadialOutsideWallCylinder(radialDist) || radialDist < 1e-5)
    {
        return;
    }

    // Clamp only when the particle either crossed out from interior this step
    // or was already wall-adjacent and should keep sliding along the wall.
    bool crossedOutward = prevRadialDist <= _ContainerRadius + OTC_WALL_CROSS_MARGIN
        && radialDist > prevRadialDist + OTC_WALL_CROSS_MARGIN;
    bool wallAdjacent = prevRadialDist <= _ContainerRadius + OTC_WALL_CAPTURE_MARGIN;
    bool driftingOutward = radialDist >= prevRadialDist - OTC_WALL_CROSS_MARGIN;
    if (!crossedOutward && !(wallAdjacent && driftingOutward))
    {
        return;
    }

    float2 n = localPos.xz / radialDist;
    localPos.xz = n * _ContainerRadius;

    float vn = dot(localVel.xz, n);
    if (abs(vn) > 1e-6)
    {
        float2 vNormal = vn * n;
        float2 vTangent = localVel.xz - vNormal;
        localVel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
    }
}

// Bidirectional open-top cylinder: solid floor (inside footprint), solid walls to rim, open top.
// floorClampAllowed gates the floor teleport by volume-continuity (see OtcInsideVolumeForFloorGate):
// when false, a particle below the floor is left to ordinary gravity/canvas physics instead of being
// snapped up to the floor plane (prevents swept ground debris from being launched). Walls/open-top
// are unaffected.
void OtcClampToContainer(inout float3 pos, inout float3 vel, bool floorClampAllowed)
{
    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(pos, 1.0)).xyz;
        float3 localVel = mul((float3x3)_ContainerWorldToLocal, vel);
        float radialDist = length(localPos.xz);

        // Floor: only under the bucket footprint (y < 0 && r <= radius [+ eps at wall]).
        if (floorClampAllowed && localPos.y < 0.0 && OtcRadialInsideFloorFootprint(radialDist))
        {
            localPos.y = 0.0;
            if (localVel.y < 0.0)
            {
                localVel.y = -localVel.y * _ContainerRestitution;
            }

            localVel.x *= _ContainerFriction;
            localVel.z *= _ContainerFriction;
        }

        // Walls: solid from both sides up to the rim (blocks interior leak + exterior re-entry).
        OtcApplyWallClampInLocal(localPos, localVel, radialDist);

        // Open top: y > height with r <= radius (slosh) or r > radius (spilled) — no wall clamp.

        pos = mul(_ContainerLocalToWorld, float4(localPos, 1.0)).xyz;
        vel = mul((float3x3)_ContainerLocalToWorld, localVel);
        return;
    }

    // Axis-aligned world-space cup.
    float localTop = _ContainerFloorY + _ContainerHeight;
    float2 relWorld = pos.xz - _ContainerCenter.xz;
    float radialDistWorld = length(relWorld);

    if (floorClampAllowed && pos.y < _ContainerFloorY && OtcRadialInsideFloorFootprint(radialDistWorld))
    {
        pos.y = _ContainerFloorY;
        if (vel.y < 0.0)
        {
            vel.y = -vel.y * _ContainerRestitution;
        }

        vel.x *= _ContainerFriction;
        vel.z *= _ContainerFriction;
    }

    if (pos.y <= localTop && OtcRadialOutsideWallCylinder(radialDistWorld) && radialDistWorld > 1e-5)
    {
        float2 n = relWorld / radialDistWorld;
        pos.xz = _ContainerCenter.xz + n * _ContainerRadius;

        float vn = dot(vel.xz, n);
        if (abs(vn) > 1e-6)
        {
            float2 vNormal = vn * n;
            float2 vTangent = vel.xz - vNormal;
            vel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
        }
    }
}

// Legacy unconditional variant (floor always clamps) — preserves existing behaviour for rigid
// carry and the boundary test kernels that don't participate in the volume-continuity gate.
void OtcClampToContainer(inout float3 pos, inout float3 vel)
{
    OtcClampToContainer(pos, vel, true);
}

// Predict/Apply bounded-wall variant: requires previous position so distant exterior
// particles are not pulled onto the wall while passing through the height band.
// floorClampAllowed gates the floor teleport by volume-continuity (see the 2-arg overload).
void OtcClampToContainer(inout float3 pos, inout float3 vel, float3 prevPos, bool floorClampAllowed)
{
    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(pos, 1.0)).xyz;
        float3 prevLocalPos = mul(_ContainerWorldToLocal, float4(prevPos, 1.0)).xyz;
        float3 localVel = mul((float3x3)_ContainerWorldToLocal, vel);
        float radialDist = length(localPos.xz);
        float prevRadialDist = length(prevLocalPos.xz);

        // Floor: only under the bucket footprint (y < 0 && r <= radius [+ eps at wall]).
        if (floorClampAllowed && localPos.y < 0.0 && OtcRadialInsideFloorFootprint(radialDist))
        {
            localPos.y = 0.0;
            if (localVel.y < 0.0)
            {
                localVel.y = -localVel.y * _ContainerRestitution;
            }

            localVel.x *= _ContainerFriction;
            localVel.z *= _ContainerFriction;
        }

        OtcApplyWallClampInLocalBounded(localPos, localVel, radialDist, prevRadialDist);

        pos = mul(_ContainerLocalToWorld, float4(localPos, 1.0)).xyz;
        vel = mul((float3x3)_ContainerLocalToWorld, localVel);
        return;
    }

    float localTop = _ContainerFloorY + _ContainerHeight;
    float2 relWorld = pos.xz - _ContainerCenter.xz;
    float2 prevRelWorld = prevPos.xz - _ContainerCenter.xz;
    float radialDistWorld = length(relWorld);
    float prevRadialDistWorld = length(prevRelWorld);

    if (floorClampAllowed && pos.y < _ContainerFloorY && OtcRadialInsideFloorFootprint(radialDistWorld))
    {
        pos.y = _ContainerFloorY;
        if (vel.y < 0.0)
        {
            vel.y = -vel.y * _ContainerRestitution;
        }

        vel.x *= _ContainerFriction;
        vel.z *= _ContainerFriction;
    }

    if (pos.y <= localTop && OtcRadialOutsideWallCylinder(radialDistWorld) && radialDistWorld > 1e-5)
    {
        bool crossedOutward = prevRadialDistWorld <= _ContainerRadius + OTC_WALL_CROSS_MARGIN
            && radialDistWorld > prevRadialDistWorld + OTC_WALL_CROSS_MARGIN;
        bool wallAdjacent = prevRadialDistWorld <= _ContainerRadius + OTC_WALL_CAPTURE_MARGIN;
        bool driftingOutward = radialDistWorld >= prevRadialDistWorld - OTC_WALL_CROSS_MARGIN;
        if (crossedOutward || (wallAdjacent && driftingOutward))
        {
            float2 n = relWorld / radialDistWorld;
            pos.xz = _ContainerCenter.xz + n * _ContainerRadius;

            float vn = dot(vel.xz, n);
            if (abs(vn) > 1e-6)
            {
                float2 vNormal = vn * n;
                float2 vTangent = vel.xz - vNormal;
                vel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
            }
        }
    }
}

// Legacy bounded variant (floor always clamps) — preserves existing behaviour for callers that do
// not participate in the volume-continuity gate (e.g. boundary test kernels).
void OtcClampToContainer(inout float3 pos, inout float3 vel, float3 prevPos)
{
    OtcClampToContainer(pos, vel, prevPos, true);
}

// Position-only floor for PBF solve iterations (velocity derived later in Apply). floorClampAllowed
// gates the floor teleport by volume-continuity (see the 2-arg overload).
void OtcClampToContainerPosition(inout float3 pos, bool floorClampAllowed)
{
    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(pos, 1.0)).xyz;
        float radialDist = length(localPos.xz);

        if (floorClampAllowed && localPos.y < 0.0 && OtcRadialInsideFloorFootprint(radialDist))
        {
            localPos.y = 0.0;
        }

        float3 vel = float3(0.0, 0.0, 0.0);
        OtcApplyWallClampInLocal(localPos, vel, radialDist);
        pos = mul(_ContainerLocalToWorld, float4(localPos, 1.0)).xyz;
        return;
    }

    float3 vel = float3(0.0, 0.0, 0.0);
    OtcClampToContainer(pos, vel, floorClampAllowed);
}

// Legacy position-only variant (floor always clamps).
void OtcClampToContainerPosition(inout float3 pos)
{
    OtcClampToContainerPosition(pos, true);
}

#endif
