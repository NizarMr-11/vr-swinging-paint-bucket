using HarmonicEngine.Core.Mathematics.Integrators;
using NUnit.Framework;
using Unity.Jobs;

namespace HarmonicEngine.Tests
{
    public class PendulumRk4JobTests
    {
        [Test]
        public void PendulumRk4Job_ExecutesWithoutError()
        {
            var job = new PendulumRk4Job
            {
                Theta = 0.5f,
                Omega = 0.1f,
                RopeLength = 5f,
                Gravity = 9.81f,
                Damping = 0.05f,
                DeltaTime = 0.02f
            };

            job.Schedule().Complete();

            Assert.IsFalse(float.IsNaN(job.ResultTheta));
            Assert.IsFalse(float.IsNaN(job.ResultOmega));
            Assert.IsFalse(float.IsNaN(job.ResultAngularAcceleration));
        }
    }
}
