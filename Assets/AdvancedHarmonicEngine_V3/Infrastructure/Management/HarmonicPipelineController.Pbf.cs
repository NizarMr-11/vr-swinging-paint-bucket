using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        private float ResolvePbfEpsilon(float smoothingRadius) =>
            pbfEpsilonScale / Mathf.Pow(smoothingRadius, 4f);

        private void ApplyPbfUniformsInternal(ComputeShader shader, float smoothingRadius, float deltaTime)
        {
            shader.SetInt(GridResolutionId, _frameSortSize);
            shader.SetFloat(CellSizeId, cellSize);
            shader.SetFloat(SmoothingRadiusId, smoothingRadius);
            shader.SetFloat(ParticleMassId, ResolveContainerParticleMass());
            shader.SetFloat(RestDensityId, sphSolver.RestDensity);
            shader.SetFloat(ViscosityId, openTopCylinder.viscosity);
            shader.SetFloat(PbfEpsilonId, ResolvePbfEpsilon(smoothingRadius));
            shader.SetFloat(PbfRelaxationId, pbfRelaxation);
            shader.SetFloat(PbfVelocityDampingId, pbfVelocityDamping);
            shader.SetFloat(PbfMaxPositionDeltaId, pbfMaxPositionDelta);
            shader.SetFloat(CohesionStrengthId, pbfCohesion);
            shader.SetVector(GravityId, gravity);
            shader.SetVector(ContainerCenterId, openTopCylinder.center);
            shader.SetFloat(ContainerRadiusId, openTopCylinder.radius);
            shader.SetFloat(ContainerFloorYId, openTopCylinder.floorY);
            shader.SetFloat(ContainerRimYId, openTopCylinder.rimY);
            shader.SetFloat(ContainerRestitutionId, openTopCylinder.restitution);
            shader.SetFloat(ContainerFrictionId, openTopCylinder.friction);
            shader.SetFloat(DeltaTimeId, deltaTime);
            shader.SetInt(MaxParticleCountId, maxCapacity);
            shader.SetFloat(WorldDragId, worldDrag);
            shader.SetFloat(CanvasPlaneYId, canvasPlaneY);
            shader.SetInt(CanvasCullingEnabledId, canvasCullingEnabled ? 1 : 0);
            shader.SetInt(CanvasPaintAbsorbEnabledId, canvasPaintAbsorbEnabled ? 1 : 0);
            shader.SetFloat(CanvasAbsorbRateId, canvasAbsorbRate);
            shader.SetFloat(CanvasAbsorbPaintWeightScaleId, canvasAbsorbPaintWeightScale);
            shader.SetFloat(FloorRestitutionId, floorRestitution);
            shader.SetFloat(FloorFrictionId, floorFriction);
            ApplyContainerPbfUniforms(shader);
        }
    }
}
