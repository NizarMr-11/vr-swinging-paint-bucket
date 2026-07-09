using System.Collections.Generic;
using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Bake
{
    /// <summary>Authoring definition of one hole, before bake.</summary>
    [System.Serializable]
    public struct V4HoleDef
    {
        [Tooltip("Hole opening center in bucket-local space (origin = floor center, +Y up).")]
        public Vector3 localPosition;

        [Tooltip("Physical radius of the hole opening.")]
        public float radius;

        [Tooltip("Outward surface normal at the hole (unit, bucket-local). Eject impulses go this way.")]
        public Vector3 outwardNormal;
    }

    /// <summary>One horizontal slab stacked from the bucket floor upward.</summary>
    [System.Serializable]
    public struct V4LayerDef
    {
        [Min(0.001f)]
        [Tooltip("Layer thickness in meters (stacked bottom to top).")]
        public float thickness;

        [Tooltip("Spawn / debug color for liquid in this slab.")]
        public Color color;

        [Tooltip("Optional zone force scale for this layer; <= 0 uses the liquid profile curve.")]
        public float forceStrength;
    }

    /// <summary>
    /// Bucket geometry bake: holes (cylindrical eject footprints) and stacked height layers.
    /// </summary>
    public static class V4BucketBake
    {
        public sealed class Result
        {
            public V4BakedHole[] Holes;
            public V4BakedLayer[] Layers;
            public bool SpacingOk;
            public readonly List<string> Errors = new List<string>();
        }

        public static V4BakedLayer[] BakeLayers(IReadOnlyList<V4LayerDef> layerDefs, float bucketHeight, float legacyTopBandHeight)
        {
            if (bucketHeight <= 1e-6f)
            {
                return new[]
                {
                    new V4BakedLayer { yMin = 0f, yMax = 1f }
                };
            }

            if (layerDefs == null || layerDefs.Count == 0)
            {
                return BuildDefaultLayers(bucketHeight, legacyTopBandHeight);
            }

            var layers = new V4BakedLayer[layerDefs.Count];
            float y = 0f;
            for (int i = 0; i < layerDefs.Count; i++)
            {
                float thickness = Mathf.Max(layerDefs[i].thickness, 0.001f);
                float yMax = i == layerDefs.Count - 1
                    ? bucketHeight
                    : Mathf.Min(y + thickness, bucketHeight);
                layers[i] = new V4BakedLayer
                {
                    yMin = y,
                    yMax = yMax,
                    forceStrength = layerDefs[i].forceStrength
                };
                y = yMax;
            }

            if (y < bucketHeight - 1e-5f)
            {
                layers[layers.Length - 1].yMax = bucketHeight;
            }

            return layers;
        }

        /// <summary>
        /// Total fluid fill height from authored layer thicknesses (spawn volume), not zone extension to bucket top.
        /// </summary>
        public static float ComputeAuthoredFillHeight(IReadOnlyList<V4LayerDef> layerDefs, float bucketHeight)
        {
            if (bucketHeight <= 1e-6f)
            {
                return 0f;
            }

            if (layerDefs == null || layerDefs.Count == 0)
            {
                return bucketHeight;
            }

            float fillHeight = 0f;
            for (int i = 0; i < layerDefs.Count; i++)
            {
                fillHeight += Mathf.Max(layerDefs[i].thickness, 0.001f);
            }

            return Mathf.Min(fillHeight, bucketHeight);
        }

        private static V4BakedLayer[] BuildDefaultLayers(float bucketHeight, float legacyTopBandHeight)
        {
            float topHeight = Mathf.Clamp(legacyTopBandHeight, 0.001f, bucketHeight);
            if (topHeight >= bucketHeight - 1e-5f)
            {
                return new[]
                {
                    new V4BakedLayer { yMin = 0f, yMax = bucketHeight }
                };
            }

            return new[]
            {
                new V4BakedLayer { yMin = 0f, yMax = bucketHeight - topHeight },
                new V4BakedLayer { yMin = bucketHeight - topHeight, yMax = bucketHeight }
            };
        }

        /// <summary>
        /// Bakes hole footprints and validates that physical openings do not overlap in XZ.
        /// </summary>
        public static Result BakeBucket(
            IReadOnlyList<V4HoleDef> holeDefs,
            IReadOnlyList<V4LayerDef> layerDefs,
            float bucketHeight,
            float legacyTopBandHeight)
        {
            var result = new Result
            {
                Holes = BakeHoles(holeDefs),
                Layers = BakeLayers(layerDefs, bucketHeight, legacyTopBandHeight),
                SpacingOk = true
            };

            for (int i = 0; i < result.Holes.Length; i++)
            {
                for (int j = i + 1; j < result.Holes.Length; j++)
                {
                    float centerDist = HorizontalDistance(
                        result.Holes[i].localPosition,
                        result.Holes[j].localPosition);
                    float required = result.Holes[i].radius + result.Holes[j].radius;
                    if (centerDist < required - 1e-5f)
                    {
                        result.SpacingOk = false;
                        result.Errors.Add(
                            $"holes {i} and {j} overlap in XZ: center distance {centerDist:F4} < combined radius {required:F4}");
                    }
                }
            }

            return result;
        }

        public static V4BakedHole[] BakeHoles(IReadOnlyList<V4HoleDef> holeDefs)
        {
            var holes = new V4BakedHole[holeDefs.Count];
            for (int i = 0; i < holeDefs.Count; i++)
            {
                V4HoleDef def = holeDefs[i];
                holes[i] = new V4BakedHole
                {
                    localPosition = def.localPosition,
                    radius = def.radius,
                    outwardNormal = def.outwardNormal.sqrMagnitude > 1e-8f ? def.outwardNormal.normalized : Vector3.up
                };
            }

            return holes;
        }

        /// <summary>Legacy entry: holes only, no layers.</summary>
        public static Result BakeHoles(IReadOnlyList<V4HoleDef> holeDefs, float ringSpacing, bool autoShrinkRings)
        {
            return BakeBucket(holeDefs, null, 1f, 0.15f);
        }

        /// <summary>Legacy ring helper kept for tests migrating off spherical zones.</summary>
        public static void ComputeRings(float holeRadius, float ringSpacing, out float d0, out float d1, out float d2)
        {
            d0 = Mathf.Max(holeRadius, ringSpacing * 0.25f);
            d1 = d0 + ringSpacing;
            d2 = d1 + ringSpacing;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
