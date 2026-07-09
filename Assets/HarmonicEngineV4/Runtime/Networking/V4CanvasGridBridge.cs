using System;
using System.Collections.Generic;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Simulation;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>Converts between GPU <c>_CanvasGrid</c> buffers and networking <see cref="CanvasGrid"/> DTOs.</summary>
    public static class V4CanvasGridBridge
    {
        public const float PaintedDepthEpsilon = 1e-5f;

        public static CanvasCell[] CapturePaintedCells(V4PipelineRoot pipeline, Vector4[] gridScratch = null)
        {
            if (pipeline == null || !pipeline.Initialized || pipeline.canvas == null)
            {
                return Array.Empty<CanvasCell>();
            }

            int resX = pipeline.CanvasGridSize.x;
            int resY = pipeline.CanvasGridSize.y;
            int total = resX * resY;
            if (total <= 0)
            {
                return Array.Empty<CanvasCell>();
            }

            if (gridScratch == null || gridScratch.Length < total)
            {
                gridScratch = new Vector4[total];
            }

            pipeline.ReadCanvasGridSnapshot(gridScratch);
            float cellSize = pipeline.CanvasCellSize;
            V4Canvas canvas = pipeline.canvas;
            float3 origin = new float3(canvas.MinCorner.x, canvas.PlaneY, canvas.MinCorner.y);
            var painted = new List<CanvasCell>();

            for (int i = 0; i < total; i++)
            {
                Vector4 cell = gridScratch[i];
                if (cell.w <= PaintedDepthEpsilon)
                {
                    continue;
                }

                int x = i % resX;
                int y = i / resX;
                float u = (x + 0.5f) * cellSize;
                float v = (y + 0.5f) * cellSize;
                painted.Add(new CanvasCell
                {
                    cellIndex = i,
                    worldPosition = new float3(origin.x + u, origin.y, origin.z + v),
                    splatRadius = cellSize * 0.5f,
                    packedColor = PackRgb(cell),
                    opacity = cell.w
                });
            }

            return painted.ToArray();
        }

        public static void ApplyPaintedCells(V4PipelineRoot pipeline, CanvasGrid canvasGrid)
        {
            if (pipeline == null || !pipeline.Initialized)
            {
                return;
            }

            if (canvasGrid == null)
            {
                pipeline.ResetCanvasGridForNetwork();
                return;
            }

            int resX = pipeline.CanvasGridSize.x;
            int resY = pipeline.CanvasGridSize.y;
            if (canvasGrid.resolutionX != resX || canvasGrid.resolutionY != resY)
            {
                throw new InvalidOperationException(
                    $"Canvas resolution mismatch: network {canvasGrid.resolutionX}x{canvasGrid.resolutionY}, local {resX}x{resY}");
            }

            int total = resX * resY;
            var grid = new Vector4[total];
            Color baseColor = pipeline.canvas != null ? pipeline.canvas.baseColor : Color.white;
            var empty = new Vector4(baseColor.r, baseColor.g, baseColor.b, 0f);
            for (int i = 0; i < total; i++)
            {
                grid[i] = empty;
            }

            CanvasCell[] cells = canvasGrid.cells;
            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    CanvasCell cell = cells[i];
                    if (cell.cellIndex < 0 || cell.cellIndex >= total)
                    {
                        continue;
                    }

                    float3 rgb = UnpackColor(cell.packedColor);
                    grid[cell.cellIndex] = new Vector4(rgb.x, rgb.y, rgb.z, cell.opacity);
                }
            }

            pipeline.ApplyNetworkCanvasSnapshot(grid);
        }

        public static CanvasGrid CloneCanvasGrid(CanvasGrid source)
        {
            if (source == null)
            {
                return null;
            }

            CanvasCell[] cells = source.cells;
            CanvasCell[] cloneCells = null;
            if (cells != null)
            {
                cloneCells = new CanvasCell[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    CanvasCell cell = cells[i];
                    cloneCells[i] = new CanvasCell
                    {
                        cellIndex = cell.cellIndex,
                        worldPosition = cell.worldPosition,
                        splatRadius = cell.splatRadius,
                        packedColor = cell.packedColor,
                        opacity = cell.opacity
                    };
                }
            }

            return new CanvasGrid
            {
                resolutionX = source.resolutionX,
                resolutionY = source.resolutionY,
                cellSize = source.cellSize,
                origin = source.origin,
                cells = cloneCells
            };
        }

        public static uint PackRgb(Vector4 cell)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(cell.x * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(cell.y * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(cell.z * 255f), 0, 255);
            return r | (g << 8) | (b << 16) | (0xFFu << 24);
        }

        public static float3 UnpackColor(uint packed)
        {
            float r = (packed & 0xFFu) / 255f;
            float g = ((packed >> 8) & 0xFFu) / 255f;
            float b = ((packed >> 16) & 0xFFu) / 255f;
            return new float3(r, g, b);
        }
    }
}
