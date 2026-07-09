using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4SphericalPendulumTests
    {
        private const float L = 2.6f;
        private const float G = 9.81f;

        [Test]
        public void IntegrateVerletStep_PreservesSphereRadius()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.7f, -0.7f, 0.3f),
                Vector3.down);
            Vector3 tangentialVelocity = Vector3.zero;
            Vector3 pivot = new Vector3(0f, 3f, 0f);

            for (int i = 0; i < 120; i++)
            {
                V4SphericalPendulumMath.IntegrateVerletStep(
                    ref unitDirection,
                    ref tangentialVelocity,
                    L,
                    G,
                    damping: 0.05f,
                    deltaTime: 0.02f,
                    sloshAcceleration: Vector3.zero);

                Vector3 worldPos = V4SphericalPendulumMath.WorldPosition(pivot, L, unitDirection);
                float distance = Vector3.Distance(worldPos, pivot);
                Assert.AreEqual(L, distance, 1e-3f, $"radius drift at step {i}");
            }
        }

        [Test]
        public void IntegrateVerletStep_WithDamping_ReducesSpeed()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(Vector3.right, Vector3.down);
            Vector3 tangentialVelocity = Vector3.forward * 2f;
            float initialSpeed = tangentialVelocity.magnitude;

            V4SphericalPendulumMath.IntegrateVerletStep(
                ref unitDirection,
                ref tangentialVelocity,
                L,
                G,
                damping: 0.5f,
                deltaTime: 0.02f,
                sloshAcceleration: Vector3.zero);

            Assert.Less(tangentialVelocity.magnitude, initialSpeed);
        }

        [Test]
        public void IntegrateStep_PreservesSphereRadius()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.7f, -0.7f, 0.3f),
                Vector3.down);
            Vector3 tangentialVelocity = Vector3.zero;
            Vector3 pivot = new Vector3(0f, 3f, 0f);

            for (int i = 0; i < 120; i++)
            {
                V4SphericalPendulumMath.IntegrateStep(
                    ref unitDirection,
                    ref tangentialVelocity,
                    L,
                    G,
                    damping: 0.05f,
                    deltaTime: 0.02f,
                    sloshAcceleration: Vector3.zero);

                Vector3 worldPos = V4SphericalPendulumMath.WorldPosition(pivot, L, unitDirection);
                float distance = Vector3.Distance(worldPos, pivot);
                Assert.AreEqual(L, distance, 1e-3f, $"radius drift at step {i}");
            }
        }

        [Test]
        public void IntegrateStep_WithDamping_ReducesSpeed()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(Vector3.right, Vector3.down);
            Vector3 tangentialVelocity = Vector3.forward * 2f;
            float initialSpeed = tangentialVelocity.magnitude;

            V4SphericalPendulumMath.IntegrateStep(
                ref unitDirection,
                ref tangentialVelocity,
                L,
                G,
                damping: 0.5f,
                deltaTime: 0.02f,
                sloshAcceleration: Vector3.zero);

            Assert.Less(tangentialVelocity.magnitude, initialSpeed);
        }

        [Test]
        public void ComputeAngularVelocity_IsPerpendicularToRopeDirection()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.2f, -0.9f, 0.3f),
                Vector3.down);
            Vector3 tangentialVelocity = new Vector3(0.4f, 0f, -0.2f);
            tangentialVelocity = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                tangentialVelocity,
                unitDirection);

            Vector3 omega = V4SphericalPendulumMath.ComputeAngularVelocity(
                unitDirection,
                tangentialVelocity,
                L);

            float dot = Vector3.Dot(omega, unitDirection);
            Assert.AreEqual(0f, dot, 1e-4f);
        }

        [Test]
        public void ComputeBucketRotation_AlignsFloorRimAxisTowardPivot()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 bucket = new Vector3(0.8f, 0.5f, -0.4f);
            V4SphericalPendulumMath.DecomposePose(
                pivot,
                bucket,
                twistDegrees: 0f,
                out float length,
                out Vector3 direction,
                out float alpha,
                out float beta,
                out float omega);

            Quaternion rotation = V4SphericalPendulumMath.ComputeBucketRotation(direction, 0f);
            Vector3 worldPos = V4SphericalPendulumMath.WorldPosition(pivot, length, direction);
            Assert.IsTrue(V4SphericalPendulumMath.BucketAxisAlignsWithHangPoint(rotation, worldPos, pivot));
        }

        [Test]
        public void DecomposePoseFromFloor_UsesHangEarInsteadOfFloorCenter()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 floor = new Vector3(0f, 0.4f, 0f);
            float attachY = 1.12f;
            Quaternion rotation = V4SphericalPendulumMath.ComputeBucketRotation(Vector3.down, 0f);
            V4SphericalPendulumMath.DecomposePoseFromFloor(
                pivot,
                floor,
                rotation,
                attachY,
                twistDegrees: 0f,
                out float length,
                out Vector3 direction,
                out float alpha,
                out float beta,
                out float omega);

            Vector3 hangPoint = V4SphericalPendulumMath.HangPointFromFloor(floor, rotation, attachY);
            Assert.AreEqual(Vector3.Distance(hangPoint, pivot), length, 1e-3f);
            Assert.AreEqual(Vector3.down, direction, "rope should point straight down from hang ear");
            Assert.AreEqual(180f, beta, 1e-2f);
            Assert.AreEqual(0f, omega, 1e-3f);
        }

        [Test]
        public void DecomposePose_MatchesManualBucketPlacement()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 bucket = new Vector3(0f, 0.4f, 0f);
            V4SphericalPendulumMath.DecomposePose(
                pivot,
                bucket,
                twistDegrees: 15f,
                out float length,
                out Vector3 direction,
                out float alpha,
                out float beta,
                out float omega);

            Assert.AreEqual(2.6f, length, 1e-3f);
            Assert.AreEqual(0f, alpha, 1e-3f);
            Assert.AreEqual(180f, beta, 1e-2f, "β is polar angle from world +Y; straight down is 180°");
            Assert.AreEqual(15f, omega, 1e-3f);
            Assert.AreEqual(Vector3.down, direction, "rope should point straight down");
        }
    }
}
