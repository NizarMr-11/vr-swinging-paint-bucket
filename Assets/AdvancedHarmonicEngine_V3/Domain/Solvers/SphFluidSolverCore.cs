using System;

namespace HarmonicEngine.Domain.Solvers
{
    [Serializable]
    public sealed class SphFluidSolverCore : IUniversalPhysicsSolver
    {
        public float GasConstantK { get; set; } = 2000f;
        public float RestDensity { get; set; } = 1000f;
        public float Viscosity { get; set; } = 0.05f;
        public float ParticleMass { get; set; } = 0.02f;
        public float SpeedOfSound { get; set; } = 12f;
        public float SmoothingRadiusMultiplier { get; set; } = 1f;

        public float SmoothingRadius(float cellSize) => cellSize * SmoothingRadiusMultiplier;

        public float StiffnessB => SpeedOfSound * SpeedOfSound * RestDensity / 7f;
    }
}
