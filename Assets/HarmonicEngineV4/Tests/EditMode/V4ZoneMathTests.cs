using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    /// <summary>
    /// Layer 1 CPU reference tests for the zone system (spec section 5, plan Phase 3 Step 1),
    /// including the exact ring-boundary edge cases that plagued the V3 implementation.
    /// </summary>
    public sealed class V4ZoneMathTests
    {
        private const float BucketHeight = 1f;
        private const float TopBand = 0.15f;

        private static V4BakedHole MakeHole(Vector3 pos, float d0 = 0.05f, float d1 = 0.13f, float d2 = 0.21f)
        {
            return new V4BakedHole
            {
                localPosition = pos,
                radius = d0,
                outwardNormal = Vector3.right,
                d0 = d0,
                d1 = d1,
                d2 = d2
            };
        }

        // --- Ring classification against a single hole ---

        [Test]
        public void AtHoleCenter_IsZone0()
        {
            V4BakedHole hole = MakeHole(new Vector3(0.5f, 0.2f, 0f));
            Assert.AreEqual(V4Zone.HoleZone0, V4ZoneMath.ClassifyAgainstHole(hole.localPosition, hole));
        }

        [Test]
        public void ExactlyOnD0_IsZone0()
        {
            V4BakedHole hole = MakeHole(Vector3.zero);
            Assert.AreEqual(V4Zone.HoleZone0, V4ZoneMath.ClassifyAgainstHole(new Vector3(hole.d0, 0f, 0f), hole));
        }

        [Test]
        public void JustPastD0_IsZone1()
        {
            V4BakedHole hole = MakeHole(Vector3.zero);
            Assert.AreEqual(V4Zone.HoleZone1, V4ZoneMath.ClassifyAgainstHole(new Vector3(hole.d0 + 1e-4f, 0f, 0f), hole));
        }

        [Test]
        public void ExactlyOnD1_IsZone1()
        {
            V4BakedHole hole = MakeHole(Vector3.zero);
            Assert.AreEqual(V4Zone.HoleZone1, V4ZoneMath.ClassifyAgainstHole(new Vector3(hole.d1, 0f, 0f), hole));
        }

        [Test]
        public void BetweenD1AndD2_IsZone2()
        {
            V4BakedHole hole = MakeHole(Vector3.zero);
            Assert.AreEqual(V4Zone.HoleZone2, V4ZoneMath.ClassifyAgainstHole(new Vector3((hole.d1 + hole.d2) * 0.5f, 0f, 0f), hole));
        }

        [Test]
        public void PastD2_IsNoZone()
        {
            V4BakedHole hole = MakeHole(Vector3.zero);
            Assert.AreEqual(V4Zone.None, V4ZoneMath.ClassifyAgainstHole(new Vector3(hole.d2 + 1e-4f, 0f, 0f), hole));
        }

        // --- Priority resolution (Level 1 beats Level 2, hard rule) ---

        [Test]
        public void ParticleInHoleZoneAndTopBand_GetsHoleZoneOnly()
        {
            // Hole near the rim so its d2 ring overlaps the top band.
            var holes = new[] { MakeHole(new Vector3(0.4f, 0.95f, 0f)) };
            Vector3 pos = new Vector3(0.4f, 0.92f, 0f); // dist 0.03 < d0 => Zone0; also in top band (y >= 0.85)

            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(pos, inside: true, holes, holes.Length, BucketHeight, TopBand);
            Assert.AreEqual(V4Zone.HoleZone0, result.Zone, "Level 1 must always win over Level 2");
        }

        [Test]
        public void ParticleInTopBandOnly_GetsTopBand()
        {
            var holes = new[] { MakeHole(new Vector3(0.4f, 0.2f, 0f)) };
            Vector3 pos = new Vector3(-0.3f, 0.9f, 0f);

            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(pos, inside: true, holes, holes.Length, BucketHeight, TopBand);
            Assert.AreEqual(V4Zone.TopBand, result.Zone);
        }

        [Test]
        public void ExactlyAtTopBandBoundary_IsInTopBand()
        {
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                new Vector3(0f, BucketHeight - TopBand, 0f), true,
                new V4BakedHole[0], 0, BucketHeight, TopBand);
            Assert.AreEqual(V4Zone.TopBand, result.Zone);
        }

        [Test]
        public void JustBelowTopBand_IsNoZone()
        {
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                new Vector3(0f, BucketHeight - TopBand - 1e-4f, 0f), true,
                new V4BakedHole[0], 0, BucketHeight, TopBand);
            Assert.AreEqual(V4Zone.None, result.Zone);
        }

        [Test]
        public void OutsideParticle_GetsNoZone_EvenNearHole()
        {
            var holes = new[] { MakeHole(new Vector3(0.5f, 0.2f, 0f)) };
            V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(
                holes[0].localPosition, inside: false, holes, holes.Length, BucketHeight, TopBand);
            Assert.AreEqual(V4Zone.None, result.Zone);
        }

        // --- Hole ownership uniqueness at minimum legal spacing ---

        [Test]
        public void TwoHolesAtMinimumSpacing_EveryZonedPointClaimedByExactlyNearestHole()
        {
            // Centers exactly d2_a + d2_b apart: rings touch but do not overlap.
            var holeA = MakeHole(new Vector3(-0.21f, 0.3f, 0f));
            var holeB = MakeHole(new Vector3(0.21f, 0.3f, 0f));
            var holes = new[] { holeA, holeB };

            for (int i = 0; i <= 100; i++)
            {
                float x = Mathf.Lerp(-0.45f, 0.45f, i / 100f);
                var pos = new Vector3(x, 0.3f, 0f);
                V4ZoneMath.ZoneResult result = V4ZoneMath.Classify(pos, true, holes, 2, BucketHeight, TopBand);
                if (result.Zone == V4Zone.None || result.Zone == V4Zone.TopBand)
                {
                    continue;
                }

                int nearest = Vector3.Distance(pos, holeA.localPosition) <= Vector3.Distance(pos, holeB.localPosition) ? 0 : 1;
                Assert.AreEqual(nearest, result.HoleIndex, $"ambiguous hole ownership at x={x}");
            }
        }

        // --- Force direction ---

        [Test]
        public void Zone1ForceDirection_PointsTowardHole()
        {
            V4BakedHole hole = MakeHole(new Vector3(0.5f, 0.2f, 0f));
            Vector3 pos = new Vector3(0.4f, 0.2f, 0f);
            Vector3 dir = V4ZoneMath.ForceDirection(pos, hole, V4Zone.HoleZone1);
            Assert.AreEqual(1f, Vector3.Dot(dir, Vector3.right), 1e-4f);
        }

        [Test]
        public void Zone0ForceDirection_IsOutwardNormal()
        {
            V4BakedHole hole = MakeHole(new Vector3(0.5f, 0.2f, 0f));
            Vector3 dir = V4ZoneMath.ForceDirection(new Vector3(0.49f, 0.2f, 0f), hole, V4Zone.HoleZone0);
            Assert.AreEqual(hole.outwardNormal, dir);
        }

        [Test]
        public void ForceDirection_AtExactHoleCenter_FallsBackToNormal()
        {
            V4BakedHole hole = MakeHole(new Vector3(0.5f, 0.2f, 0f));
            Vector3 dir = V4ZoneMath.ForceDirection(hole.localPosition, hole, V4Zone.HoleZone1);
            Assert.AreEqual(hole.outwardNormal, dir, "degenerate zero-length direction must fall back to the outward normal");
        }

        // --- Top-band push-down ---

        [Test]
        public void TopBandPushDown_MatchesSpecFormula()
        {
            Assert.AreEqual(2f, V4ZoneMath.TopBandPushDown(10f, 5, 1f), 1e-5f);
            Assert.AreEqual(1f, V4ZoneMath.TopBandPushDown(10f, 5, 0.5f), 1e-5f);
        }

        [Test]
        public void TopBandPushDown_ZeroCount_DoesNotDivideByZero()
        {
            Assert.AreEqual(10f, V4ZoneMath.TopBandPushDown(10f, 0, 1f), 1e-5f);
        }
    }
}
