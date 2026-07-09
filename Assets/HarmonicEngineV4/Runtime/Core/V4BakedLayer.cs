using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>Baked height slab inside the bucket cavity. Mirrors GPU <c>V4Layer</c> layout.</summary>
    [System.Serializable]
    public struct V4BakedLayer
    {
        public float yMin;
        public float yMax;

        /// <summary>Optional per-layer force scale; &lt;= 0 uses the liquid profile curve.</summary>
        public float forceStrength;
        public float pad;
    }
}
