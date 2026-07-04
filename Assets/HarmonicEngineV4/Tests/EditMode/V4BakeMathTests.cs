using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4BakeMathTests
    {
        // --- Canvas grid (spec section 2.2) ---

        [Test]
        public void CanvasGrid_CellSizeIsParticleRadius()
        {
            Assert.AreEqual(0.025f, V4CanvasGridMath.CellSize(0.025f), 1e-6f);
        }

        [Test]
        public void CanvasGrid_SizeMatchesSpecFormula()
        {
            // width / (particleWidth / 2) = 2.0 / 0.025 = 80 cells
            Vector2Int size = V4CanvasGridMath.GridSize(2f, 1f, 0.025f);
            Assert.AreEqual(80, size.x);
            Assert.AreEqual(40, size.y);
        }

        [Test]
        public void CanvasGrid_NonDivisibleSizes_RoundUp()
        {
            Vector2Int size = V4CanvasGridMath.GridSize(1.01f, 0.99f, 0.1f);
            Assert.AreEqual(11, size.x);
            Assert.AreEqual(10, size.y);
        }

        [Test]
        public void CanvasGrid_MinimumOneCell()
        {
            Vector2Int size = V4CanvasGridMath.GridSize(0.001f, 0.001f, 0.1f);
            Assert.AreEqual(1, size.x);
            Assert.AreEqual(1, size.y);
        }

        [Test]
        public void CanvasGrid_CellOf_MapsLocalPositionToCell()
        {
            Assert.AreEqual(new Vector2Int(0, 0), V4CanvasGridMath.CellOf(new Vector2(0.01f, 0.01f), 0.1f));
            Assert.AreEqual(new Vector2Int(3, 7), V4CanvasGridMath.CellOf(new Vector2(0.35f, 0.75f), 0.1f));
        }

        // --- Spawn math (spec sections 2.3, 3) ---

        [Test]
        public void SpawnMath_SpacingFromDensity_InvertsCorrectly()
        {
            float density = 8000f; // 8000 per m3 => spacing = 0.05
            Assert.AreEqual(0.05f, V4SpawnMath.SpacingFromDensity(density), 1e-4f);
            Assert.AreEqual(0.025f, V4SpawnMath.ParticleRadiusFromDensity(density), 1e-4f);
        }

        [Test]
        public void SpawnMath_ExpectedCount_IsVolumeTimesDensity()
        {
            Assert.AreEqual(500, V4SpawnMath.ExpectedCount(0.5f, 1000f));
        }

        [Test]
        public void SpawnMath_LatticeFill_AllPointsInsideSphere()
        {
            Vector3 center = new Vector3(1f, 2f, 3f);
            const float radius = 0.3f;
            List<Vector3> points = V4SpawnMath.LatticeFillSphere(center, radius, 30000f);

            Assert.Greater(points.Count, 0);
            foreach (Vector3 p in points)
            {
                Assert.LessOrEqual(Vector3.Distance(p, center), radius + 1e-5f);
            }
        }

        [Test]
        public void SpawnMath_LatticeFill_CountApproximatesVolumeTimesDensity()
        {
            const float radius = 0.5f;
            const float density = 50000f;
            List<Vector3> points = V4SpawnMath.LatticeFillSphere(Vector3.zero, radius, density);

            int expected = V4SpawnMath.ExpectedCount(V4SpawnMath.SphereVolume(radius), density);
            Assert.Greater(points.Count, expected * 0.7f);
            Assert.Less(points.Count, expected * 1.3f);
        }

        [Test]
        public void SpawnMath_LatticeFill_IsDeterministic()
        {
            List<Vector3> a = V4SpawnMath.LatticeFillSphere(Vector3.one, 0.25f, 20000f);
            List<Vector3> b = V4SpawnMath.LatticeFillSphere(Vector3.one, 0.25f, 20000f);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i], b[i]);
            }
        }
    }
}
