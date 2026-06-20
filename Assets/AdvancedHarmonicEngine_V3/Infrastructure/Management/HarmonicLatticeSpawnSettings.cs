using System;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Regular 3D lattice fill for deterministic GPU verification (Phase 1 / density tests).
    /// </summary>
    [Serializable]
    public sealed class HarmonicLatticeSpawnSettings
    {
        public Vector3 center = Vector3.zero;
        [Tooltip("World spacing between lattice sites. 0 = use pipeline CellSize.")]
        [Min(0f)] public float spacing;
        public int3 gridDimensions = new int3(10, 10, 10);
        public float restDensity = 1000f;
        public Color spawnColor = Color.cyan;
        public Vector3 initialVelocity = Vector3.zero;

        public int TotalCount =>
            math.max(0, gridDimensions.x) * math.max(0, gridDimensions.y) * math.max(0, gridDimensions.z);
    }
}
