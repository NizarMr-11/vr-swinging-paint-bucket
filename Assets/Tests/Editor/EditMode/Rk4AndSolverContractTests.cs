using HarmonicEngine.Core.Mathematics.Integrators;
using HarmonicEngine.Domain.Solvers;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class Rk4AndSolverContractTests
    {
        [Test]
        public void Rk4SystemSolver_IntegratesSimpleHarmonicOscillator()
        {
            float state = 1f;
            float result = Rk4SystemSolver.IntegrateScalar(
                state,
                0f,
                0.01f,
                (s, _) => -s);

            Assert.Less(result, state);
            Assert.Greater(result, 0.98f);
        }

        [Test]
        public void LocalSpaceProcessor_ComputesNonZeroCentrifugalTerm()
        {
            float3 pseudo = LocalSpaceProcessor.ComputeNonInertialAcceleration(
                new float3(1f, 0f, 0f),
                float3.zero,
                new float3(0f, 0f, 2f),
                float3.zero);

            Assert.AreNotEqual(float3.zero, pseudo);
        }

        [Test]
        public void SphFluidSolverCore_ImplementsUniversalPhysicsSolver()
        {
            IUniversalPhysicsSolver solver = new SphFluidSolverCore
            {
                GasConstantK = 100f,
                RestDensity = 900f,
                Viscosity = 0.1f,
                ParticleMass = 0.01f
            };

            Assert.AreEqual(100f, solver.GasConstantK);
            Assert.AreEqual(900f, solver.RestDensity);
        }

        [Test]
        public void WorldSpaceProcessor_AppliesDrag()
        {
            float3 velocity = new float3(0f, -5f, 0f);
            float3 dragged = WorldSpaceProcessor.ApplyDrag(velocity, dragCoefficient: 0.5f, dt: 0.1f);
            Assert.Less(math.length(dragged), math.length(velocity));
        }
    }
}
