using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Profiles;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Legacy spherical spawn zone. Prefer <see cref="V4Bucket.heightLayers"/> for cylinder slab spawns.
    /// </summary>
    public sealed class V4SpawnZone : MonoBehaviour
    {
        [Min(0.01f)] public float radius = 0.2f;
        public Color color = Color.red;

        [Tooltip("Liquid profile for particles spawned by this zone. Null = pipeline global fallback.")]
        public V4LiquidProfile profile;

        public float Volume => V4SpawnMath.SphereVolume(radius);
    }
}
