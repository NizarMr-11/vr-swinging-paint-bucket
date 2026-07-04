using HarmonicEngineV4.Bake;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Fixed horizontal paint canvas (spec section 1). Baked once into a paint grid of
    /// (color, depth) cells; cell size = particle radius (half the particle width).
    /// The plane is at this transform's Y, centered on its XZ position (rotation ignored).
    /// </summary>
    public sealed class V4Canvas : MonoBehaviour
    {
        [Min(0.05f)] public float width = 2f;
        [Min(0.05f)] public float depth = 2f;

        [Tooltip("Base canvas color before any paint lands.")]
        public Color baseColor = Color.white;

        public float PlaneY => transform.position.y;

        public Vector2 MinCorner => new Vector2(
            transform.position.x - width * 0.5f,
            transform.position.z - depth * 0.5f);

        public Vector2Int GridSize(float particleRadius)
        {
            return V4CanvasGridMath.GridSize(width, depth, particleRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                new Vector3(transform.position.x, PlaneY, transform.position.z),
                new Vector3(width, 0.001f, depth));
        }
    }
}
