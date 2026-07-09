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
        public void LegalHoleSpacing_PassesValidation()
        {
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0.5f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(-0.5f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeBucket(holes, null, 1f, 0.15f);
            Assert.IsTrue(result.SpacingOk);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(2, result.Layers.Length);
        }

        [Test]
        public void OverlappingHoleFootprints_IsBakeError()
        {
            var holes = new List<V4HoleDef>
            {
                Hole(new Vector3(0f, 0.2f, 0f), 0.05f),
                Hole(new Vector3(0.06f, 0.2f, 0f), 0.05f)
            };

            V4BucketBake.Result result = V4BucketBake.BakeBucket(holes, null, 1f, 0.15f);
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

            V4BucketBake.Result result = V4BucketBake.BakeBucket(holes, null, 1f, 0.15f);
            Assert.AreEqual(1f, result.Holes[0].outwardNormal.magnitude, 1e-4f);
        }

        [Test]
        public void SingleHole_AlwaysValid()
        {
            var holes = new List<V4HoleDef> { Hole(Vector3.zero, 0.1f) };
            V4BucketBake.Result result = V4BucketBake.BakeBucket(holes, null, 1f, 0.15f);
            Assert.IsTrue(result.SpacingOk);
        }

        [Test]
        public void ComputeAuthoredFillHeight_SumsLayerThicknesses()
        {
            var defs = new List<V4LayerDef>
            {
                new V4LayerDef { thickness = 0.2f },
                new V4LayerDef { thickness = 0.2f },
                new V4LayerDef { thickness = 0.2f }
            };

            Assert.AreEqual(0.6f, V4BucketBake.ComputeAuthoredFillHeight(defs, 0.6f), 1e-5f);
            Assert.AreEqual(0.2f, V4BucketBake.ComputeAuthoredFillHeight(defs.GetRange(0, 1), 0.6f), 1e-5f);
        }
    }
}
