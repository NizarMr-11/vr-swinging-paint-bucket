using HarmonicEngine.Domain.Models;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private float _estimatedMaxSpeed = 5f;
        private float _cflLogAccumulator;

        private const float CflSonicFactor = 0.20f;
        private const float CflVelocityFactor = 0.40f;

        private static readonly ProfilerMarker MarkerDensity = new("Harmonic.SphDensity");
        private static readonly ProfilerMarker MarkerIntegration = new("Harmonic.SphIntegration");
        private static readonly ProfilerMarker MarkerContainerFrame = new("Harmonic.ContainerFluidFrame");
        private static readonly ProfilerMarker MarkerWorldFalling = new("Harmonic.WorldFalling");

        public void ExecutePipelineFrame(float deltaTime)
        {
            if (!simulationActive || simulationMode == HarmonicSimulationMode.BakePlayback || !AreShadersReady())
            {
                return;
            }

            _pingPong.BeginFrame();
            _bufferFalling.SetCounterValue(0);
            _bufferFallingWorld?.SetCounterValue(0);
            _bufferCanvasHits?.SetCounterValue(0);
            _lastCanvasHitCount = 0;

            uint activeCount = SanitizeAndRepairCount(_pingPong.ReadBuffer);
            _cachedInternalCount = activeCount;
            if (activeCount == 0)
            {
                _lastFallingQuantizeCount = 0;
                _lastFallingDebugCount = 0;
                _pingPong.Swap();
                PublishPipelineFrameDiagnostic(0);
                return;
            }

            if (worldFallingOnly)
            {
                ExecuteWorldFallingOnlyFrame(activeCount, deltaTime);
                PublishPipelineFrameDiagnostic(_cachedInternalCount);
                return;
            }

            if (containerFluid.enabled)
            {
                ExecuteContainerFluidFrame(activeCount, deltaTime);
                PublishPipelineFrameDiagnostic(_cachedInternalCount);
                return;
            }

            ExecuteBucketSphFrame(activeCount, deltaTime);
            PublishPipelineFrameDiagnostic(_cachedInternalCount);
        }

        /// <summary>
        /// Runs container-fluid spatial hash + SPH density pass only (no integration).
        /// Used by GPU verification tests to validate ExecuteSphDensityPass at frame 0.
        /// </summary>
        public void ExecuteContainerSphDensityForVerification()
        {
            if (!AreShadersReady() || _pingPong == null || !containerFluid.enabled)
            {
                return;
            }

            uint activeCount = SanitizeAndRepairCount(_pingPong.ReadBuffer);
            _cachedInternalCount = activeCount;
            if (activeCount == 0)
            {
                return;
            }

            ComputeFrameSortSize(activeCount);
            BuildSpatialHashGrid(_pingPong.ReadBuffer, activeCount);

            float smoothingRadius = sphSolver.SmoothingRadius(cellSize);
            ApplyContainerSphUniforms(streamCompactionShader, smoothingRadius);

            using (MarkerDensity.Auto())
            {
                streamCompactionShader.SetBuffer(_kernelDensity, ReadOnlyParticleSourceId, _pingPong.ReadBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, CellStartEndBufferId, _cellStartEndBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, DensityWritableCacheId, _bufferDensityCache);
                streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
                streamCompactionShader.DispatchIndirect(_kernelDensity, _indirectArgsBuffer, 0);
            }
        }

        private void ExecuteBucketSphFrame(uint activeCount, float deltaTime)
        {
            float smoothingRadius = sphSolver.SmoothingRadius(cellSize);
            Matrix4x4 localToWorld = GetLocalToWorldMatrix();
            Vector3 bucketVelocity = GetBucketVelocity(deltaTime);
            ResolveAngularKinematics(out Vector3 angularVelocityWorld, out Vector3 angularAccelerationWorld);

            ComputeBuffer particleSourceForSph = _pingPong.ReadBuffer;
            if (enableEulerianDrag && eulerianDragGridShader != null && _bufferDragGrid != null)
            {
                RunEulerianDragPass(activeCount, deltaTime);
                particleSourceForSph = _bufferDragParticleScratch;
            }

            ComputeFrameSortSize(activeCount);
            BuildSpatialHashGrid(_pingPong.ReadBuffer, activeCount);

            ApplySphUniforms(streamCompactionShader, smoothingRadius, localToWorld, bucketVelocity, angularVelocityWorld, angularAccelerationWorld);
            using (MarkerDensity.Auto())
            {
                streamCompactionShader.SetBuffer(_kernelDensity, ReadOnlyParticleSourceId, particleSourceForSph);
                streamCompactionShader.SetBuffer(_kernelDensity, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, CellStartEndBufferId, _cellStartEndBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, DensityWritableCacheId, _bufferDensityCache);
                streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
                streamCompactionShader.DispatchIndirect(_kernelDensity, _indirectArgsBuffer, 0);
            }

            ApplySphUniforms(streamCompactionShader, smoothingRadius, localToWorld, bucketVelocity, angularVelocityWorld, angularAccelerationWorld);
            using (MarkerIntegration.Auto())
            {
                streamCompactionShader.SetBuffer(_kernelIntegration, DensityWritableCacheId, _bufferDensityCache);
                streamCompactionShader.SetBuffer(_kernelIntegration, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                streamCompactionShader.SetBuffer(_kernelIntegration, CellStartEndBufferId, _cellStartEndBuffer);
                streamCompactionShader.SetBuffer(_kernelIntegration, InternalAppendId, _pingPong.WriteBuffer);
                streamCompactionShader.SetBuffer(_kernelIntegration, FallingAppendId, _bufferFalling);
                streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
                streamCompactionShader.SetFloat(DeltaTimeId, deltaTime);
                streamCompactionShader.DispatchIndirect(_kernelIntegration, _indirectArgsBuffer, 0);
            }

            uint fallingCount = SanitizeCount(FetchActiveCount(_bufferFalling));
            ComputeBuffer quantizeSource = _bufferFalling;
            _lastFallingDebugCount = fallingCount;

            if (fallingCount > 0 && fallingFluidWorldShader != null)
            {
                _bufferFallingWorld.SetCounterValue(0);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingReadId, _bufferFalling);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingAppendId, _bufferFallingWorld);
                fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, CanvasHitAppendId, _bufferCanvasHits);
                ApplyFallingWorldUniforms(fallingFluidWorldShader, fallingCount, deltaTime);
                int fallingGroups = Mathf.CeilToInt(fallingCount / 64f);
                fallingFluidWorldShader.Dispatch(_kernelFallingWorld, fallingGroups, 1, 1);

                _lastCanvasHitCount = FetchActiveCount(_bufferCanvasHits);
                fallingCount = SanitizeAndRepairCount(_bufferFallingWorld);
                quantizeSource = _bufferFallingWorld;
                _lastFallingDebugCount = fallingCount;
            }

            _lastFallingQuantizeCount = fallingCount;
            if (fallingCount == 0)
            {
                _lastBucketPosition = GetBucketPosition();
                _pingPong.Swap();
                return;
            }

            ComputeBuffer.CopyCount(quantizeSource, _indirectArgsBuffer, sizeof(int) * 3);
            DispatchIndirectArgsSetup();

            dataCompactionShader.SetBuffer(_kernelQuantize, SourceParticleBufferId, quantizeSource);
            dataCompactionShader.SetBuffer(_kernelQuantize, QuantizedOutputBufferId, _quantizedBakeBuffer);
            dataCompactionShader.SetVector(QuantizationOriginId, GetBucketPosition());
            dataCompactionShader.SetInt(QuantizedCountId, (int)fallingCount);
            dataCompactionShader.DispatchIndirect(_kernelQuantize, _indirectArgsBuffer, 0);

            _lastBucketPosition = GetBucketPosition();
            _pingPong.Swap();
        }

        private void ExecuteWorldFallingOnlyFrame(uint activeCount, float deltaTime)
        {
            _lastFallingDebugCount = activeCount;
            _lastFallingQuantizeCount = activeCount;

            if (fallingFluidWorldShader == null)
            {
                _pingPong.Swap();
                return;
            }

            _pingPong.WriteBuffer.SetCounterValue(0);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingReadId, _pingPong.ReadBuffer);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, FallingAppendId, _pingPong.WriteBuffer);
            fallingFluidWorldShader.SetBuffer(_kernelFallingWorld, CanvasHitAppendId, _bufferCanvasHits);
            ApplyFallingWorldUniforms(fallingFluidWorldShader, activeCount, deltaTime);
            int groups = Mathf.CeilToInt(activeCount / 64f);
            using (MarkerWorldFalling.Auto())
            {
                fallingFluidWorldShader.Dispatch(_kernelFallingWorld, groups, 1, 1);
            }

            _lastCanvasHitCount = FetchActiveCount(_bufferCanvasHits);
            _cachedInternalCount = SanitizeAndRepairCount(_pingPong.WriteBuffer);
            _lastFallingDebugCount = _cachedInternalCount;
            _lastFallingQuantizeCount = _cachedInternalCount;
            _pingPong.Swap();

            if (verbosePipelineDiagnostics && !perfDiagnosticsMuted)
            {
                int lost = (int)activeCount - (int)_cachedInternalCount - (int)_lastCanvasHitCount;
                PublishStageDiagnostic(
                    "worldFalling",
                    $"in={activeCount} survived={_cachedInternalCount} canvasHits={_lastCanvasHitCount} lost={lost} " +
                    $"culling={canvasCullingEnabled} planeY={canvasPlaneY:F2} dt={deltaTime:F4}");
            }

            MaybeSampleParticlePositions(_pingPong.ReadBuffer, _cachedInternalCount, "worldFalling");
        }

        private void ExecuteContainerFluidFrame(uint activeCount, float deltaTime)
        {
            using (MarkerContainerFrame.Auto())
            {
                deltaTime = Mathf.Min(deltaTime, containerFluid.maxTimeStep);

                float h = sphSolver.SmoothingRadius(cellSize);
                float c = sphSolver.SpeedOfSound;
                float vMax = Mathf.Max(_estimatedMaxSpeed, 5f);
                float dtCfl = Mathf.Min(CflSonicFactor * h / c, CflVelocityFactor * h / vMax);
                int desiredSubsteps = Mathf.Max(2, Mathf.CeilToInt(deltaTime / dtCfl));
                int steps = Mathf.Clamp(desiredSubsteps, 2, maxCflSubsteps);
                float subDt = deltaTime / steps;
                float cflRatio = subDt / dtCfl;

                MaybeLogCflDiagnostic(c, h, dtCfl, desiredSubsteps, steps, subDt, cflRatio, deltaTime);

                int maxSubstepsBetweenRebuilds = ComputeMaxSubstepsBetweenHashRebuilds(vMax, subDt, cellSize);
                int substepsSinceRebuild = 0;
                int hashRebuildsThisFrame = 0;

                // activeCount is loop-invariant in container mode — no spawn/death.
                // Per-substep readback was ~79 sync GPU stalls/frame (~240ms on iGPU).
                for (int step = 0; step < steps; step++)
                {
                    if (substepsSinceRebuild == 0
                        || substepsSinceRebuild >= maxSubstepsBetweenRebuilds)
                    {
                        ComputeFrameSortSize(activeCount);
                        BuildSpatialHashGrid(_pingPong.ReadBuffer, activeCount);
                        MaybeLogStencilNeighborCount();
                        substepsSinceRebuild = 0;
                        hashRebuildsThisFrame++;
                    }

                    substepsSinceRebuild++;
                    RunContainerFluidSubstep(activeCount, subDt);
                }

                MaybeLogHashRebuildDiagnostic(hashRebuildsThisFrame, steps, deltaTime);

                _cachedInternalCount = SanitizeAndRepairCount(_pingPong.ReadBuffer);
                _lastFallingDebugCount = 0;
                _lastFallingQuantizeCount = 0;

                MaybeSampleParticlePositions(_pingPong.ReadBuffer, _cachedInternalCount, "containerFluid");

                if (!perfDiagnosticsMuted)
                {
                    PublishStageDiagnostic(
                        "containerFluid",
                        $"active={_cachedInternalCount} sortSize={_frameSortSize} R={containerFluid.radius:F2} " +
                        $"floorY={containerFluid.floorY:F2} rimY={containerFluid.rimY:F2} substeps={steps} dtCfl={dtCfl:F6} cflRatio={cflRatio:F2}");
                }
            }
        }

        private void RunContainerFluidSubstep(uint activeCount, float deltaTime)
        {
            _pingPong.WriteBuffer.SetCounterValue(0);

            float smoothingRadius = sphSolver.SmoothingRadius(cellSize);

            ApplyContainerSphUniforms(streamCompactionShader, smoothingRadius);
            using (MarkerDensity.Auto())
            {
                streamCompactionShader.SetBuffer(_kernelDensity, ReadOnlyParticleSourceId, _pingPong.ReadBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, CellStartEndBufferId, _cellStartEndBuffer);
                streamCompactionShader.SetBuffer(_kernelDensity, DensityWritableCacheId, _bufferDensityCache);
                streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
                streamCompactionShader.DispatchIndirect(_kernelDensity, _indirectArgsBuffer, 0);
            }

            ApplyContainerSphUniforms(streamCompactionShader, smoothingRadius);
            using (MarkerIntegration.Auto())
            {
                streamCompactionShader.SetBuffer(_kernelContainerIntegration, DensityWritableCacheId, _bufferDensityCache);
                streamCompactionShader.SetBuffer(_kernelContainerIntegration, SortedGridKeyValueBufferId, _gridKeyValueBuffer);
                streamCompactionShader.SetBuffer(_kernelContainerIntegration, CellStartEndBufferId, _cellStartEndBuffer);
                streamCompactionShader.SetBuffer(_kernelContainerIntegration, InternalAppendId, _pingPong.WriteBuffer);
                streamCompactionShader.SetInt(ActiveParticleCountId, (int)activeCount);
                streamCompactionShader.SetFloat(DeltaTimeId, deltaTime);
                streamCompactionShader.DispatchIndirect(_kernelContainerIntegration, _indirectArgsBuffer, 0);
            }

            _pingPong.Swap();
        }

        private void ApplyFallingWorldUniforms(ComputeShader shader, uint fallingCount, float deltaTime)
        {
            shader.SetInt(FallingCountId, (int)fallingCount);
            shader.SetFloat(DeltaTimeId, deltaTime);
            shader.SetVector(WorldGravityId, gravity);
            shader.SetFloat(WorldDragId, worldDrag);
            shader.SetFloat(CanvasPlaneYId, canvasPlaneY);
            shader.SetInt(CanvasCullingEnabledId, canvasCullingEnabled ? 1 : 0);
            shader.SetInt(CanvasPaintAbsorbEnabledId, canvasPaintAbsorbEnabled ? 1 : 0);
            shader.SetFloat(CanvasAbsorbRateId, canvasAbsorbRate);
            shader.SetFloat(CanvasAbsorbPaintWeightScaleId, canvasAbsorbPaintWeightScale);
            shader.SetFloat(FloorRestitutionId, floorRestitution);
            shader.SetFloat(FloorFrictionId, floorFriction);
        }

        private void ApplyContainerSphUniforms(ComputeShader shader, float smoothingRadius)
        {
            SyncSpeedOfSoundToSolver();
            shader.SetInt(GridResolutionId, _frameSortSize);
            shader.SetFloat(CellSizeId, cellSize);
            shader.SetFloat(SmoothingRadiusId, smoothingRadius);
            shader.SetFloat(ParticleMassId, ResolveContainerParticleMass());
            shader.SetFloat(GasConstantKId, containerFluid.gasConstantK);
            shader.SetFloat(StiffnessBId, sphSolver.StiffnessB);
            shader.SetFloat(RestDensityId, sphSolver.RestDensity);
            shader.SetFloat(ViscosityId, containerFluid.viscosity);
            shader.SetVector(GravityId, gravity);
            shader.SetVector(ContainerCenterId, containerFluid.center);
            shader.SetFloat(ContainerRadiusId, containerFluid.radius);
            shader.SetFloat(ContainerFloorYId, containerFluid.floorY);
            shader.SetFloat(ContainerRimYId, containerFluid.rimY);
            shader.SetFloat(ContainerRestitutionId, containerFluid.restitution);
            shader.SetFloat(ContainerFrictionId, containerFluid.friction);
            shader.SetFloat(ContainerWallStiffnessId, containerFluid.wallStiffness);
            shader.SetFloat(ContainerDampingId, containerFluid.velocityDamping);
            shader.SetFloat(ContainerMaxSpeedId, containerFluid.maxSpeed);
            shader.SetFloat(ColorDiffusionRateId, colorDiffusionRate);
            shader.SetInt(MaxParticleCountId, maxCapacity);
        }

        private float ResolveContainerParticleMass()
        {
            if (containerFluid.particleMass > 0f)
            {
                return containerFluid.particleMass;
            }

            float spacing = cellSize;
            return sphSolver.RestDensity * spacing * spacing * spacing;
        }

        private void ApplySphUniforms(
            ComputeShader shader,
            float smoothingRadius,
            Matrix4x4 localToWorld,
            Vector3 bucketVelocity,
            Vector3 angularVelocityWorld,
            Vector3 angularAccelerationWorld)
        {
            SyncSpeedOfSoundToSolver();
            shader.SetInt(GridResolutionId, _frameSortSize);
            shader.SetFloat(CellSizeId, cellSize);
            shader.SetFloat(SmoothingRadiusId, smoothingRadius);
            shader.SetFloat(ParticleMassId, sphSolver.ParticleMass);
            shader.SetFloat(GasConstantKId, sphSolver.GasConstantK);
            shader.SetFloat(StiffnessBId, sphSolver.StiffnessB);
            shader.SetFloat(RestDensityId, sphSolver.RestDensity);
            shader.SetFloat(ViscosityId, sphSolver.Viscosity);
            shader.SetVector(GravityId, gravity);
            shader.SetVector(AngularVelocityWorldId, angularVelocityWorld);
            shader.SetVector(AngularAccelerationWorldId, angularAccelerationWorld);
            shader.SetFloat(NozzlePlaneLocalYId, nozzlePlaneLocalY);
            shader.SetFloat(NozzleRadiusId, nozzleRadius);
            shader.SetFloat(BucketRimLocalYId, bucketRimLocalY);
            shader.SetMatrix(LocalToWorldMatrixId, localToWorld);
            shader.SetVector(InstantaneousBucketGlobalVelocityId, bucketVelocity);
            shader.SetFloat(ColorDiffusionRateId, colorDiffusionRate);
            shader.SetInt(MaxParticleCountId, maxCapacity);
        }

        private void ResolveAngularKinematics(out Vector3 angularVelocityWorld, out Vector3 angularAccelerationWorld)
        {
            if (!applyNonInertialPseudoForces || bucketKinematicProvider is not IBucketKinematicProvider provider)
            {
                angularVelocityWorld = Vector3.zero;
                angularAccelerationWorld = Vector3.zero;
                return;
            }

            angularVelocityWorld = provider.AngularVelocityWorld;
            angularAccelerationWorld = provider.AngularAccelerationWorld;
        }

        private void RunEulerianDragPass(uint activeCount, float deltaTime)
        {
            int gridVolume = Mathf.Max(1, dragGridVolume);
            int clearGroups = Mathf.CeilToInt(gridVolume / 256f);
            int applyGroups = Mathf.CeilToInt(activeCount / 64f);

            eulerianDragGridShader.SetBuffer(_kernelDragAdvect, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetFloat(DragDecayId, dragDecay);
            eulerianDragGridShader.SetFloat(AmbientWindStrengthId, ambientWindStrength);
            eulerianDragGridShader.SetFloat(DeltaTimeId, deltaTime);
            eulerianDragGridShader.Dispatch(_kernelDragAdvect, clearGroups, 1, 1);

            eulerianDragGridShader.SetBuffer(_kernelDragScatter, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetBuffer(_kernelDragScatter, ParticleSourceId, _pingPong.ReadBuffer);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
            eulerianDragGridShader.Dispatch(_kernelDragScatter, applyGroups, 1, 1);

            eulerianDragGridShader.SetBuffer(_kernelDragApply, DragGridId, _bufferDragGrid);
            eulerianDragGridShader.SetBuffer(_kernelDragApply, ParticleSourceId, _pingPong.ReadBuffer);
            eulerianDragGridShader.SetBuffer(_kernelDragApply, ParticleTargetId, _bufferDragParticleScratch);
            eulerianDragGridShader.SetInt(GridVolumeId, gridVolume);
            eulerianDragGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
            eulerianDragGridShader.SetFloat(DragStrengthId, dragStrength);
            eulerianDragGridShader.SetFloat(DeltaTimeId, deltaTime);
            eulerianDragGridShader.Dispatch(_kernelDragApply, applyGroups, 1, 1);
        }

        private Vector3 GetBucketVelocity(float deltaTime)
        {
            if (bucketKinematicProvider is IBucketKinematicProvider provider)
            {
                return provider.BucketWorldVelocity;
            }

            if (deltaTime <= 1e-6f)
            {
                return Vector3.zero;
            }

            Vector3 current = GetBucketPosition();
            return (current - _lastBucketPosition) / deltaTime;
        }

        private Matrix4x4 GetLocalToWorldMatrix()
        {
            Transform t = driveBucketFromTransform && bucketTransform != null ? bucketTransform : transform;
            return t.localToWorldMatrix;
        }

        private Vector3 GetBucketPosition()
        {
            Transform t = driveBucketFromTransform && bucketTransform != null ? bucketTransform : transform;
            return t.position;
        }

        private void SyncSpeedOfSoundToSolver()
        {
            sphSolver.SpeedOfSound = speedOfSound;
        }

        private void MaybeLogCflDiagnostic(
            float speedOfSound,
            float smoothingRadius,
            float dtCfl,
            int desiredSubsteps,
            int actualSubsteps,
            float actualSubDt,
            float cflRatio,
            float frameDeltaTime)
        {
            _cflLogAccumulator += frameDeltaTime;
            if (_cflLogAccumulator < 1f)
            {
                return;
            }

            _cflLogAccumulator = 0f;
            string status = cflRatio <= 1f ? "OK" : "UNSTABLE";
            LogSphTelemetry(
                $"[SPH CFL] {status} c={speedOfSound:F1} h={smoothingRadius:F4} dtCFL={dtCfl:F6}s "
                + $"desiredSubsteps={desiredSubsteps} substeps={actualSubsteps} actualSubDt={actualSubDt:F6}s "
                + $"cflRatio={cflRatio:F3} (must be <=1.0) cap={maxCflSubsteps}");
            if (cflRatio > 1f)
            {
                LogSphTelemetry(
                    $"[SPH CFL] CFL violated: raise maxCflSubsteps above {desiredSubsteps} or lower SpeedOfSound below {speedOfSound:F1}.",
                    warning: true);
            }
        }
    }
}
