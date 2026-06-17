using HarmonicEngine.Core.Mathematics.Integrators;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class PendulumRk4IntegratorTests
    {
        [Test]
        public void RungeKutta4_HasLowerEnergyDriftThanEuler_AfterSimulatedMinute()
        {
            const float ropeLength = 5f;
            const float gravity = 9.81f;
            const float damping = 0.02f;
            const float dt = 0.02f;
            const int steps = 3000;

            float eulerTheta = 0.8f;
            float eulerOmega = 0f;
            float rkTheta = 0.8f;
            float rkOmega = 0f;

            float initialEnergy = PendulumRk4Integrator.ComputeMechanicalEnergy(eulerTheta, eulerOmega, ropeLength, gravity);

            for (int i = 0; i < steps; i++)
            {
                float alpha = PendulumRk4Integrator.ComputeAngularAcceleration(
                    eulerTheta, eulerOmega, ropeLength, gravity, damping);
                eulerOmega += alpha * dt;
                eulerTheta += eulerOmega * dt;

                PendulumRk4Integrator.Step(
                    ref rkTheta,
                    ref rkOmega,
                    out _,
                    ropeLength,
                    gravity,
                    damping,
                    dt);
            }

            float eulerEnergyDrift = math.abs(
                PendulumRk4Integrator.ComputeMechanicalEnergy(eulerTheta, eulerOmega, ropeLength, gravity) - initialEnergy);
            float rkEnergyDrift = math.abs(
                PendulumRk4Integrator.ComputeMechanicalEnergy(rkTheta, rkOmega, ropeLength, gravity) - initialEnergy);

            Assert.Less(rkEnergyDrift, eulerEnergyDrift);
        }
    }
}
