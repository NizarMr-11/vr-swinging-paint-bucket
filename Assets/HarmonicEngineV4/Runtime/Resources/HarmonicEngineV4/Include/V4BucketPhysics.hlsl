#ifndef HARMONIC_V4_BUCKET_PHYSICS_INCLUDED
#define HARMONIC_V4_BUCKET_PHYSICS_INCLUDED

#include "Include/V4BucketState.hlsl"

float3 V4BucketGravityTangent(float3 unitDirection, float gravity)
{
    float3 gravityAccel = float3(0.0, -gravity, 0.0);
    return V4BucketProjectOntoTangentPlane(gravityAccel, unitDirection);
}

float3 V4BucketComputeSloshAcceleration(
    float3 unitDirection,
    float3 comOffsetWorld,
    float fluidMass,
    float bucketMass,
    float ropeLength,
    float gravity,
    float feedbackScale)
{
    if (fluidMass <= 1e-6 || feedbackScale <= 1e-6)
    {
        return float3(0.0, 0.0, 0.0);
    }

    float totalMass = max(bucketMass + fluidMass, 1e-6);
    float3 weight = float3(0.0, -fluidMass * gravity, 0.0);
    float3 torque = cross(comOffsetWorld, weight);
    float3 angularAccel = torque / (totalMass * ropeLength * ropeLength);
    float3 tangential = V4BucketProjectOntoTangentPlane(angularAccel, unitDirection);
    return tangential * feedbackScale;
}

void V4BucketComputeAcceleration(
    float3 unitDirection,
    float3 tangentialVelocity,
    float3 comOffsetWorld,
    float fluidMass,
    float bucketMass,
    float ropeLength,
    float gravity,
    float damping,
    float sloshScale,
    float applySlosh,
    out float3 acceleration)
{
    float3 gravityTangent = V4BucketGravityTangent(unitDirection, gravity);
    float3 dampingAccel = -damping * tangentialVelocity;
    float3 sloshAccel = float3(0.0, 0.0, 0.0);
    if (applySlosh > 0.5)
    {
        sloshAccel = V4BucketComputeSloshAcceleration(
            unitDirection, comOffsetWorld, fluidMass, bucketMass,
            ropeLength, gravity, sloshScale);
    }
    acceleration = gravityTangent + dampingAccel + sloshAccel;
}

void V4BucketIntegrateVerlet(
    inout float3 unitDirection,
    inout float3 tangentialVelocity,
    float3 comOffsetWorld,
    float fluidMass,
    float bucketMass,
    float ropeLength,
    float gravity,
    float damping,
    float sloshScale,
    float applySlosh,
    float deltaTime)
{
    float3 accel0;
    V4BucketComputeAcceleration(
        unitDirection, tangentialVelocity, comOffsetWorld, fluidMass,
        bucketMass, ropeLength, gravity, damping, sloshScale, applySlosh, accel0);

    float3 vHalf = tangentialVelocity + accel0 * deltaTime;
    float safeL = max(ropeLength, 1e-6);
    unitDirection = V4BucketNormalizeDirection(
        unitDirection + (vHalf / safeL) * deltaTime,
        float3(0.0, -1.0, 0.0));

    float3 accel1;
    V4BucketComputeAcceleration(
        unitDirection, vHalf, comOffsetWorld, fluidMass,
        bucketMass, ropeLength, gravity, damping, sloshScale, applySlosh, accel1);

    tangentialVelocity = V4BucketProjectOntoTangentPlane(
        tangentialVelocity + 0.5 * (accel0 + accel1) * deltaTime,
        unitDirection);
}

#endif
