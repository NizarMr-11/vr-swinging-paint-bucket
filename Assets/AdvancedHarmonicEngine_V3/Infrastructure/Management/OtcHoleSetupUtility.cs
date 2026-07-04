using HarmonicEngine.Domain.Models;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>CPU-side hole projection and GPU uniform packing (mirrors OtcFieldParityTests / OtcParticleField.hlsl).</summary>
    public static class OtcHoleSetupUtility
    {
        public const int MaxHoles = 16;
        public const float DefaultClipDepthScale = 1.0f;

        public readonly struct ProjectedHole
        {
            public readonly Vector3 localPosition;
            public readonly float radius;
            public readonly Vector3 axis;
            public readonly OtcHoleSurface surface;

            public ProjectedHole(Vector3 localPosition, float radius, Vector3 axis, OtcHoleSurface surface)
            {
                this.localPosition = localPosition;
                this.radius = radius;
                this.axis = axis;
                this.surface = surface;
            }
        }

        public static bool TryProject(OtcContainerHole authored, float containerRadius, float containerHeight, out ProjectedHole projected)
        {
            projected = default;
            if (authored.radius <= 1e-5f)
            {
                return false;
            }

            float radius = authored.radius;
            if (authored.surface == OtcHoleSurface.Floor)
            {
                Vector3 pos = new Vector3(authored.localPosition.x, 0f, authored.localPosition.z);
                projected = new ProjectedHole(pos, radius, Vector3.up, OtcHoleSurface.Floor);
                return true;
            }

            Vector2 xz = new Vector2(authored.localPosition.x, authored.localPosition.z);
            if (xz.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            Vector2 snapped = xz.normalized * containerRadius;
            Vector3 holePos = new Vector3(
                snapped.x,
                Mathf.Clamp(authored.localPosition.y, 0f, containerHeight),
                snapped.y);
            Vector3 axis = new Vector3(snapped.normalized.x, 0f, snapped.normalized.y);
            projected = new ProjectedHole(holePos, radius, axis, OtcHoleSurface.Side);
            return true;
        }

        public static int PackProjectedHoles(
            OtcContainerHole[] authored,
            float containerRadius,
            float containerHeight,
            Vector4[] holesOut,
            Vector4[] axisOut)
        {
            if (authored == null || holesOut == null || axisOut == null)
            {
                return 0;
            }

            int limit = Mathf.Min(authored.Length, MaxHoles, holesOut.Length, axisOut.Length);
            int count = 0;
            for (int i = 0; i < limit; i++)
            {
                if (!TryProject(authored[i], containerRadius, containerHeight, out ProjectedHole p))
                {
                    continue;
                }

                holesOut[count] = new Vector4(p.localPosition.x, p.localPosition.y, p.localPosition.z, p.radius);
                axisOut[count] = new Vector4(p.axis.x, p.axis.y, p.axis.z, (float)p.surface);
                count++;
            }

            for (int i = count; i < holesOut.Length; i++)
            {
                holesOut[i] = Vector4.zero;
                if (i < axisOut.Length)
                {
                    axisOut[i] = Vector4.zero;
                }
            }

            return count;
        }
    }
}
