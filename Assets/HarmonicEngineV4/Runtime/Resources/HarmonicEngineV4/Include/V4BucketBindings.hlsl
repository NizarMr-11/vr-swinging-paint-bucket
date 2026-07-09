#ifndef HARMONIC_V4_BUCKET_BINDINGS_INCLUDED
#define HARMONIC_V4_BUCKET_BINDINGS_INCLUDED

#include "Include/V4BucketState.hlsl"

StructuredBuffer<V4BucketState> _BucketState;

// Authoritative bucket pose from Unity transform (set each frame from CPU).
// Velocities and accelerations come from _BucketState after GPU integrate.
float4x4 _BucketWorldToLocal;
float4x4 _BucketLocalToWorld;
float3 _BucketWorldOrigin;

void V4LoadBucketKinematics(
    out float4x4 worldToLocal,
    out float4x4 localToWorld,
    out float3 linearVelocity,
    out float3 angularVelocity,
    out float3 angularAcceleration,
    out float3 linearAcceleration,
    out float3 worldOrigin)
{
    V4BucketState state = _BucketState[0];
    worldToLocal = _BucketWorldToLocal;
    localToWorld = _BucketLocalToWorld;
    worldOrigin = _BucketWorldOrigin;
    linearVelocity = state.linearVelocity;
    angularVelocity = state.angularVelocity;
    angularAcceleration = state.angularAcceleration;
    linearAcceleration = float3(
        state.linearAccelerationX,
        state.linearAccelerationY,
        state.linearAccelerationZ);
}

float3 V4BucketContactVelAt(float3 worldPos)
{
    V4BucketState state = _BucketState[0];
    return state.linearVelocity + cross(state.angularVelocity, worldPos - _BucketWorldOrigin);
}

#endif
