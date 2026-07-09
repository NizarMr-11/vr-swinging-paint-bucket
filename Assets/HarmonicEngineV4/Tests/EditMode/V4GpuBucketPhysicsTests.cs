using HarmonicEngineV4.Core;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4GpuBucketPhysicsTests
    {
        private const float L = 2.6f;
        private const float G = 9.81f;
        private const float Dt = 0.02f;

        [Test]
        public void GpuBucketRotation_MatchesCpuLookRotation()
        {
            Vector3[] directions =
            {
                Vector3.down,
                V4SphericalPendulumMath.NormalizeDirection(new Vector3(0.3f, -0.9f, 0.2f), Vector3.down),
                V4SphericalPendulumMath.NormalizeDirection(new Vector3(0.8f, -0.5f, -0.4f), Vector3.down)
            };

            ComputeShader shader = V4ShaderLibrary.Load(V4ShaderLibrary.BucketPhysics);
            int integrateKernel = shader.FindKernel("BucketIntegrateKernel");
            var buffer = new ComputeBuffer(1, V4GpuBucketState.Stride);

            foreach (Vector3 direction in directions)
            {
                V4GpuBucketState state = V4GpuBucketState.FromPendulumParams(
                    new Vector3(0f, 3f, 0f),
                    L,
                    direction,
                    Vector3.zero,
                    G,
                    damping: 0.05f,
                    bucketMass: 0.5f,
                    sloshFeedbackScale: 0f,
                    fluidMassSmoothing: 0.2f,
                    twistDegrees: 0f,
                    applySlosh: false);

                Quaternion cpuRotation = V4SphericalPendulumMath.ComputeBucketRotation(direction, 0f);
                buffer.SetData(new[] { state });
                shader.SetBuffer(integrateKernel, V4GpuBucketState.BufferName, buffer);
                shader.SetFloat("_DeltaTime", 0f);
                shader.Dispatch(integrateKernel, 1, 1, 1);

                var gpuScratch = new V4GpuBucketState[1];
                buffer.GetData(gpuScratch);
                Quaternion gpuRotation = new Quaternion(
                    gpuScratch[0].worldRotation.x,
                    gpuScratch[0].worldRotation.y,
                    gpuScratch[0].worldRotation.z,
                    gpuScratch[0].worldRotation.w);

                float angle = Quaternion.Angle(cpuRotation, gpuRotation);
                Assert.Less(angle, 1f, $"rotation mismatch for direction {direction}");

                Vector3 pivot = new Vector3(0f, 3f, 0f);
                Vector3 bucketPos = gpuScratch[0].worldOrigin;
                Assert.IsTrue(V4SphericalPendulumMath.BucketAxisAlignsWithHangPoint(
                    gpuRotation, bucketPos, pivot));
            }

            buffer.Release();
        }

        [Test]
        public void CpuVerlet_MatchesReferenceAfterSeveralSteps()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.3f, -0.9f, 0.1f),
                Vector3.down);
            Vector3 tangentialVelocity = new Vector3(0.2f, 0f, -0.1f);
            tangentialVelocity = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                tangentialVelocity,
                unitDirection);

            Vector3 cpuDirection = unitDirection;
            Vector3 cpuVelocity = tangentialVelocity;
            for (int i = 0; i < 30; i++)
            {
                V4SphericalPendulumMath.IntegrateVerletStep(
                    ref cpuDirection,
                    ref cpuVelocity,
                    L,
                    G,
                    damping: 0.05f,
                    Dt,
                    Vector3.zero);
            }

            ComputeShader shader = V4ShaderLibrary.Load(V4ShaderLibrary.BucketPhysics);
            int integrateKernel = shader.FindKernel("BucketIntegrateKernel");

            var state = V4GpuBucketState.FromPendulumParams(
                new Vector3(0f, 3f, 0f),
                L,
                unitDirection,
                tangentialVelocity,
                G,
                damping: 0.05f,
                bucketMass: 0.5f,
                sloshFeedbackScale: 0f,
                fluidMassSmoothing: 0.2f,
                twistDegrees: 0f,
                applySlosh: false);

            var buffer = new ComputeBuffer(1, V4GpuBucketState.Stride);
            buffer.SetData(new[] { state });
            shader.SetBuffer(integrateKernel, V4GpuBucketState.BufferName, buffer);
            shader.SetFloat("_DeltaTime", Dt);

            for (int i = 0; i < 30; i++)
            {
                shader.Dispatch(integrateKernel, 1, 1, 1);
            }

            var gpuScratch = new V4GpuBucketState[1];
            buffer.GetData(gpuScratch);
            buffer.Release();

            Assert.AreEqual(cpuDirection.x, gpuScratch[0].unitDirection.x, 1e-3f);
            Assert.AreEqual(cpuDirection.y, gpuScratch[0].unitDirection.y, 1e-3f);
            Assert.AreEqual(cpuDirection.z, gpuScratch[0].unitDirection.z, 1e-3f);
            Assert.AreEqual(cpuVelocity.x, gpuScratch[0].tangentialVelocity.x, 1e-2f);
            Assert.AreEqual(cpuVelocity.y, gpuScratch[0].tangentialVelocity.y, 1e-2f);
            Assert.AreEqual(cpuVelocity.z, gpuScratch[0].tangentialVelocity.z, 1e-2f);
        }

        [Test]
        public void GpuBucketState_StrideMatchesHlslLayout()
        {
            Assert.AreEqual(176, V4GpuBucketState.Stride);
        }

        [Test]
        public void FromScenePose_PreservesOffCenterManualWorldPosition()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 bucketWorld = new Vector3(-0.375f, 0.346f, 0.859f);
            Vector3 delta = bucketWorld - pivot;
            Vector3 unitDir = delta.normalized;
            float ropeLength = delta.magnitude;
            Quaternion rotation = V4SphericalPendulumMath.ComputeBucketRotation(unitDir, 0f);

            V4GpuBucketState state = V4GpuBucketState.FromScenePose(
                pivot,
                bucketWorld,
                rotation,
                unitDir,
                ropeLength,
                Vector3.zero,
                gravity: 9.81f,
                damping: 0.05f,
                bucketMass: 0.5f,
                sloshFeedbackScale: 0f,
                fluidMassSmoothing: 0.2f,
                twistDegrees: 0f,
                applySlosh: false);

            Assert.AreEqual(bucketWorld.x, state.worldOrigin.x, 1e-4f);
            Assert.AreEqual(bucketWorld.y, state.worldOrigin.y, 1e-4f);
            Assert.AreEqual(bucketWorld.z, state.worldOrigin.z, 1e-4f);
        }

        [Test]
        public void GpuIntegrate_ProducesNonZeroLinearAccelerationOnSwing()
        {
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.3f, -0.9f, 0.1f),
                Vector3.down);
            Vector3 tangentialVelocity = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                new Vector3(0.4f, 0f, -0.2f),
                unitDirection);

            ComputeShader shader = V4ShaderLibrary.Load(V4ShaderLibrary.BucketPhysics);
            int integrateKernel = shader.FindKernel("BucketIntegrateKernel");
            var state = V4GpuBucketState.FromPendulumParams(
                new Vector3(0f, 3f, 0f),
                L,
                unitDirection,
                tangentialVelocity,
                G,
                damping: 0.05f,
                bucketMass: 0.5f,
                sloshFeedbackScale: 0f,
                fluidMassSmoothing: 0.2f,
                twistDegrees: 0f,
                applySlosh: false);

            var buffer = new ComputeBuffer(1, V4GpuBucketState.Stride);
            buffer.SetData(new[] { state });
            shader.SetBuffer(integrateKernel, V4GpuBucketState.BufferName, buffer);
            shader.SetFloat("_DeltaTime", Dt);
            shader.Dispatch(integrateKernel, 1, 1, 1);

            var gpuScratch = new V4GpuBucketState[1];
            buffer.GetData(gpuScratch);
            buffer.Release();

            float accelMag = gpuScratch[0].linearAcceleration.magnitude;
            Assert.Greater(accelMag, 0.05f, "swinging pendulum should produce bucket linear acceleration");
        }

        [Test]
        public void GpuIntegrate_PoseMatchesKinematicChain()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 unitDirection = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(-0.1f, -0.95f, 0.2f),
                Vector3.down);

            ComputeShader shader = V4ShaderLibrary.Load(V4ShaderLibrary.BucketPhysics);
            int integrateKernel = shader.FindKernel("BucketIntegrateKernel");
            var state = V4GpuBucketState.FromPendulumParams(
                pivot,
                L,
                unitDirection,
                Vector3.zero,
                G,
                damping: 0.05f,
                bucketMass: 0.5f,
                sloshFeedbackScale: 0f,
                fluidMassSmoothing: 0.2f,
                twistDegrees: 15f,
                applySlosh: false);

            var buffer = new ComputeBuffer(1, V4GpuBucketState.Stride);
            buffer.SetData(new[] { state });
            shader.SetBuffer(integrateKernel, V4GpuBucketState.BufferName, buffer);
            shader.SetFloat("_DeltaTime", Dt);
            shader.Dispatch(integrateKernel, 1, 1, 1);

            var gpuScratch = new V4GpuBucketState[1];
            buffer.GetData(gpuScratch);
            buffer.Release();

            Vector3 expectedOrigin = pivot + gpuScratch[0].unitDirection * L;
            Assert.AreEqual(expectedOrigin.x, gpuScratch[0].worldOrigin.x, 1e-3f);
            Assert.AreEqual(expectedOrigin.y, gpuScratch[0].worldOrigin.y, 1e-3f);
            Assert.AreEqual(expectedOrigin.z, gpuScratch[0].worldOrigin.z, 1e-3f);
            Assert.AreEqual(
                gpuScratch[0].tangentialVelocity.x,
                gpuScratch[0].linearVelocity.x,
                1e-4f);
        }

        [Test]
        public void GpuBucketWorldToLocal_RoundTripsLocalSpawnPoint()
        {
            Vector3 pivot = new Vector3(0f, 3f, 0f);
            Vector3 direction = V4SphericalPendulumMath.NormalizeDirection(
                new Vector3(0.3f, -0.9f, 0.2f),
                Vector3.down);
            float ropeLength = 2.6f;
            Vector3 worldOrigin = pivot + direction * ropeLength;
            Quaternion rotation = V4SphericalPendulumMath.ComputeBucketRotation(direction, 0f);
            Matrix4x4 localToWorld = Matrix4x4.TRS(worldOrigin, rotation, Vector3.one);
            Matrix4x4 worldToLocal = localToWorld.inverse;
            Vector3 localSpawn = new Vector3(0.05f, 0.12f, -0.08f);
            Vector3 worldSpawn = localToWorld.MultiplyPoint3x4(localSpawn);
            Vector3 roundTrip = worldToLocal.MultiplyPoint3x4(worldSpawn);

            Assert.AreEqual(localSpawn.x, roundTrip.x, 1e-4f);
            Assert.AreEqual(localSpawn.y, roundTrip.y, 1e-4f);
            Assert.AreEqual(localSpawn.z, roundTrip.z, 1e-4f);
        }
    }
}
