using System.Collections.Generic;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4CanvasSplatMathTests
    {
        private static readonly Vector2Int Grid = new Vector2Int(20, 20);
        private const float CellSize = 0.05f;

        [Test]
        public void Weights_SumToOne()
        {
            List<V4CanvasSplatMath.CellWeight> weights =
                V4CanvasSplatMath.ComputeCellWeights(new Vector2(0.5f, 0.5f), 0.06f, CellSize, Grid);

            float sum = 0f;
            foreach (V4CanvasSplatMath.CellWeight cw in weights)
            {
                sum += cw.Weight;
            }

            Assert.AreEqual(1f, sum, 1e-4f);
        }

        [Test]
        public void Footprint_CoversMultipleCells()
        {
            List<V4CanvasSplatMath.CellWeight> weights =
                V4CanvasSplatMath.ComputeCellWeights(new Vector2(0.5f, 0.5f), 0.06f, CellSize, Grid);
            Assert.GreaterOrEqual(weights.Count, 4, "spec section 7: a splat covers the cells the radius overlaps (~4+)");
        }

        [Test]
        public void CellsOutsideGrid_AreExcluded()
        {
            List<V4CanvasSplatMath.CellWeight> weights =
                V4CanvasSplatMath.ComputeCellWeights(new Vector2(0.01f, 0.01f), 0.08f, CellSize, Grid);

            foreach (V4CanvasSplatMath.CellWeight cw in weights)
            {
                Assert.GreaterOrEqual(cw.X, 0);
                Assert.GreaterOrEqual(cw.Y, 0);
                Assert.Less(cw.X, Grid.x);
                Assert.Less(cw.Y, Grid.y);
            }

            // Clipped footprints still deliver the full contribution on-canvas.
            float sum = 0f;
            foreach (V4CanvasSplatMath.CellWeight cw in weights)
            {
                sum += cw.Weight;
            }

            Assert.AreEqual(1f, sum, 1e-4f);
        }

        [Test]
        public void DegenerateTinyRadius_FallsBackToContainingCell()
        {
            List<V4CanvasSplatMath.CellWeight> weights =
                V4CanvasSplatMath.ComputeCellWeights(new Vector2(0.5f, 0.5f), 1e-5f, CellSize, Grid);

            Assert.AreEqual(1, weights.Count);
            Assert.AreEqual(10, weights[0].X);
            Assert.AreEqual(10, weights[0].Y);
            Assert.AreEqual(1f, weights[0].Weight, 1e-5f);
        }

        [Test]
        public void FullyOffCanvas_ProducesNoCells()
        {
            List<V4CanvasSplatMath.CellWeight> weights =
                V4CanvasSplatMath.ComputeCellWeights(new Vector2(-1f, -1f), 0.05f, CellSize, Grid);
            Assert.AreEqual(0, weights.Count);
        }

        [Test]
        public void BlendCell_DepthSaturates()
        {
            Vector3 color = Vector3.one;
            float depth = 0f;
            for (int i = 0; i < 30; i++)
            {
                V4CanvasSplatMath.BlendCell(ref color, ref depth, new Vector3(1f, 0f, 0f), 0.5f, 4f);
            }

            Assert.AreEqual(4f, depth, 1e-4f);
        }

        [Test]
        public void BlendCell_FirstFullContribution_TakesParticleColor()
        {
            Vector3 color = Vector3.one;
            float depth = 0f;
            V4CanvasSplatMath.BlendCell(ref color, ref depth, new Vector3(0f, 1f, 0f), 1f, 4f);

            // alpha = contribution / newDepth = 1/1 = 1 => full particle color.
            Assert.Less(Vector3.Distance(color, new Vector3(0f, 1f, 0f)), 1e-4f);
            Assert.AreEqual(1f, depth, 1e-5f);
        }

        [Test]
        public void BlendCell_ZeroContribution_IsNoOp()
        {
            Vector3 color = new Vector3(0.3f, 0.4f, 0.5f);
            float depth = 1.5f;
            V4CanvasSplatMath.BlendCell(ref color, ref depth, Vector3.one, 0f, 4f);
            Assert.AreEqual(new Vector3(0.3f, 0.4f, 0.5f), color);
            Assert.AreEqual(1.5f, depth);
        }

        [Test]
        public void BlendCell_AccumulatingLayers_ShiftTowardNewColor()
        {
            Vector3 color = new Vector3(1f, 0f, 0f);
            float depth = 1f;
            for (int i = 0; i < 10; i++)
            {
                V4CanvasSplatMath.BlendCell(ref color, ref depth, new Vector3(0f, 0f, 1f), 1f, 10f);
            }

            Assert.Greater(color.z, color.x, "repeated blue layers must dominate the original red");
        }
    }
}
