using System;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// The volume particles interact with. The world-space container is an open-top cylinder
    /// (analytic - not mesh collision); the nozzle fields drive the local-space bucket SPH exit
    /// SDF used by the swinging-bucket simulation. Pass whichever your scene uses.
    /// </summary>
    [Serializable]
    public class HarmonicBucketVolume
    {
        [Header("Open-top cylinder (world-space container)")]
        public Vector3 center = Vector3.zero;
        [Min(0.01f)] public float radius = 0.55f;
        public float floorY = 0f;
        public float rimY = 1.1f;
        [Range(0f, 1f)] public float restitution = 0.1f;
        [Range(0f, 1f)] public float friction = 0.85f;
        [Min(0f)] public float wallStiffness = 400f;

        [Header("Local-space nozzle SDF (swinging bucket)")]
        public float nozzlePlaneLocalY = -0.35f;
        [Min(0f)] public float nozzleRadius = 0.05f;
        public float bucketRimLocalY = 0.35f;
    }
}
