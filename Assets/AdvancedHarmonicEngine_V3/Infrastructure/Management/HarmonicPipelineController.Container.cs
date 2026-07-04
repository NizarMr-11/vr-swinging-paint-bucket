using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        private Matrix4x4 _containerLocalToWorld = Matrix4x4.identity;
        private Matrix4x4 _containerWorldToLocal = Matrix4x4.identity;
        private Vector3 _containerFloorPivot;

        public void SetContainerFluidOriented(
            Vector3 floorPivotWorld,
            Quaternion worldRotation,
            float radius,
            float height,
            float restitution,
            float friction,
            float wallStiffness)
        {
            openTopCylinder.ApplyOrientedBounds(
                floorPivotWorld,
                worldRotation,
                radius,
                height,
                restitution,
                friction,
                wallStiffness);

            _containerFloorPivot = floorPivotWorld;
            _containerLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivotWorld, worldRotation);
            _containerWorldToLocal = _containerLocalToWorld.inverse;
        }

        public void ApplyContainerRigidRotation(Matrix4x4 prevLocalToWorld, Matrix4x4 currLocalToWorld, float deltaTime) =>
            _rigidBodyCarryPass.ApplyRotation(this, prevLocalToWorld, currLocalToWorld, deltaTime);

        internal void ApplyContainerPbfUniforms(ComputeShader shader)
        {
            shader.SetMatrix(ContainerLocalToWorldId, _containerLocalToWorld);
            shader.SetMatrix(ContainerWorldToLocalId, _containerWorldToLocal);
            shader.SetFloat(ContainerHeightId, openTopCylinder.height);
            shader.SetInt(ContainerUsesOrientationId, 1);
            shader.SetVector(ContainerFloorPivotId, _containerFloorPivot);
        }
    }
}
