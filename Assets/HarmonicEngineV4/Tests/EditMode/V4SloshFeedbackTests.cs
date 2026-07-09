using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4SloshFeedbackTests
    {
        private const float L = 2.6f;
        private const float G = 9.81f;

        [Test]
        public void SloshAcceleration_OpposesTangentComOffset()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.5f, -0.8f, 0.2f),
                Vector3.down);
            Vector3 comOffset = new Vector3(0.1f, 0f, 0.05f);
            Vector3 tangentCom = V4SphericalPendulumMath.ProjectOntoTangentPlane(comOffset, unitDirection);
            Assume.That(tangentCom.sqrMagnitude, Is.GreaterThan(1e-6f));

            Vector3 accel = V4SphericalPendulumMath.ComputeSloshAcceleration(
                unitDirection,
                comOffset,
                fluidMass: 1f,
                bucketMass: 0.5f,
                ropeLength: L,
                gravity: G,
                feedbackScale: 1f);

            Assert.Less(Vector3.Dot(accel, tangentCom.normalized), 0f);
        }

        [Test]
        public void SloshAcceleration_ZeroWhenNoFluid()
        {
            Vector3 unitDirection = Vector3.down;
            Vector3 accel = V4SphericalPendulumMath.ComputeSloshAcceleration(
                unitDirection,
                new Vector3(0.2f, 0f, 0f),
                fluidMass: 0f,
                bucketMass: 0.5f,
                ropeLength: L,
                gravity: G,
                feedbackScale: 1f);

            Assert.AreEqual(Vector3.zero, accel);
        }
    }
}
