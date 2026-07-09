using System.Runtime.InteropServices;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>GPU mirror of V4BucketState in V4BucketState.hlsl (176 bytes).</summary>
    [StructLayout(LayoutKind.Sequential, Size = 176)]
    public struct V4GpuBucketState
    {
        public Vector3 pivot;
        public float ropeLength;
        public Vector3 unitDirection;
        public float pad0;
        public Vector3 tangentialVelocity;
        public float totalFluidMass;
        public Vector3 comOffsetWorld;
        public float fluidMassSmoothing;
        public Vector3 worldOrigin;
        public float twistDegrees;
        public Vector4 worldRotation;
        public Vector3 linearVelocity;
        public float gravity;
        public Vector3 angularVelocity;
        public float damping;
        public Vector3 angularAcceleration;
        public float sloshFeedbackScale;
        public float bucketMass;
        public float applySlosh;
        public Vector3 linearAcceleration;
        public float useGpuState;
        public float padEnd0;
        public float padEnd1;

        public const int Stride = 176;
        public const string BufferName = "_BucketState";

        public static V4GpuBucketState FromScenePose(
            Vector3 pivotWorld,
            Vector3 bucketWorldPosition,
            Quaternion bucketWorldRotation,
            Vector3 unitDirection,
            float ropeLength,
            Vector3 tangentialVelocity,
            float gravity,
            float damping,
            float bucketMass,
            float sloshFeedbackScale,
            float fluidMassSmoothing,
            float twistDegrees,
            bool applySlosh,
            float hangEarLocalY)
        {
            Vector3 u = unitDirection.sqrMagnitude > 1e-8f ? unitDirection.normalized : Vector3.down;
            Vector3 omega = V4SphericalPendulumMath.ComputeAngularVelocity(u, tangentialVelocity, ropeLength);
            Vector3 floorOrigin = V4SphericalPendulumMath.FloorOriginFromHang(
                pivotWorld,
                ropeLength,
                u,
                twistDegrees,
                hangEarLocalY);

            return new V4GpuBucketState
            {
                pivot = pivotWorld,
                ropeLength = ropeLength,
                unitDirection = u,
                tangentialVelocity = tangentialVelocity,
                totalFluidMass = 0f,
                comOffsetWorld = Vector3.zero,
                fluidMassSmoothing = fluidMassSmoothing,
                worldOrigin = floorOrigin,
                twistDegrees = twistDegrees,
                worldRotation = new Vector4(
                    bucketWorldRotation.x,
                    bucketWorldRotation.y,
                    bucketWorldRotation.z,
                    bucketWorldRotation.w),
                linearVelocity = tangentialVelocity,
                gravity = gravity,
                angularVelocity = omega,
                damping = damping,
                angularAcceleration = Vector3.zero,
                sloshFeedbackScale = sloshFeedbackScale,
                bucketMass = bucketMass,
                applySlosh = applySlosh ? 1f : 0f,
                linearAcceleration = Vector3.zero,
                useGpuState = 1f,
                padEnd0 = hangEarLocalY,
                padEnd1 = 0f
            };
        }

        public static V4GpuBucketState FromPendulumParams(
            Vector3 pivot,
            float ropeLength,
            Vector3 unitDirection,
            Vector3 tangentialVelocity,
            float gravity,
            float damping,
            float bucketMass,
            float sloshFeedbackScale,
            float fluidMassSmoothing,
            float twistDegrees,
            bool applySlosh,
            float hangEarLocalY)
        {
            Vector3 u = unitDirection.sqrMagnitude > 1e-8f ? unitDirection.normalized : Vector3.down;
            Quaternion rot = V4SphericalPendulumMath.ComputeBucketRotation(u, twistDegrees);
            Vector3 origin = V4SphericalPendulumMath.FloorOriginFromHang(
                pivot,
                ropeLength,
                u,
                twistDegrees,
                hangEarLocalY);
            Vector3 omega = V4SphericalPendulumMath.ComputeAngularVelocity(u, tangentialVelocity, ropeLength);

            return new V4GpuBucketState
            {
                pivot = pivot,
                ropeLength = ropeLength,
                unitDirection = u,
                tangentialVelocity = tangentialVelocity,
                totalFluidMass = 0f,
                comOffsetWorld = Vector3.zero,
                fluidMassSmoothing = fluidMassSmoothing,
                worldOrigin = origin,
                twistDegrees = twistDegrees,
                worldRotation = new Vector4(rot.x, rot.y, rot.z, rot.w),
                linearVelocity = tangentialVelocity,
                gravity = gravity,
                angularVelocity = omega,
                damping = damping,
                angularAcceleration = Vector3.zero,
                sloshFeedbackScale = sloshFeedbackScale,
                bucketMass = bucketMass,
                applySlosh = applySlosh ? 1f : 0f,
                linearAcceleration = Vector3.zero,
                useGpuState = 1f,
                padEnd0 = hangEarLocalY,
                padEnd1 = 0f
            };
        }

        public static void ApplySceneTransformPose(
            ref V4GpuBucketState state,
            Transform bucketTransform,
            Vector3 pivotWorld,
            float hangEarLocalY)
        {
            Vector3 position = bucketTransform.position;
            Quaternion rotation = bucketTransform.rotation;
            Vector3 hangPoint = V4SphericalPendulumMath.HangPointFromFloor(position, rotation, hangEarLocalY);
            Vector3 delta = hangPoint - pivotWorld;
            if (delta.sqrMagnitude > 1e-8f)
            {
                state.unitDirection = delta.normalized;
                state.ropeLength = delta.magnitude;
            }

            state.pivot = pivotWorld;
            state.worldOrigin = position;
            state.worldRotation = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);
            state.padEnd0 = hangEarLocalY;
            state.padEnd1 = 0f;
        }
    }
}
