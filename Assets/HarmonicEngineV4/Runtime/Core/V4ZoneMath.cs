using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>Baked per-hole data. Mirrors the GPU hole buffer layout (V4HoleGpu, 48 bytes).</summary>
    [System.Serializable]
    public struct V4BakedHole
    {
        public Vector3 localPosition;
        public float radius;
        public Vector3 outwardNormal;
        public float d0;
        public float d1;
        public float d2;
        public float pad0;
        public float pad1;
    }

    /// <summary>
    /// Pure CPU reference for the zone system (spec section 5, plan Phase 3 Step 1).
    /// Priority resolution is a hard rule: Level 1 (hole zones) always wins over
    /// Level 2 (top band); a particle receives at most one zone per frame.
    /// The HLSL mirror lives in V4BucketZones.hlsl.
    /// </summary>
    public static class V4ZoneMath
    {
        public readonly struct ZoneResult
        {
            public readonly V4Zone Zone;
            public readonly int HoleIndex;

            public ZoneResult(V4Zone zone, int holeIndex)
            {
                Zone = zone;
                HoleIndex = holeIndex;
            }

            public static readonly ZoneResult None = new ZoneResult(V4Zone.None, 0);
        }

        /// <summary>Zone ring classification against one hole. Distance in bucket-local space to the hole opening.</summary>
        public static V4Zone ClassifyAgainstHole(Vector3 localPos, in V4BakedHole hole)
        {
            float dist = Vector3.Distance(localPos, hole.localPosition);
            if (dist <= hole.d0)
            {
                return V4Zone.HoleZone0;
            }

            if (dist <= hole.d1)
            {
                return V4Zone.HoleZone1;
            }

            if (dist <= hole.d2)
            {
                return V4Zone.HoleZone2;
            }

            return V4Zone.None;
        }

        /// <summary>
        /// Full priority-based classification (spec section 5 resolution rule):
        /// Level 1 hole zones first - if any hole claims the particle, that is final.
        /// Only unclaimed particles are then tested against the Level 2 top band.
        /// Non-inside particles receive no zone (they are outside or escaped).
        /// </summary>
        public static ZoneResult Classify(
            Vector3 localPos,
            bool inside,
            V4BakedHole[] holes,
            int holeCount,
            float bucketHeight,
            float topBandHeight)
        {
            if (!inside)
            {
                return ZoneResult.None;
            }

            // Level 1: hole zones. The bake-time min-spacing constraint guarantees at
            // most one hole's d2 ring contains the particle; take the nearest for
            // robustness against exactly-on-boundary float noise.
            int bestHole = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < holeCount; i++)
            {
                float dist = Vector3.Distance(localPos, holes[i].localPosition);
                if (dist <= holes[i].d2 && dist < bestDist)
                {
                    bestDist = dist;
                    bestHole = i;
                }
            }

            if (bestHole >= 0)
            {
                return new ZoneResult(ClassifyAgainstHole(localPos, holes[bestHole]), bestHole);
            }

            // Level 2: top band, only for particles no hole claimed this frame.
            if (localPos.y >= bucketHeight - topBandHeight)
            {
                return new ZoneResult(V4Zone.TopBand, 0);
            }

            return ZoneResult.None;
        }

        /// <summary>
        /// Zone force direction (spec section 5): Zones 1/2 pull toward the hole opening;
        /// Zone 0 ejects along the hole's outward normal.
        /// </summary>
        public static Vector3 ForceDirection(Vector3 localPos, in V4BakedHole hole, V4Zone zone)
        {
            if (zone == V4Zone.HoleZone0)
            {
                return hole.outwardNormal;
            }

            Vector3 toHole = hole.localPosition - localPos;
            float len = toHole.magnitude;
            return len > 1e-6f ? toHole / len : hole.outwardNormal;
        }

        /// <summary>Top-band push-down magnitude (spec section 5 loss approximation).</summary>
        public static float TopBandPushDown(float totalExpectedLoss, int topBandCount, float downwardScale)
        {
            return totalExpectedLoss / Mathf.Max(topBandCount, 1) * downwardScale;
        }
    }
}
