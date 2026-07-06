using System.Collections.Generic;
using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Bake
{
    /// <summary>Authoring definition of one hole, before ring thresholds are baked.</summary>
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

    /// <summary>
    /// Bucket geometry bake (spec section 2.1): holes plus their zone ring thresholds
    /// d0 &lt; d1 &lt; d2, with the bake-time minimum-spacing constraint so no particle is
    /// ever ambiguous about which hole's zone it belongs to.
    /// </summary>
    public static class V4BucketBake
    {
        public sealed class Result
        {
            public V4BakedHole[] Holes;
            public bool SpacingOk;
            public bool RingsShrunk;
            public readonly List<string> Errors = new List<string>();
        }

        /// <summary>
        /// d0 = hole radius, then two equal ring steps outward. d0 is clamped to a
        /// fraction of the ring spacing so a zero/near-zero authored radius still gives
        /// the eject zone a real volume - a point-sized Zone 0 can never claim a
        /// particle, which jams the outflow into burst clumps instead of a stream.
        /// </summary>
        public static void ComputeRings(float holeRadius, float ringSpacing, out float d0, out float d1, out float d2)
        {
            d0 = Mathf.Max(holeRadius, ringSpacing * 0.25f);
            d1 = d0 + ringSpacing;
            d2 = d1 + ringSpacing;
        }

        /// <summary>
        /// Bakes holes and validates spacing: two holes' outermost rings (d2) must not
        /// overlap. With autoShrinkRings the rings are shrunk to fit (never below the
        /// physical hole radius); otherwise overlap is a bake error.
        /// </summary>
        public static Result BakeHoles(IReadOnlyList<V4HoleDef> holeDefs, float ringSpacing, bool autoShrinkRings)
        {
            var result = new Result
            {
                Holes = new V4BakedHole[holeDefs.Count],
                SpacingOk = true
            };

            for (int i = 0; i < holeDefs.Count; i++)
            {
                V4HoleDef def = holeDefs[i];
                ComputeRings(def.radius, ringSpacing, out float d0, out float d1, out float d2);
                result.Holes[i] = new V4BakedHole
                {
                    localPosition = def.localPosition,
                    radius = def.radius,
                    outwardNormal = def.outwardNormal.sqrMagnitude > 1e-8f ? def.outwardNormal.normalized : Vector3.up,
                    d0 = d0,
                    d1 = d1,
                    d2 = d2
                };
            }

            for (int i = 0; i < result.Holes.Length; i++)
            {
                for (int j = i + 1; j < result.Holes.Length; j++)
                {
                    float centerDist = Vector3.Distance(result.Holes[i].localPosition, result.Holes[j].localPosition);
                    float required = result.Holes[i].d2 + result.Holes[j].d2;
                    if (centerDist >= required)
                    {
                        continue;
                    }

                    if (!autoShrinkRings)
                    {
                        result.SpacingOk = false;
                        result.Errors.Add(
                            $"holes {i} and {j} are too close: center distance {centerDist:F4} < combined d2 rings {required:F4}");
                        continue;
                    }

                    // Shrink both holes' rings proportionally so d2_i + d2_j == centerDist,
                    // clamped so rings never collapse below the physical hole radius.
                    float scale = centerDist / required;
                    ShrinkRings(ref result.Holes[i], scale, ringSpacing);
                    ShrinkRings(ref result.Holes[j], scale, ringSpacing);
                    result.RingsShrunk = true;

                    if (result.Holes[i].d2 + result.Holes[j].d2 > centerDist + 1e-5f)
                    {
                        // Even fully shrunk rings (d2 == hole radius) overlap: the physical
                        // holes themselves are too close - unrecoverable authoring error.
                        result.SpacingOk = false;
                        result.Errors.Add(
                            $"holes {i} and {j} physically overlap even with fully shrunk rings (center distance {centerDist:F4})");
                    }
                }
            }

            return result;
        }

        private static void ShrinkRings(ref V4BakedHole hole, float scale, float ringSpacing)
        {
            // d0 stays at the physical hole radius; d1/d2 scale down toward it,
            // never collapsing below it.
            hole.d2 = Mathf.Max(hole.radius, hole.d2 * scale);
            hole.d1 = Mathf.Max(hole.radius, Mathf.Min(hole.d1 * scale, (hole.d0 + hole.d2) * 0.5f));
        }
    }
}
