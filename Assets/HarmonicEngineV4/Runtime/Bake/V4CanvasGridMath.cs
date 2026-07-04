using UnityEngine;

namespace HarmonicEngineV4.Bake
{
    /// <summary>
    /// Canvas grid bake math (spec section 2.2): cell size = particle width / 2,
    /// grid sized (width / cellSize) x (height / cellSize).
    /// </summary>
    public static class V4CanvasGridMath
    {
        /// <summary>Cell size is half the particle width, i.e. equal to the particle radius.</summary>
        public static float CellSize(float particleRadius)
        {
            return Mathf.Max(particleRadius, 1e-5f);
        }

        public static Vector2Int GridSize(float canvasWidth, float canvasHeight, float particleRadius)
        {
            float cell = CellSize(particleRadius);
            return new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(canvasWidth / cell)),
                Mathf.Max(1, Mathf.CeilToInt(canvasHeight / cell)));
        }

        public static int CellCount(float canvasWidth, float canvasHeight, float particleRadius)
        {
            Vector2Int size = GridSize(canvasWidth, canvasHeight, particleRadius);
            return size.x * size.y;
        }

        /// <summary>Canvas-local (u,v) in meters from the canvas min corner to integer cell coordinates.</summary>
        public static Vector2Int CellOf(Vector2 canvasLocal, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(canvasLocal.x / cellSize),
                Mathf.FloorToInt(canvasLocal.y / cellSize));
        }
    }
}
