#ifndef OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED
#define OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED

// Uniforms expected by including shader:
// float4x4 _ContainerLocalToWorld, _ContainerWorldToLocal
// int _ContainerUsesOrientation
// float _ContainerHeight, _ContainerRadius, _ContainerFloorY, _ContainerRimY
// float3 _ContainerCenter, _ContainerFloorPivot
// float _ContainerRestitution, _ContainerFriction

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

// PBF only within the footprint so fluid cannot couple across solid walls.
// Slosh above the rim (y > height, r <= radius) still participates.
bool OtcParticipatesInPbf(float3 worldPos)
{
    return !OtcIsOutsideFootprint(worldPos);
}

void OtcApplyWallClampInLocal(inout float3 localPos, inout float3 localVel, float radialDist)
{
    if (localPos.y > _ContainerHeight || radialDist <= _ContainerRadius || radialDist < 1e-5)
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
void OtcClampToContainer(inout float3 pos, inout float3 vel)
{
    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(pos, 1.0)).xyz;
        float3 localVel = mul((float3x3)_ContainerWorldToLocal, vel);
        float radialDist = length(localPos.xz);

        // Floor: only under the bucket footprint (y < 0 && r <= radius).
        if (localPos.y < 0.0 && radialDist <= _ContainerRadius)
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

    if (pos.y < _ContainerFloorY && radialDistWorld <= _ContainerRadius)
    {
        pos.y = _ContainerFloorY;
        if (vel.y < 0.0)
        {
            vel.y = -vel.y * _ContainerRestitution;
        }

        vel.x *= _ContainerFriction;
        vel.z *= _ContainerFriction;
    }

    if (pos.y <= localTop && radialDistWorld > _ContainerRadius && radialDistWorld > 1e-5)
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

void OtcClampToContainerPosition(inout float3 pos)
{
    float3 vel = float3(0.0, 0.0, 0.0);
    OtcClampToContainer(pos, vel);
}

#endif
