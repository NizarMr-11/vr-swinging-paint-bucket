using Unity.Mathematics;

namespace HarmonicEngine.Domain.Solvers
{
    public static class LocalSpaceProcessor
    {
        public static float3 ApplyPseudoForce(float3 acceleration, float3 pseudoForce)
        {
            return acceleration + pseudoForce;
        }

        /// <summary>
        /// Non-inertial bucket frame: centrifugal + Euler + Coriolis (architecture §1 sloshing).
        /// </summary>
        public static float3 ComputeNonInertialAcceleration(
            float3 localPosition,
            float3 localVelocity,
            float3 angularVelocity,
            float3 angularAcceleration)
        {
            float3 centrifugal = -math.cross(angularVelocity, math.cross(angularVelocity, localPosition));
            float3 euler = -math.cross(angularAcceleration, localPosition);
            float3 coriolis = -2f * math.cross(angularVelocity, localVelocity);
            return centrifugal + euler + coriolis;
        }

        public static float3 AngularVelocityFromScalarZ(float omegaRadiansPerSecond)
        {
            return new float3(0f, 0f, omegaRadiansPerSecond);
        }

        public static float3 AngularAccelerationFromScalarZ(float alphaRadiansPerSecondSquared)
        {
            return new float3(0f, 0f, alphaRadiansPerSecondSquared);
        }
    }
}
