using System;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// World-space open-top cylinder SPH configuration (lab / pooling mode).
    /// </summary>
    [Serializable]
    public sealed class ContainerFluidSettings
    {
        [Tooltip("When true the pipeline runs world-space SPH confined to the cylinder below.")]
        public bool enabled;

        public Vector3 center = Vector3.zero;
        [Min(0.01f)] public float radius = 0.55f;
        public float floorY;
        public float rimY = 1.1f;
        [Range(0f, 1f)] public float restitution = 0.1f;
        [Range(0f, 1f)] public float friction = 0.85f;
        [Min(0f)] public float wallStiffness = 400f;

        [Tooltip("Maximum integration time step for the container SPH pass (CFL stability guard).")]
        [Min(0.001f)] public float maxTimeStep = 0.008f;

        [Header("SPH tuning")]
        [Min(0f)] public float gasConstantK = 380f;
        [Min(0f)] public float viscosity = 2.5f;
        [Range(0f, 0.5f)] public float velocityDamping;
        [Min(1f)] public float maxSpeed = 100f;
        [Tooltip("0 = auto from cell size and rest density (mass = rho0 * spacing^3).")]
        [Min(0f)] public float particleMass;
        [Range(1, 4)] public int substeps = 2;

        public void ApplyBounds(Vector3 newCenter, float newRadius, float newFloorY, float newRimY, float newRestitution, float newFriction, float newWallStiffness)
        {
            center = newCenter;
            radius = Mathf.Max(0.01f, newRadius);
            floorY = newFloorY;
            rimY = newRimY;
            restitution = Mathf.Clamp01(newRestitution);
            friction = Mathf.Clamp01(newFriction);
            wallStiffness = Mathf.Max(0f, newWallStiffness);
        }
    }
}
