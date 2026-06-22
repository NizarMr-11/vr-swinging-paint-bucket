using HarmonicEngine.Diagnostics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        public void SetContainerParticleMass(float mass) => containerFluid.particleMass = Mathf.Max(0f, mass);

        /// <summary>
        /// Enable/disable the world-space SPH container path. When enabled the pipeline confines
        /// particles to an open-top cylinder and runs full SPH on them in world space.
        /// </summary>
        public void SetContainerFluidEnabled(bool enabled)
        {
            containerFluid.enabled = enabled;
            if (enabled)
            {
                worldFallingOnly = false;
                useExternalParticleIngestion = true;
                seedTestParticlesOnStart = false;
                enableEulerianDrag = false;
                applyNonInertialPseudoForces = false;
                driveBucketFromTransform = false;
            }
        }

        /// <summary>
        /// Configure the world-space cylindrical container that confines the fluid.
        /// </summary>
        public void SetContainerFluid(
            Vector3 center,
            float radius,
            float floorY,
            float rimY,
            float restitution,
            float friction,
            float wallStiffness)
        {
            containerFluid.ApplyBounds(center, radius, floorY, rimY, restitution, friction, wallStiffness);
        }

        /// <summary>Stores a default spawn region used by the parameterless <see cref="SpawnVolume()"/>.</summary>
        public void SetSpawnRegion(HarmonicSpawnRegion region) => _defaultSpawnRegion = region;

        /// <summary>Samples the given region and appends its colored particles. Returns the appended count.</summary>
        public int SpawnVolume(HarmonicSpawnRegion region) => HarmonicParticleSpawner.Spawn(this, region);

        /// <summary>Spawns the region previously set via <see cref="SetSpawnRegion"/>.</summary>
        public int SpawnVolume() => HarmonicParticleSpawner.Spawn(this, _defaultSpawnRegion);

        /// <summary>Spawns every enabled <see cref="ParticleSpawnVolume"/> in the scene (priority order).</summary>
        public int SpawnAllParticleVolumes(bool clearFirst = true, bool activateSimulation = true)
        {
            int spawned = HarmonicParticleSpawnCoordinator.SpawnAll(this, clearFirst, activateSimulation);
            if (spawned > 0)
            {
                RecordRunSpawnInfo(new HarmonicRunSpawnInfo
                {
                    method = "volumes",
                    spawnCount = spawned
                });
            }

            return spawned;
        }

        /// <summary>Spawns a single scene volume using its configured count and flow flags.</summary>
        public int SpawnParticleVolume(ParticleSpawnVolume volume) =>
            volume != null ? volume.Emit() : 0;

        /// <summary>Configures the canvas hit surface (horizontal plane + culling mode).</summary>
        public void SetCanvasSurface(HarmonicCanvasSurface surface)
        {
            if (surface == null)
            {
                return;
            }

            SetCanvasPlaneY(surface.planeY);
            SetCanvasCullingEnabled(surface.cullIntoCanvas);
            canvasPaintAbsorbEnabled = surface.paintAbsorbEnabled;
            canvasAbsorbRate = surface.absorbRate;
            canvasAbsorbPaintWeightScale = surface.absorbPaintWeightScale;
        }

        /// <summary>Configures the bucket interaction volume (open-top cylinder + nozzle SDF).</summary>
        public void SetBucketVolume(HarmonicBucketVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            SetContainerFluid(
                volume.center, volume.radius, volume.floorY, volume.rimY,
                volume.restitution, volume.friction, volume.wallStiffness);
            SetNozzle(volume.nozzlePlaneLocalY, volume.nozzleRadius, volume.bucketRimLocalY);
        }

        /// <summary>Sets the local-space bucket nozzle exit SDF parameters.</summary>
        public void SetNozzle(float planeLocalY, float radius, float rimLocalY)
        {
            nozzlePlaneLocalY = planeLocalY;
            nozzleRadius = Mathf.Max(0f, radius);
            bucketRimLocalY = rimLocalY;
        }

        public void SetWorldFallingOnly(bool enabled)
        {
            worldFallingOnly = enabled;
            if (enabled)
            {
                seedTestParticlesOnStart = false;
                useExternalParticleIngestion = true;
                enableEulerianDrag = false;
                applyNonInertialPseudoForces = false;
                driveBucketFromTransform = false;
            }
        }

        public void SetCanvasPlaneY(float planeY) => canvasPlaneY = planeY;

        /// <summary>
        /// Toggle paint-canvas culling. When disabled the canvas plane acts as a solid floor
        /// and particles are kept alive instead of being consumed (so they stay visible).
        /// </summary>
        public void SetCanvasCullingEnabled(bool enabled) => canvasCullingEnabled = enabled;

        public void SetColorDiffusionRate(float rate) => colorDiffusionRate = Mathf.Max(0f, rate);

        public void SetFloorResponse(float restitution, float friction)
        {
            floorRestitution = Mathf.Clamp01(restitution);
            floorFriction = Mathf.Clamp01(friction);
        }

        public void SetEnableEulerianDrag(bool enable) => enableEulerianDrag = enable;

        public bool TryCopyIndirectDispatchArgs(int[] destination)
        {
            if (_indirectArgsBuffer == null || destination == null || destination.Length < 3)
            {
                return false;
            }

            _indirectArgsBuffer.GetData(destination);
            return true;
        }

        /// <summary>
        /// Exposes the spatial-hash buffers built each frame (sorted keys + cell ranges).
        /// Intended for tests and diagnostics; read after <see cref="ExecutePipelineFrame"/>.
        /// </summary>
        public bool TryGetSpatialHashBuffers(out ComputeBuffer sortedGridKeys, out ComputeBuffer cellRanges, out int frameSortSize)
        {
            sortedGridKeys = _gridKeyValueBuffer;
            cellRanges = _cellStartEndBuffer;
            frameSortSize = _frameSortSize;
            return sortedGridKeys != null && cellRanges != null;
        }

        public void ApplyQualityTier(HarmonicQualityTier tier)
        {
            qualityTier = tier;
            int targetCapacity = HarmonicQualityPresets.GetParticleCapacity(tier);
            if (targetCapacity == maxCapacity)
            {
                return;
            }

            maxCapacity = targetCapacity;
            ReleaseBuffers();
            InitializeBuffers();
            CacheKernels();
        }

        public bool TryGetCanvasHitBuffer(out ComputeBuffer buffer, out uint count)
        {
            buffer = _bufferCanvasHits;
            count = _lastCanvasHitCount;
            return buffer != null;
        }

        public void SetBucketTransform(Transform transform) => bucketTransform = transform;

        public void SetBucketKinematicProvider(MonoBehaviour provider) => bucketKinematicProvider = provider;

        public bool TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count)
        {
            buffer = _pingPong?.ReadBuffer;
            count = _cachedInternalCount;
            return buffer != null;
        }

        public bool TryGetFallingParticleBuffer(out ComputeBuffer buffer, out uint count)
        {
            if (worldFallingOnly && _pingPong != null)
            {
                buffer = _pingPong.ReadBuffer;
                count = _cachedInternalCount;
                return buffer != null;
            }

            buffer = _bufferFallingWorld ?? _bufferFalling;
            count = _lastFallingDebugCount;
            return buffer != null;
        }

        public bool TryGetDensityCacheBuffer(out ComputeBuffer buffer, out uint count)
        {
            buffer = _bufferDensityCache;
            count = _cachedInternalCount;
            return buffer != null;
        }

        public void SetSimulationActive(bool active)
        {
            simulationActive = active;
            PublishDiagnostic(
                HarmonicDiagnosticEventType.SimulationStateChanged,
                "PIPELINE",
                active ? "active" : "inactive",
                boolArg0: active);
        }

        public void EnableExternalIngestion(bool enabled)
        {
            useExternalParticleIngestion = enabled;
            if (enabled)
            {
                seedTestParticlesOnStart = false;
            }
        }

        public void ApplyDiagnosticsSettings(HarmonicPipelineDiagnosticsSettings settings)
        {
            verbosePipelineDiagnostics = settings.verbosePipelineDiagnostics;
            frameDiagnosticInterval = settings.frameDiagnosticInterval;
            positionSampleInterval = settings.positionSampleInterval;
            positionSampleCount = settings.positionSampleCount;
            logStencilNeighborCount = settings.logStencilNeighborCount;
            perfDiagnosticsMuted = settings.perfDiagnosticsMuted;
            logSphToConsole = settings.logSphToConsole;
            muteSphTelemetry = settings.muteSphTelemetry;
        }

        /// <summary>When <see cref="useLatticeSpawn"/> is enabled, fills the container cylinder with a uniform lattice.</summary>
        public int TrySpawnContainerLatticeFill() => SpawnContainerLatticeFillInternal();

        public bool UseLatticeSpawn => useLatticeSpawn;
    }
}
