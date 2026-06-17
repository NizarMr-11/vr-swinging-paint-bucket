using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Domain.Adapters
{
    public static class FluidParticleFactory
    {
        public static FluidParticle FromWorldSpawn(
            Vector3 worldPosition,
            Vector3 worldVelocity,
            Transform bucketTransform,
            float restDensity,
            float mass)
        {
            Matrix4x4 worldToLocal = bucketTransform.worldToLocalMatrix;
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPosition);
            Vector3 localVel = worldToLocal.MultiplyVector(worldVelocity);

            return new FluidParticle
            {
                Position = localPos,
                Velocity = localVel,
                Density = restDensity,
                Pressure = 0f
            };
        }

        /// <summary>World-space position/velocity stored directly (no bucket transform).</summary>
        public static FluidParticle FromWorldPosition(
            Vector3 worldPosition,
            Vector3 worldVelocity,
            float restDensity)
        {
            return new FluidParticle
            {
                Position = worldPosition,
                Velocity = (float3)worldVelocity,
                Density = restDensity,
                Pressure = 0f
            };
        }

        public static FluidParticle FromLocalSpawn(float3 localPosition, float3 localVelocity, float restDensity)
        {
            return new FluidParticle
            {
                Position = localPosition,
                Velocity = localVelocity,
                Density = restDensity,
                Pressure = 0f
            };
        }
    }
}
