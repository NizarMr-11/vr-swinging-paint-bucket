using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Profiles;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Spherical spawn zone (spec sections 1-3). Particle count derives from zone volume
    /// x global density; particles receive this zone's color and liquid profile
    /// (falling back to the pipeline root's global profile when none is set).
    /// </summary>
    public sealed class V4SpawnZone : MonoBehaviour
    {
        [Min(0.01f)] public float radius = 0.2f;
        public Color color = Color.red;

        [Tooltip("Liquid profile for particles spawned by this zone. Null = pipeline global fallback.")]
        public V4LiquidProfile profile;

        public float Volume => V4SpawnMath.SphereVolume(radius);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
