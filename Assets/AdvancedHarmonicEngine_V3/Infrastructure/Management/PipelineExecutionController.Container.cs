using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private static readonly ProfilerMarker MarkerContainerRigidCarry = new("Harmonic.ContainerRigidCarry");

        private Matrix4x4 _containerLocalToWorld = Matrix4x4.identity;
        private Matrix4x4 _containerWorldToLocal = Matrix4x4.identity;
        private Vector3 _containerFloorPivot;
        private int _kernelContainerRigidCarry = -1;

        public void SetContainerFluidOriented(
            Vector3 floorPivotWorld,
            Quaternion worldRotation,
            float radius,
            float height,
            float restitution,
            float friction,
            float wallStiffness,
            bool spillOverRim = true)
        {
            containerFluid.ApplyOrientedBounds(
                floorPivotWorld,
                worldRotation,
                radius,
                height,
                restitution,
                friction,
                wallStiffness,
                spillOverRim);

            _containerFloorPivot = floorPivotWorld;
            _containerLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivotWorld, worldRotation);
            _containerWorldToLocal = _containerLocalToWorld.inverse;
        }

        public void ApplyContainerRigidRotation(Matrix4x4 prevLocalToWorld, Matrix4x4 currLocalToWorld, float deltaTime)
        {
            if (!containerFluid.enabled || containerRigidCarryShader == null || _pingPong == null)
            {
                return;
            }

            if (prevLocalToWorld.Equals(currLocalToWorld))
            {
                return;
            }

            uint activeCount = SanitizeCount(FetchActiveCount(_pingPong.ReadSet));
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

            if (_kernelContainerRigidCarry < 0)
            {
                _kernelContainerRigidCarry = containerRigidCarryShader.FindKernel("ContainerRigidCarryKernel");
            }

            using (MarkerContainerRigidCarry.Auto())
            {
                containerRigidCarryShader.SetInt(ActiveParticleCountId, (int)activeCount);
                containerRigidCarryShader.SetInt(MaxParticleCountId, maxCapacity);
                containerRigidCarryShader.SetMatrix(ContainerRotationDeltaId, rotationDelta);
                containerRigidCarryShader.SetVector(ContainerAngularVelocityWorldId, angularVelocity);
                containerRigidCarryShader.SetVector(ContainerFloorPivotId, _containerFloorPivot);
                containerRigidCarryShader.SetBuffer(_kernelContainerRigidCarry, Block0Id, _pingPong.ReadSet.Block0);
                containerRigidCarryShader.SetBuffer(_kernelContainerRigidCarry, Block1Id, _pingPong.ReadSet.Block1);
                int groups = Mathf.CeilToInt(activeCount / 64f);
                containerRigidCarryShader.Dispatch(_kernelContainerRigidCarry, groups, 1, 1);
            }
        }

        private void ApplyContainerPbfUniforms(ComputeShader shader)
        {
            shader.SetMatrix(ContainerLocalToWorldId, _containerLocalToWorld);
            shader.SetMatrix(ContainerWorldToLocalId, _containerWorldToLocal);
            shader.SetFloat(ContainerHeightId, containerFluid.height);
            shader.SetInt(ContainerSpillEnabledId, containerFluid.spillOverRim ? 1 : 0);
            shader.SetInt(ContainerUsesOrientationId, 1);
            shader.SetVector(ContainerFloorPivotId, _containerFloorPivot);
        }
    }
}
