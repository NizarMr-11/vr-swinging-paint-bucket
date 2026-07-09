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
    /// Pure CPU reference for the zone system. Priority: hole eject footprint, then height layer.
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

            public int LayerIndex => HoleIndex;

            public static readonly ZoneResult None = new ZoneResult(V4Zone.None, 0);
        }

        public static int FindLayerIndex(float y, V4BakedLayer[] layers, int layerCount)
        {
            if (layers == null || layerCount <= 0)
            {
                return -1;
            }

            for (int i = 0; i < layerCount; i++)
            {
                bool isLast = i == layerCount - 1;
                if (y >= layers[i].yMin && (isLast ? y <= layers[i].yMax : y < layers[i].yMax))
                {
                    return i;
                }
            }

            return -1;
        }

        public static bool IsInHoleEjectFootprint(
            Vector3 localPos,
            in V4BakedHole hole,
            in V4BakedLayer layer,
            float particleRadius = 0f)
        {
            if (hole.radius <= 1e-6f)
            {
                return false;
            }

            float dx = localPos.x - hole.localPosition.x;
            float dz = localPos.z - hole.localPosition.z;
            float effectiveRadius = hole.radius + particleRadius;
            if (dx * dx + dz * dz > effectiveRadius * effectiveRadius)
            {
                return false;
            }

            return localPos.y >= layer.yMin && localPos.y <= layer.yMax;
        }

        public static ZoneResult Classify(
            Vector3 localPos,
            bool inside,
            V4BakedHole[] holes,
            int holeCount,
            V4BakedLayer[] layers,
            int layerCount,
            float particleRadius = 0f)
        {
            if (!inside)
            {
                return ZoneResult.None;
            }

            for (int i = 0; i < holeCount; i++)
            {
                if (holes[i].radius <= 1e-6f)
                {
                    continue;
                }

                int holeLayer = FindLayerIndex(holes[i].localPosition.y, layers, layerCount);
                if (holeLayer < 0)
                {
                    continue;
                }

                if (IsInHoleEjectFootprint(localPos, holes[i], layers[holeLayer], particleRadius))
                {
                    return new ZoneResult(V4Zone.HoleZone0, i);
                }
            }

            int layerIndex = FindLayerIndex(localPos.y, layers, layerCount);
            if (layerIndex >= 0)
            {
                return new ZoneResult(V4Zone.HeightLayer, layerIndex);
            }

            return ZoneResult.None;
        }

        /// <summary>Legacy overload used while migrating callers.</summary>
        public static ZoneResult Classify(
            Vector3 localPos,
            bool inside,
            V4BakedHole[] holes,
            int holeCount,
            float bucketHeight,
            float topBandHeight)
        {
            V4BakedLayer[] layers = V4BucketBakeShim.LegacyLayers(bucketHeight, topBandHeight);
            return Classify(localPos, inside, holes, holeCount, layers, layers.Length);
        }

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

        public static float TopBandPushDown(float totalExpectedLoss, int topBandCount, float downwardScale)
        {
            return totalExpectedLoss / Mathf.Max(topBandCount, 1) * downwardScale;
        }
    }

    internal static class V4BucketBakeShim
    {
        public static V4BakedLayer[] LegacyLayers(float bucketHeight, float topBandHeight)
        {
            return HarmonicEngineV4.Bake.V4BucketBake.BakeLayers(null, bucketHeight, topBandHeight);
        }
    }
}
