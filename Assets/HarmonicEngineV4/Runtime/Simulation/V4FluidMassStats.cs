using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>Aggregated in-bucket fluid properties for pendulum slosh feedback.</summary>
    public struct V4FluidMassStats
    {
        public float totalMass;
        public Vector3 centerOfMassWorld;
        public Vector3 momentumWorld;
        public int particleCount;

        public static V4FluidMassStats Empty => new V4FluidMassStats
        {
            totalMass = 0f,
            centerOfMassWorld = Vector3.zero,
            momentumWorld = Vector3.zero,
            particleCount = 0
        };
    }
}
