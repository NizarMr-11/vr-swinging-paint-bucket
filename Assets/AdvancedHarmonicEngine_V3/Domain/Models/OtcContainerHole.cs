using System;
using UnityEngine;

namespace HarmonicEngine.Domain.Models
{
    public enum OtcHoleSurface
    {
        Floor = 0,
        Side = 1,
    }

    /// <summary>Authoring-time hole definition in container-local space (project-at-setup for side holes).</summary>
    [Serializable]
    public struct OtcContainerHole
    {
        public Vector3 localPosition;
        public float radius;
        public OtcHoleSurface surface;
    }
}
