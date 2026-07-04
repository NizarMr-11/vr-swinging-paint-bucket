using System.Collections.Generic;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// Pure CPU reference for canvas painting (spec section 7). A settled particle
    /// splats across the cells its radius overlaps, weighted by distance from the cell
    /// center; each cell blends incrementally with a saturating paint depth.
    /// HLSL mirror: SplatApplyKernel in V4CanvasSplat.compute.
    /// </summary>
    public static class V4CanvasSplatMath
    {
        public readonly struct CellWeight
        {
            public readonly int X;
            public readonly int Y;
            public readonly float Weight;

            public CellWeight(int x, int y, float weight)
            {
                X = x;
                Y = y;
                Weight = weight;
            }
        }

        /// <summary>
        /// Cells overlapped by a particle footprint at canvas-local (u,v) with the given
        /// radius. Weights fall off linearly with distance from the cell center and are
        /// normalized to sum to 1, so the full contribution always lands on the canvas.
        /// </summary>
        public static List<CellWeight> ComputeCellWeights(
            Vector2 uv,
            float radius,
            float cellSize,
            Vector2Int gridSize)
        {
            var raw = new List<CellWeight>();
            int minX = Mathf.FloorToInt((uv.x - radius) / cellSize);
            int maxX = Mathf.FloorToInt((uv.x + radius) / cellSize);
            int minY = Mathf.FloorToInt((uv.y - radius) / cellSize);
            int maxY = Mathf.FloorToInt((uv.y + radius) / cellSize);

            float totalWeight = 0f;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (x < 0 || y < 0 || x >= gridSize.x || y >= gridSize.y)
                    {
                        continue;
                    }

                    var cellCenter = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
                    float dist = Vector2.Distance(cellCenter, uv);
                    float weight = Mathf.Max(0f, 1f - dist / Mathf.Max(radius, 1e-6f));
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    raw.Add(new CellWeight(x, y, weight));
                    totalWeight += weight;
                }
            }

            if (totalWeight <= 0f)
            {
                // Degenerate footprint (radius smaller than distance to any cell center):
                // put everything into the cell containing the point, if on-canvas.
                Vector2Int cell = Bake.V4CanvasGridMath.CellOf(uv, cellSize);
                if (cell.x >= 0 && cell.y >= 0 && cell.x < gridSize.x && cell.y < gridSize.y)
                {
                    raw.Add(new CellWeight(cell.x, cell.y, 1f));
                }

                return raw;
            }

            var normalized = new List<CellWeight>(raw.Count);
            foreach (CellWeight cw in raw)
            {
                normalized.Add(new CellWeight(cw.X, cw.Y, cw.Weight / totalWeight));
            }

            return normalized;
        }

        /// <summary>
        /// Incremental weighted blend with saturating depth (spec section 7 stacking rule):
        ///   depth' = min(depth + contribution, maxDepth)
        ///   alpha  = contribution / depth'
        ///   color' = lerp(color, particleColor, alpha)
        /// </summary>
        public static void BlendCell(
            ref Vector3 cellColor,
            ref float cellDepth,
            Vector3 particleColor,
            float contribution,
            float maxDepth)
        {
            if (contribution <= 0f)
            {
                return;
            }

            float newDepth = Mathf.Min(cellDepth + contribution, maxDepth);
            float alpha = contribution / Mathf.Max(newDepth, 1e-6f);
            cellColor = Vector3.Lerp(cellColor, particleColor, Mathf.Clamp01(alpha));
            cellDepth = newDepth;
        }
    }
}
