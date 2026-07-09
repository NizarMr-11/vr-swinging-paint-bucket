using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4ZoneMathTests
    {
        private const float BucketHeight = 1f;

        private static V4BakedHole MakeHole(Vector3 pos, float radius = 0.05f)
        {
            return new V4BakedHole
            {
                localPosition = pos,
                radius = radius,
                outwardNormal = Vector3.down
            };
        }

        private static V4BakedLayer[] ThreeLayers()
        {
            return new[]
            {
                new V4BakedLayer { yMin = 0f, yMax = 0.33f },
                new V4BakedLayer { yMin = 0.33f, yMax = 0.66f },
                new V4BakedLayer { yMin = 0.66f, yMax = 1f }
            };
        }

        [Test]
        public void FindLayerIndex_AssignsStackedSlabs()
        {
            V4BakedLayer[] layers = ThreeLayers();
            Assert.AreEqual(0, V4ZoneMath.FindLayerIndex(0.1f, layers, layers.Length));
            Assert.AreEqual(1, V4ZoneMath.FindLayerIndex(0.5f, layers, layers.Length));
            Assert.AreEqual(2, V4ZoneMath.FindLayerIndex(0.95f, layers, layers.Length));
        }

        [Test]
        public void HoleEject_RequiresXZFootprintInHoleLayer()
        {
            var holes = new[] { MakeHole(new Vector3(0.1f, 0.05f, 0f), 0.04f) };
            V4BakedLayer[] layers = new[] { new V4BakedLayer { yMin = 0f, yMax = 0.4f } };

            V4ZoneMath.ZoneResult insideFootprint = V4ZoneMath.Classify(
                new Vector3(0.1f, 0.08f, 0f), true, holes, 1, layers, 1);
            Assert.AreEqual(V4Zone.HoleZone0, insideFootprint.Zone);

            V4ZoneMath.ZoneResult outsideFootprint = V4ZoneMath.Classify(
                new Vector3(0.3f, 0.08f, 0f), true, holes, 1, layers, 1);
            Assert.AreEqual(V4Zone.HeightLayer, outsideFootprint.Zone);
            Assert.AreEqual(0, outsideFootprint.LayerIndex);
        }

        [Test]
        public void HoleEject_WinsOverHeightLayer()
        {
            var holes = new[] { MakeHole(new Vector3(0.1f, 0.9f, 0f), 0.04f) };
            V4BakedLayer[] layers = ThreeLayers();
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                new Vector3(0.1f, 0.92f, 0f), true, holes, 1, layers, layers.Length);
            Assert.AreEqual(V4Zone.HoleZone0, result.Zone);
        }

        [Test]
        public void ParticleInTopLayer_GetsHeightLayerIndex()
        {
            V4BakedLayer[] layers = ThreeLayers();
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                new Vector3(-0.2f, 0.9f, 0f), true, new V4BakedHole[0], 0, layers, layers.Length);
            Assert.AreEqual(V4Zone.HeightLayer, result.Zone);
            Assert.AreEqual(2, result.LayerIndex);
        }

        [Test]
        public void OutsideParticle_GetsNoZone_EvenNearHole()
        {
            var holes = new[] { MakeHole(new Vector3(0.1f, 0.05f, 0f)) };
            V4BakedLayer[] layers = new[] { new V4BakedLayer { yMin = 0f, yMax = 1f } };
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                new Vector3(0.1f, 0.05f, 0f), false, holes, 1, layers, 1);
            Assert.AreEqual(V4Zone.None, result.Zone);
        }

        [Test]
        public void Zone0ForceDirection_IsOutwardNormal()
        {
            V4BakedHole hole = MakeHole(new Vector3(0.1f, 0.05f, 0f));
            hole.outwardNormal = Vector3.right;
            Vector3 dir = V4ZoneMath.ForceDirection(new Vector3(0.09f, 0.05f, 0f), hole, V4Zone.HoleZone0);
            Assert.AreEqual(Vector3.right, dir);
        }

        [Test]
        public void TopBandPushDown_MatchesSpecFormula()
        {
            Assert.AreEqual(2f, V4ZoneMath.TopBandPushDown(10f, 5, 1f), 1e-5f);
            Assert.AreEqual(1f, V4ZoneMath.TopBandPushDown(10f, 5, 0.5f), 1e-5f);
        }

        [Test]
        public void BakeLayers_StacksUserThicknessesToRim()
        {
            var defs = new[]
            {
                new V4LayerDef { thickness = 0.2f },
                new V4LayerDef { thickness = 0.2f },
                new V4LayerDef { thickness = 0.2f }
            };
            V4BakedLayer[] layers = V4BucketBake.BakeLayers(defs, 0.6f, 0.1f);
            Assert.AreEqual(3, layers.Length);
            Assert.AreEqual(0f, layers[0].yMin, 1e-5f);
            Assert.AreEqual(0.6f, layers[2].yMax, 1e-5f);
        }
    }
}
