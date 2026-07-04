using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// CPU mirror of the spatial-hash math in V4Common.hlsl. The GPU/CPU parity tests
    /// assert both sides produce identical hashes, so grid build and neighbor query
    /// provably agree with the CPU brute-force reference.
    /// </summary>
    public static class V4SpatialHashMath
    {
        public static Vector3Int CellFromPosition(Vector3 position, float cellSize)
        {
            float safe = Mathf.Max(cellSize, 1e-4f);
            return new Vector3Int(
                Mathf.FloorToInt(position.x / safe),
                Mathf.FloorToInt(position.y / safe),
                Mathf.FloorToInt(position.z / safe));
        }

        public static uint HashCell(Vector3Int cell, uint gridResolution)
        {
            unchecked
            {
                uint x = (uint)(cell.x * 73856093);
                uint y = (uint)(cell.y * 19349663);
                uint z = (uint)(cell.z * 83492791);
                return (x ^ y ^ z) & (gridResolution - 1u);
            }
        }

        public static uint HashPosition(Vector3 position, float cellSize, uint gridResolution)
        {
            return HashCell(CellFromPosition(position, cellSize), gridResolution);
        }
    }
}
