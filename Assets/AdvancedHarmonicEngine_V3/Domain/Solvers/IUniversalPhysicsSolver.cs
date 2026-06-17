using Unity.Mathematics;

namespace HarmonicEngine.Domain.Solvers
{
    public interface IUniversalPhysicsSolver
    {
        float GasConstantK { get; }
        float RestDensity { get; }
        float Viscosity { get; }
        float ParticleMass { get; }
        float SmoothingRadius(float cellSize);
    }
}
