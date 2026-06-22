using HarmonicEngine.Domain.Solvers;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class SolverTests
    {
        [Test]
        public void SphFluidSolverCore_SmoothingRadius_ScalesWithCellSize()
        {
            var solver = new SphFluidSolverCore { SmoothingRadiusMultiplier = 1f };
            Assert.AreEqual(0.25f, solver.SmoothingRadius(0.25f), 1e-5f);
        }

        [Test]
        public void LocalSpaceProcessor_AddsPseudoForceToAcceleration()
        {
            float3 acceleration = new(0, -9.81f, 0);
            float3 pseudo = new(1, 0, 0);
            float3 result = LocalSpaceProcessor.ApplyPseudoForce(acceleration, pseudo);
            Assert.AreEqual(new float3(1, -9.81f, 0), result);
        }

        [Test]
        public void WorldSpaceProcessor_IntegratesGravity()
        {
            float3 velocity = float3.zero;
            float3 gravity = new(0, -9.81f, 0);
            float3 result = WorldSpaceProcessor.IntegrateVelocity(velocity, gravity, 0.02f);
            Assert.AreEqual(-0.1962f, result.y, 1e-3f);
            Assert.AreEqual(0f, result.x, 1e-3f);
            Assert.AreEqual(0f, result.z, 1e-3f);
        }
    }
}
