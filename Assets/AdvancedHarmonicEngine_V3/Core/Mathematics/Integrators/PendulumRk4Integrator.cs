using Unity.Mathematics;

namespace HarmonicEngine.Core.Mathematics.Integrators
{
    /// <summary>
    /// RK4 integrator for pendulum (theta, omega). Scalar-only for Burst-safe inlining from jobs later.
    /// </summary>
    public static class PendulumRk4Integrator
    {
        public static float ComputeAngularAcceleration(
            float theta,
            float omega,
            float ropeLength,
            float gravity,
            float damping) =>
            -(gravity / math.max(ropeLength, 1e-4f)) * math.sin(theta) - damping * omega;

        public static void Step(
            ref float theta,
            ref float omega,
            out float angularAcceleration,
            float ropeLength,
            float gravity,
            float damping,
            float deltaTime)
        {
            float k1Theta = omega;
            float k1Omega = ComputeAngularAcceleration(theta, omega, ropeLength, gravity, damping);

            float t2 = theta + k1Theta * (deltaTime * 0.5f);
            float o2 = omega + k1Omega * (deltaTime * 0.5f);
            float k2Theta = o2;
            float k2Omega = ComputeAngularAcceleration(t2, o2, ropeLength, gravity, damping);

            float t3 = theta + k2Theta * (deltaTime * 0.5f);
            float o3 = omega + k2Omega * (deltaTime * 0.5f);
            float k3Theta = o3;
            float k3Omega = ComputeAngularAcceleration(t3, o3, ropeLength, gravity, damping);

            float t4 = theta + k3Theta * deltaTime;
            float o4 = omega + k3Omega * deltaTime;
            float k4Theta = o4;
            float k4Omega = ComputeAngularAcceleration(t4, o4, ropeLength, gravity, damping);

            theta += (deltaTime / 6f) * (k1Theta + 2f * k2Theta + 2f * k3Theta + k4Theta);
            omega += (deltaTime / 6f) * (k1Omega + 2f * k2Omega + 2f * k3Omega + k4Omega);
            angularAcceleration = ComputeAngularAcceleration(theta, omega, ropeLength, gravity, damping);
        }

        public static float ComputeMechanicalEnergy(
            float theta,
            float omega,
            float ropeLength,
            float gravity) =>
            0.5f * omega * omega * ropeLength * ropeLength
            + gravity * ropeLength * (1f - math.cos(theta));
    }
}
