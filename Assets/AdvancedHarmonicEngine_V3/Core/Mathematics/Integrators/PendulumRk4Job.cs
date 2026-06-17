using Unity.Burst;
using Unity.Jobs;

namespace HarmonicEngine.Core.Mathematics.Integrators
{
    [BurstCompile]
    public struct PendulumRk4Job : IJob
    {
        public float Theta;
        public float Omega;
        public float RopeLength;
        public float Gravity;
        public float Damping;
        public float DeltaTime;

        public float ResultTheta;
        public float ResultOmega;
        public float ResultAngularAcceleration;

        public void Execute()
        {
            float theta = Theta;
            float omega = Omega;
            PendulumRk4Integrator.Step(
                ref theta,
                ref omega,
                out float alpha,
                RopeLength,
                Gravity,
                Damping,
                DeltaTime);
            ResultTheta = theta;
            ResultOmega = omega;
            ResultAngularAcceleration = alpha;
        }
    }
}
