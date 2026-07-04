using System.Runtime.InteropServices;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Direct GPU tests of the canvas splat kernel (plan Phase 5): synthetic settle
    /// events dispatched against a small grid, compared cell-for-cell with the CPU
    /// reference (V4CanvasSplatMath), plus the saturating depth cap.
    /// </summary>
    public sealed class V4CanvasGpuTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SplatEvent
        {
            public Vector2 Uv;
            public float Radius;
            public float Contribution;
            public uint Color;
            public uint Pad0;
            public uint Pad1;
            public uint Pad2;
        }

        private sealed class CanvasHarness : System.IDisposable
        {
            public readonly ComputeShader Shader;
            public readonly ComputeBuffer Grid;
            public readonly ComputeBuffer Events;
            public readonly ComputeBuffer Counters;
            public readonly Vector2Int GridSize;
            public readonly float CellSize;
            public readonly float MaxDepth;

            public CanvasHarness(Vector2Int gridSize, float cellSize, float maxDepth, int maxEvents = 64)
            {
                GridSize = gridSize;
                CellSize = cellSize;
                MaxDepth = maxDepth;
                Shader = V4ShaderLibrary.Load(V4ShaderLibrary.CanvasSplat);
                Grid = new ComputeBuffer(gridSize.x * gridSize.y, 16);
                Events = new ComputeBuffer(maxEvents, 32);
                Counters = new ComputeBuffer(V4Counters.SlotCount, sizeof(uint));

                Shader.SetInts("_CanvasGridSize", gridSize.x, gridSize.y);
                Shader.SetFloat("_CanvasCellSize", cellSize);
                Shader.SetFloat("_CanvasMaxDepthDefault", maxDepth);
                Shader.SetVector("_CanvasBaseColor", Color.white);
                Shader.SetInt("_MaxSplatEvents", maxEvents);

                int clearKernel = Shader.FindKernel("ClearCanvasKernel");
                Shader.SetBuffer(clearKernel, "_CanvasGrid", Grid);
                Shader.Dispatch(clearKernel, Mathf.CeilToInt(gridSize.x * gridSize.y / 64f), 1, 1);
            }

            public void ApplyEvents(SplatEvent[] events)
            {
                // Production writes the source particle index into Pad0 as a unique,
                // stable ordering key; mirror that here.
                for (int i = 0; i < events.Length; i++)
                {
                    events[i].Pad0 = (uint)i;
                }

                Events.SetData(events);
                var counters = new uint[V4Counters.SlotCount];
                counters[V4Counters.SplatEventCount] = (uint)events.Length;
                Counters.SetData(counters);

                int kernel = Shader.FindKernel("SplatApplyKernel");
                Shader.SetBuffer(kernel, "_CanvasGrid", Grid);
                Shader.SetBuffer(kernel, "_SplatEvents", Events);
                Shader.SetBuffer(kernel, "_Counters", Counters);
                Shader.Dispatch(kernel, 1, 1, 1);
            }

            public Vector4[] ReadGrid()
            {
                var data = new Vector4[GridSize.x * GridSize.y];
                Grid.GetData(data);
                return data;
            }

            public void Dispose()
            {
                Grid.Release();
                Events.Release();
                Counters.Release();
            }
        }

        [Test]
        public void SingleSplat_MatchesCpuReference_CellForCell()
        {
            var gridSize = new Vector2Int(16, 16);
            const float cellSize = 0.05f;
            const float maxDepth = 4f;
            using var harness = new CanvasHarness(gridSize, cellSize, maxDepth);

            var uv = new Vector2(0.41f, 0.37f);
            const float radius = 0.06f;
            const float contribution = 1f;
            var particleColor = new Vector3(1f, 0f, 0f);

            harness.ApplyEvents(new[]
            {
                new SplatEvent { Uv = uv, Radius = radius, Contribution = contribution, Color = 0xFF0000FFu }
            });

            // CPU reference over the same grid.
            var cpuColor = new Vector3[gridSize.x * gridSize.y];
            var cpuDepth = new float[gridSize.x * gridSize.y];
            for (int i = 0; i < cpuColor.Length; i++)
            {
                cpuColor[i] = Vector3.one;
            }

            foreach (V4CanvasSplatMath.CellWeight cw in V4CanvasSplatMath.ComputeCellWeights(uv, radius, cellSize, gridSize))
            {
                int idx = cw.Y * gridSize.x + cw.X;
                V4CanvasSplatMath.BlendCell(ref cpuColor[idx], ref cpuDepth[idx], particleColor, contribution * cw.Weight, maxDepth);
            }

            Vector4[] gpu = harness.ReadGrid();
            int paintedCells = 0;
            for (int i = 0; i < gpu.Length; i++)
            {
                Assert.AreEqual(cpuDepth[i], gpu[i].w, 1e-4f, $"depth mismatch at cell {i}");
                Assert.Less(Vector3.Distance(cpuColor[i], new Vector3(gpu[i].x, gpu[i].y, gpu[i].z)), 1e-3f,
                    $"color mismatch at cell {i}");
                if (gpu[i].w > 0f)
                {
                    paintedCells++;
                }
            }

            Assert.GreaterOrEqual(paintedCells, 4, "a radius splat must cover ~4+ cells (spec section 7)");
        }

        [Test]
        public void RepeatedSplats_DepthSaturatesAtMaxDepth()
        {
            var gridSize = new Vector2Int(8, 8);
            const float cellSize = 0.05f;
            const float maxDepth = 2f;
            using var harness = new CanvasHarness(gridSize, cellSize, maxDepth);

            var evt = new SplatEvent
            {
                Uv = new Vector2(0.2f, 0.2f),
                Radius = 0.04f,
                Contribution = 1f,
                Color = 0xFF00FF00u
            };

            for (int i = 0; i < 20; i++)
            {
                harness.ApplyEvents(new[] { evt });
            }

            Vector4[] grid = harness.ReadGrid();
            bool sawSaturated = false;
            foreach (Vector4 cell in grid)
            {
                Assert.LessOrEqual(cell.w, maxDepth + 1e-4f, "cell depth exceeded the saturating cap");
                if (cell.w >= maxDepth - 1e-4f)
                {
                    sawSaturated = true;
                }
            }

            Assert.IsTrue(sawSaturated, "20 repeated splats should saturate at least one cell");
        }

        [Test]
        public void LaterPaint_DominatesColor_AsDepthAccumulates()
        {
            var gridSize = new Vector2Int(8, 8);
            const float cellSize = 0.05f;
            using var harness = new CanvasHarness(gridSize, cellSize, maxDepth: 10f);

            var uv = new Vector2(0.2f, 0.2f);
            var red = new SplatEvent { Uv = uv, Radius = 0.04f, Contribution = 1f, Color = 0xFF0000FFu };   // red
            var blue = new SplatEvent { Uv = uv, Radius = 0.04f, Contribution = 1f, Color = 0xFFFF0000u };  // blue

            harness.ApplyEvents(new[] { red });
            for (int i = 0; i < 8; i++)
            {
                harness.ApplyEvents(new[] { blue });
            }

            Vector4[] grid = harness.ReadGrid();
            int centerIdx = V4CanvasSplatMathCellIndex(uv, cellSize, gridSize);
            Vector4 center = grid[centerIdx];
            Assert.Greater(center.z, center.x, "after many blue layers over one red, blue must dominate");
        }

        private static int V4CanvasSplatMathCellIndex(Vector2 uv, float cellSize, Vector2Int gridSize)
        {
            Vector2Int cell = HarmonicEngineV4.Bake.V4CanvasGridMath.CellOf(uv, cellSize);
            return cell.y * gridSize.x + cell.x;
        }

        [Test]
        public void OffCanvasEvent_PaintsNothing_AndDoesNotCrash()
        {
            var gridSize = new Vector2Int(8, 8);
            using var harness = new CanvasHarness(gridSize, 0.05f, 4f);

            harness.ApplyEvents(new[]
            {
                new SplatEvent { Uv = new Vector2(-5f, -5f), Radius = 0.04f, Contribution = 1f, Color = 0xFF0000FFu }
            });

            foreach (Vector4 cell in harness.ReadGrid())
            {
                Assert.AreEqual(0f, cell.w, "off-canvas splat must not paint any cell");
            }
        }
    }
}
