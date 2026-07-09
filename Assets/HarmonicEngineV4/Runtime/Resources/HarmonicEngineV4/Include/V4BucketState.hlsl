#ifndef HARMONIC_V4_BUCKET_STATE_INCLUDED
#define HARMONIC_V4_BUCKET_STATE_INCLUDED

// GPU bucket kinematics state (single element buffer).
// Written by V4BucketPhysics.compute; read by classification, forces, and PBF passes.

struct V4BucketState
{
    float3 pivot;
    float ropeLength;
    float3 unitDirection;
    float _pad0;
    float3 tangentialVelocity;
    float totalFluidMass;
    float3 comOffsetWorld;
    float fluidMassSmoothing;
    float3 worldOrigin;
    float twistDegrees;
    float4 worldRotation;
    float3 linearVelocity;
    float gravity;
    float3 angularVelocity;
    float damping;
    float3 angularAcceleration;
    float sloshFeedbackScale;
    float bucketMass;
    float applySlosh;
    float linearAccelerationX;
    float linearAccelerationY;
    float linearAccelerationZ;
    float useGpuState;
};

float3 V4BucketProjectOntoTangentPlane(float3 value, float3 unitDirection)
{
    return value - dot(value, unitDirection) * unitDirection;
}

float3 V4BucketNormalizeDirection(float3 direction, float3 fallback)
{
    float lenSq = dot(direction, direction);
    if (lenSq < 1e-8)
    {
        direction = dot(fallback, fallback) > 1e-8 ? fallback : float3(0.0, -1.0, 0.0);
    }
    return normalize(direction);
}

float3x3 V4BucketRotationMatrix(float4 quaternion)
{
    float4 q = normalize(quaternion);
    float x = q.x, y = q.y, z = q.z, w = q.w;
    return float3x3(
        1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y - z * w), 2.0 * (x * z + y * w),
        2.0 * (x * y + z * w), 1.0 - 2.0 * (x * x + z * z), 2.0 * (y * z - x * w),
        2.0 * (x * z - y * w), 2.0 * (y * z + x * w), 1.0 - 2.0 * (x * x + y * y));
}

float4x4 V4BucketLocalToWorldMatrix(V4BucketState state)
{
    float3x3 rot = V4BucketRotationMatrix(state.worldRotation);
    // HLSL matrices are column-major: store rotation columns (Unity localToWorld basis).
    float3 col0 = float3(rot[0].x, rot[1].x, rot[2].x);
    float3 col1 = float3(rot[0].y, rot[1].y, rot[2].y);
    float3 col2 = float3(rot[0].z, rot[1].z, rot[2].z);
    return float4x4(
        float4(col0, 0.0),
        float4(col1, 0.0),
        float4(col2, 0.0),
        float4(state.worldOrigin, 1.0));
}

float4x4 V4BucketWorldToLocalMatrix(V4BucketState state)
{
    float3x3 rot = V4BucketRotationMatrix(state.worldRotation);
    // Inverse of localToWorld (orthogonal): rotation part is transpose(R).
    float3 col0 = float3(rot[0].x, rot[1].x, rot[2].x);
    float3 col1 = float3(rot[0].y, rot[1].y, rot[2].y);
    float3 col2 = float3(rot[0].z, rot[1].z, rot[2].z);
    float3 origin = state.worldOrigin;
    float3 t = float3(-dot(col0, origin), -dot(col1, origin), -dot(col2, origin));
    return float4x4(
        float4(col0, 0.0),
        float4(col1, 0.0),
        float4(col2, 0.0),
        float4(t, 1.0));
}

float3 V4BucketContactVelocity(V4BucketState state, float3 worldPos)
{
    return state.linearVelocity + cross(state.angularVelocity, worldPos - state.worldOrigin);
}

float4 V4BucketQuatMul(float4 a, float4 b)
{
    return float4(
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
}

float4 V4BucketQuatFromToRotation(float3 fromDir, float3 toDir)
{
    fromDir = normalize(fromDir);
    toDir = normalize(toDir);
    float dotDirs = dot(fromDir, toDir);

    if (dotDirs > 0.999999)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }

    if (dotDirs < -0.999999)
    {
        float3 axis = cross(fromDir, float3(1.0, 0.0, 0.0));
        if (dot(axis, axis) < 1e-8)
        {
            axis = cross(fromDir, float3(0.0, 0.0, 1.0));
        }
        axis = normalize(axis);
        return float4(axis, 0.0);
    }

    float3 axis = cross(fromDir, toDir);
    float w = 1.0 + dotDirs;
    return normalize(float4(axis, w));
}

float4 V4BucketComputeRotation(float3 unitDirection, float twistDegrees)
{
    float3 ropeDir = V4BucketNormalizeDirection(unitDirection, float3(0.0, -1.0, 0.0));
    float3 towardPivot = -ropeDir;
    float4 q = V4BucketQuatFromToRotation(float3(0.0, 1.0, 0.0), towardPivot);

    if (abs(twistDegrees) > 1e-4)
    {
        float half = radians(twistDegrees) * 0.5;
        float4 twistQ = float4(ropeDir * sin(half), cos(half));
        q = V4BucketQuatMul(twistQ, q);
    }
    return normalize(q);
}

#endif
