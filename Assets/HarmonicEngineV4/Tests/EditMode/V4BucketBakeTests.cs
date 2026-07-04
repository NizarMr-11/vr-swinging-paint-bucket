using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4BucketBakeTests
    {
        private static V4HoleDef Hole(Vector3 pos, float radius)
        {
            return new V4HoleDef { localPosition = pos, radius = radius, outwardNormal = Vector3.right };
        }

        [Test]
        public void ComputeRings_AreStrictlyIncreasing()
        {
            V4BucketBake.ComputeRings(0.05f, 0.08f, out float d0, out float d1, out float d2);
            Assert.AreEqual(0.05f, d0, 1e-6f);
            Assert.AreEqual(0.13f, d1, 1e-6f);
            Assert.AreEqual(0.21f, d2, 1e-6f);
            Assert.Less(d0, d1);
            Assert.Less(d1, d2);
        }

        [Test]
        public void LegalSpacing_PassesValidation()
        {
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0.5f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(-0.5f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.08f, autoShrinkRings: false);
            Assert.IsTrue(result.SpacingOk);
            Assert.IsFalse(result.RingsShrunk);
            Assert.IsEmpty(result.Errors);
        }

        [Test]
        public void OverlappingRings_WithoutAutoShrink_IsBakeError()
        {
            // d2 = 0.05 + 2*0.08 = 0.21 each; centers 0.3 apart < 0.42 combined.
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(0.3f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.08f, autoShrinkRings: false);
            Assert.IsFalse(result.SpacingOk);
            Assert.IsNotEmpty(result.Errors);
        }

        [Test]
        public void OverlappingRings_WithAutoShrink_ProducesNonOverlappingRings()
        {
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(0.3f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.08f, autoShrinkRings: true);
            Assert.IsTrue(result.SpacingOk, string.Join("; ", result.Errors));
            Assert.IsTrue(result.RingsShrunk);

            float centerDist = Vector3.Distance(result.Holes[0].localPosition, result.Holes[1].localPosition);
            Assert.LessOrEqual(result.Holes[0].d2 + result.Holes[1].d2, centerDist + 1e-5f);

            foreach (V4BakedHole hole in result.Holes)
            {
                Assert.GreaterOrEqual(hole.d1, hole.d0 - 1e-6f, "d1 must not shrink below d0");
                Assert.GreaterOrEqual(hole.d2, hole.d1 - 1e-6f, "d2 must not shrink below d1");
                Assert.GreaterOrEqual(hole.d0, hole.radius - 1e-6f, "d0 must not shrink below the hole radius");
            }
        }

        [Test]
        public void PhysicallyOverlappingHoles_FailEvenWithAutoShrink()
        {
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(0.04f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.08f, autoShrinkRings: true);
            Assert.IsFalse(result.SpacingOk);
            Assert.IsNotEmpty(result.Errors);
        }

        [Test]
        public void ZeroNormal_IsNormalizedToFallback()
        {
            var holes = new List<V4HoleDef>
            {
                new V4HoleDef { localPosition = Vector3.zero, radius = 0.05f, outwardNormal = Vector3.zero }
            };

            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.08f, autoShrinkRings: false);
            Assert.AreEqual(1f, result.Holes[0].outwardNormal.magnitude, 1e-4f);
        }

        [Test]
        public void SingleHole_AlwaysValid()
        {
            var holes = new List<V4HoleDef> { Hole(Vector3.zero, 0.1f) };
            V4BucketBake.Result result = V4BucketBake.BakeHoles(holes, 0.2f, autoShrinkRings: false);
            Assert.IsTrue(result.SpacingOk);
        }
    }
}
