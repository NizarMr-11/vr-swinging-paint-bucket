using System;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Classifies how a particle relates to the bucket shell for wall-escape investigation.
    /// </summary>
    public enum V4WallEscapeExitClass
    {
        InsideCavity,
        OverOpenRim,
        BeyondOuterShell,
        OutsideWallBand,
        OutsideFloorOrOther,
        EscapedLatched
    }

    public static class V4WallEscapeForensics
    {
        public readonly struct HoleProximity
        {
            public readonly int NearestHole;
            public readonly float Distance;
            public readonly float D0;
            public readonly float D2;

            public HoleProximity(int nearestHole, float distance, float d0, float d2)
            {
                NearestHole = nearestHole;
                Distance = distance;
                D0 = d0;
                D2 = d2;
            }

            public bool WithinD2 => NearestHole >= 0 && Distance <= D2 + 1e-5f;
            public bool WithinD0 => NearestHole >= 0 && Distance <= D0 + 1e-5f;
        }

        public static HoleProximity NearestHole(Vector3 localPos, V4BakedHole[] holes)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            float d0 = 0f;
            float d2 = 0f;
            if (holes != null)
            {
                for (int i = 0; i < holes.Length; i++)
                {
                    float dist = Vector3.Distance(localPos, holes[i].localPosition);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = i;
                        d0 = holes[i].d0;
                        d2 = holes[i].d2;
                    }
                }
            }

            return new HoleProximity(best, bestDist, d0, d2);
        }

        public static V4WallEscapeExitClass ClassifyLocal(
            Vector3 localPos,
            uint flags,
            float innerRadius,
            float bucketHeight,
            float wallThickness)
        {
            if (V4ParticleFlags.HasEscaped(flags))
            {
                return V4WallEscapeExitClass.EscapedLatched;
            }

            float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
            float outerRadius = innerRadius + wallThickness;

            if (localPos.y > bucketHeight)
            {
                return V4WallEscapeExitClass.OverOpenRim;
            }

            if (r > outerRadius + 1e-5f)
            {
                return V4WallEscapeExitClass.BeyondOuterShell;
            }

            if (r > innerRadius + 1e-5f && localPos.y >= -wallThickness && localPos.y <= bucketHeight)
            {
                return V4WallEscapeExitClass.OutsideWallBand;
            }

            if (V4ParticleFlags.IsInside(flags) && localPos.y >= 0f && r <= innerRadius + 1e-5f)
            {
                return V4WallEscapeExitClass.InsideCavity;
            }

            return V4WallEscapeExitClass.OutsideFloorOrOther;
        }

        public static string ExitClassLabel(V4WallEscapeExitClass exitClass)
        {
            switch (exitClass)
            {
                case V4WallEscapeExitClass.InsideCavity: return "inside";
                case V4WallEscapeExitClass.OverOpenRim: return "overRim";
                case V4WallEscapeExitClass.BeyondOuterShell: return "beyondOuter";
                case V4WallEscapeExitClass.OutsideWallBand: return "wallBand";
                case V4WallEscapeExitClass.OutsideFloorOrOther: return "outsideOther";
                case V4WallEscapeExitClass.EscapedLatched: return "escapedLatched";
                default: return exitClass.ToString();
            }
        }
    }
}
