using Unity.Mathematics;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// CPU mirror of <c>SphCommon.hlsl</c> spatial-hash math for Phase 2 verification tests.
    /// </summary>
    public static class SphHashCpuMirror
    {
        public static int3 CellFromPosition(float3 position, float cellSize)
        {
            float safeCell = math.max(cellSize, 1e-4f);
            return new int3(
                (int)math.floor(position.x / safeCell),
                (int)math.floor(position.y / safeCell),
                (int)math.floor(position.z / safeCell));
        }

        public static uint HashCell(int3 cell, int gridResolution)
        {
            unchecked
            {
                uint x = (uint)cell.x * 73856093u;
                uint y = (uint)cell.y * 19349663u;
                uint z = (uint)cell.z * 83492791u;
                return (x ^ y ^ z) & (uint)(gridResolution - 1);
            }
        }

        public static uint HashPosition(float3 position, float cellSize, int gridResolution)
        {
            return HashCell(CellFromPosition(position, cellSize), gridResolution);
        }
    }
}
