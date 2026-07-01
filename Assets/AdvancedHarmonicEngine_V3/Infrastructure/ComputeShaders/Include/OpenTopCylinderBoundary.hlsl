#ifndef OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED
#define OPEN_TOP_CYLINDER_BOUNDARY_INCLUDED

// Uniforms expected by including shader:
// float4x4 _ContainerLocalToWorld, _ContainerWorldToLocal
// int _ContainerSpillEnabled, _ContainerUsesOrientation
// float _ContainerHeight, _ContainerRadius, _ContainerFloorY, _ContainerRimY
// float3 _ContainerCenter, _ContainerFloorPivot
// float _ContainerRestitution, _ContainerFriction

// Small radial margin so a particle that was just snapped exactly onto the wall (d == radius)
// is never mis-classified as "spilled" on the next float round-trip through the matrices.
#define OTC_SPILL_MARGIN 0.005

// Spill when the particle is above the rim AND outside the radius.
bool OtcShouldTransferToFalling(float3 worldPos)
{
    if (_ContainerSpillEnabled == 0)
    {
        return false;
    }

    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
        return localPos.y > _ContainerHeight && length(localPos.xz) > _ContainerRadius + OTC_SPILL_MARGIN;
    }

    float2 rel = worldPos.xz - _ContainerCenter.xz;
    float localTop = _ContainerFloorY + _ContainerHeight;
    return worldPos.y > localTop && length(rel) > _ContainerRadius + OTC_SPILL_MARGIN;
}

bool OtcIsInsideForRigidCarry(float3 worldPos)
{
    float3 localPos = mul(_ContainerWorldToLocal, float4(worldPos, 1.0)).xyz;
    return localPos.y <= _ContainerHeight && length(localPos.xz) <= _ContainerRadius;
}

// Open cup: infinite floor plane at local y = 0, cylindrical walls up to _ContainerHeight; open top above that.
// Particles that clear the rim and radius are handled by OtcShouldTransferToFalling.
void OtcClampToContainer(inout float3 pos, inout float3 vel)
{
    if (_ContainerUsesOrientation != 0)
    {
        float3 localPos = mul(_ContainerWorldToLocal, float4(pos, 1.0)).xyz;
        float3 localVel = mul((float3x3)_ContainerWorldToLocal, vel);

        // Infinite floor plane: catch all particles below local y = 0 (prevents leaks outside the footprint).
        if (localPos.y < 0.0)
        {
            localPos.y = 0.0;
            if (localVel.y < 0.0)
            {
                localVel.y = -localVel.y * _ContainerRestitution;
            }

            localVel.x *= _ContainerFriction;
            localVel.z *= _ContainerFriction;
        }

        // Side wall: solid from floor to rim; no clamp above _ContainerHeight (open top).
        if (localPos.y >= 0.0 && localPos.y <= _ContainerHeight)
        {
            float2 rel = localPos.xz;
            float d = length(rel);
            if (d > _ContainerRadius && d > 1e-5)
            {
                float2 n = rel / d;
                localPos.xz = n * _ContainerRadius;

                float vn = dot(localVel.xz, n);
                if (vn > 0.0)
                {
                    float2 vNormal = vn * n;
                    float2 vTangent = localVel.xz - vNormal;
                    localVel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
                }
            }
        }

        pos = mul(_ContainerLocalToWorld, float4(localPos, 1.0)).xyz;
        vel = mul((float3x3)_ContainerLocalToWorld, localVel);
        return;
    }

    // Axis-aligned world-space cup.
    float localTop = _ContainerFloorY + _ContainerHeight;

    if (pos.y < _ContainerFloorY)
    {
        pos.y = _ContainerFloorY;
        if (vel.y < 0.0)
        {
            vel.y = -vel.y * _ContainerRestitution;
        }

        vel.x *= _ContainerFriction;
        vel.z *= _ContainerFriction;
    }

    if (pos.y >= _ContainerFloorY && pos.y <= localTop)
    {
        float2 relWorld = pos.xz - _ContainerCenter.xz;
        float dWorld = length(relWorld);
        if (dWorld > _ContainerRadius && dWorld > 1e-5)
        {
            float2 n = relWorld / dWorld;
            pos.xz = _ContainerCenter.xz + n * _ContainerRadius;

            float vn = dot(vel.xz, n);
            if (vn > 0.0)
            {
                float2 vNormal = vn * n;
                float2 vTangent = vel.xz - vNormal;
                vel.xz = vTangent * _ContainerFriction - vNormal * _ContainerRestitution;
            }
        }
    }
}

#endif
