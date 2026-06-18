using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Domain.Adapters
{
    public static class FluidParticleFactory
    {
        public const uint WhiteRGBA = 0xFFFFFFFFu;

        /// <summary>Packs a Unity Color into RGBA8 (matching the GPU UnpackUintToFloat3).</summary>
        public static uint PackColor(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return r | (g << 8) | (b << 16) | (a << 24);
        }

        /// <summary>Unpacks RGBA8 written by <see cref="PackColor"/> / GPU <c>UnpackUintToFloat3</c>.</summary>
        public static Color UnpackColor(uint packed)
        {
            float r = (packed & 0xFFu) / 255f;
            float g = ((packed >> 8) & 0xFFu) / 255f;
            float b = ((packed >> 16) & 0xFFu) / 255f;
            float a = ((packed >> 24) & 0xFFu) / 255f;
            return new Color(r, g, b, a);
        }

        public static FluidParticle FromWorldSpawn(
            Vector3 worldPosition,
            Vector3 worldVelocity,
            Transform bucketTransform,
            float restDensity,
            float mass,
            uint packedColor = WhiteRGBA)
        {
            Matrix4x4 worldToLocal = bucketTransform.worldToLocalMatrix;
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPosition);
            Vector3 localVel = worldToLocal.MultiplyVector(worldVelocity);

            return new FluidParticle
            {
                Position = localPos,
                Velocity = localVel,
                Density = restDensity,
                Pressure = 0f,
                PackedColorRGBA = packedColor
            };
        }

        /// <summary>World-space position/velocity stored directly (no bucket transform).</summary>
        public static FluidParticle FromWorldPosition(
            Vector3 worldPosition,
            Vector3 worldVelocity,
            float restDensity,
            uint packedColor = WhiteRGBA)
        {
            return new FluidParticle
            {
                Position = worldPosition,
                Velocity = (float3)worldVelocity,
                Density = restDensity,
                Pressure = 0f,
                PackedColorRGBA = packedColor
            };
        }

        public static FluidParticle FromLocalSpawn(
            float3 localPosition,
            float3 localVelocity,
            float restDensity,
            uint packedColor = WhiteRGBA)
        {
            return new FluidParticle
            {
                Position = localPosition,
                Velocity = localVelocity,
                Density = restDensity,
                Pressure = 0f,
                PackedColorRGBA = packedColor
            };
        }
    }
}
