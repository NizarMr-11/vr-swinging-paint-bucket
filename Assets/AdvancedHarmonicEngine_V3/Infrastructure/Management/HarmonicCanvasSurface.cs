using System;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// The surface falling particles hit. NOTE: the engine treats the canvas as a single
    /// horizontal plane at <see cref="planeY"/> (not arbitrary mesh collision); the
    /// center/size are only used for documentation and to drive the CanvasController quad's
    /// UV mapping. When <see cref="cullIntoCanvas"/> is true a particle that crosses the
    /// plane is recorded as a paint hit; when <see cref="paintAbsorbEnabled"/> is also true
    /// the particle rests on the plane and drains wetness over time before removal.
    /// </summary>
    [Serializable]
    public class HarmonicCanvasSurface
    {
        public float planeY = -2f;
        public bool cullIntoCanvas = true;
        public bool paintAbsorbEnabled = true;
        [Min(0.01f)] public float absorbRate = 1.5f;
        [Min(0f)] public float absorbPaintWeightScale = 1f;
        public Vector3 center = Vector3.zero;
        public Vector2 size = new(10f, 10f);
    }
}
