using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    internal sealed class OpenTopCylinderRigidBodyCarryPass
    {
        private static readonly ProfilerMarker MarkerContainerRigidCarry = new("Harmonic.ContainerRigidCarry");

        public void ApplyRotation(
            HarmonicPipelineController host,
            Matrix4x4 prevLocalToWorld,
            Matrix4x4 currLocalToWorld,
            float deltaTime)
        {
            if (!host.ContainerFluidEnabled || host.ContainerRigidCarryShader == null || host.PingPong == null)
            {
                return;
            }

            if (prevLocalToWorld.Equals(currLocalToWorld))
            {
                return;
            }

            uint activeCount = host.SanitizeCountValue(host.FetchSoaActiveCount(host.PingPong.ReadSet));
            if (activeCount == 0)
            {
                return;
            }

            Matrix4x4 rotationDelta = currLocalToWorld * prevLocalToWorld.inverse;
            Quaternion prevRot = prevLocalToWorld.rotation;
            Quaternion currRot = currLocalToWorld.rotation;
            Quaternion deltaRot = currRot * Quaternion.Inverse(prevRot);
            deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
            if (angleDeg > 180f)
            {
                angleDeg -= 360f;
            }

            float safeDt = Mathf.Max(deltaTime, 1e-5f);
            Vector3 angularVelocity = axis.sqrMagnitude > 1e-8f
                ? axis.normalized * (angleDeg * Mathf.Deg2Rad / safeDt)
                : Vector3.zero;

            // Translational velocity of the container pivot this frame. The rigid point
            // velocity of any carried particle is v_pivot + cross(omega, arm); the velocity-
            // agreement gate needs this term or pure-translation sweeps score zero relative
            // speed and would wrongly promote stationary ground particles.
            Vector3 currPivot = currLocalToWorld.GetColumn(3);
            Vector3 prevPivot = prevLocalToWorld.GetColumn(3);
            Vector3 linearVelocity = (currPivot - prevPivot) / safeDt;

            if (host.KernelContainerRigidCarry < 0)
            {
                return;
            }

            using (MarkerContainerRigidCarry.Auto())
            {
                // Pre-carry classify on frame-start positions; only runs when carry dispatch fires.
                host.ClassifyParticleFieldForCarry(activeCount);
                host.ApplyOtcFieldUniforms(host.ContainerRigidCarryShader);
                host.ContainerRigidCarryShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                host.ContainerRigidCarryShader.SetInt(HarmonicShaderPropertyIds.MaxParticleCount, host.MaxCapacity);
                host.ContainerRigidCarryShader.SetMatrix(HarmonicShaderPropertyIds.ContainerRotationDelta, rotationDelta);
                host.ContainerRigidCarryShader.SetMatrix(HarmonicShaderPropertyIds.ContainerWorldToLocal, host.ContainerWorldToLocal);
                host.ContainerRigidCarryShader.SetMatrix(HarmonicShaderPropertyIds.ContainerLocalToWorld, host.ContainerLocalToWorld);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerHeight, host.ContainerFluidHeight);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerRadius, host.ContainerFluidRadius);
                host.ContainerRigidCarryShader.SetInt(HarmonicShaderPropertyIds.ContainerUsesOrientation, 1);
                host.ContainerRigidCarryShader.SetVector(HarmonicShaderPropertyIds.ContainerCenter, host.ContainerFluidCenter);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerFloorY, host.ContainerFluidFloorY);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerRimY, host.ContainerFluidRimY);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerRestitution, host.ContainerFluidRestitution);
                host.ContainerRigidCarryShader.SetFloat(HarmonicShaderPropertyIds.ContainerFriction, host.ContainerFluidFriction);
                host.ContainerRigidCarryShader.SetVector(HarmonicShaderPropertyIds.ContainerAngularVelocityWorld, angularVelocity);
                host.ContainerRigidCarryShader.SetVector(HarmonicShaderPropertyIds.ContainerLinearVelocityWorld, linearVelocity);
                host.ContainerRigidCarryShader.SetVector(HarmonicShaderPropertyIds.ContainerFloorPivot, host.ContainerFloorPivot);
                host.ContainerRigidCarryShader.SetBuffer(host.KernelContainerRigidCarry, HarmonicShaderPropertyIds.Block0, host.PingPong.ReadSet.Block0);
                host.ContainerRigidCarryShader.SetBuffer(host.KernelContainerRigidCarry, HarmonicShaderPropertyIds.Block1, host.PingPong.ReadSet.Block1);
                host.ContainerRigidCarryShader.SetBuffer(
                    host.KernelContainerRigidCarry,
                    HarmonicShaderPropertyIds.PrevCarryContribution,
                    host.PrevCarryContributionBuffer);
                host.ContainerRigidCarryShader.SetBuffer(
                    host.KernelContainerRigidCarry,
                    HarmonicShaderPropertyIds.PrevInsideForCarry,
                    host.PrevInsideForCarryBuffer);
                OtcParticleFieldPass.BindFieldRead(
                    host.ContainerRigidCarryShader,
                    host.KernelContainerRigidCarry,
                    host.PbfScratch.ParticleField);
                int groups = Mathf.CeilToInt(activeCount / 64f);
                host.ContainerRigidCarryShader.Dispatch(host.KernelContainerRigidCarry, groups, 1, 1);
            }
        }
    }
}
