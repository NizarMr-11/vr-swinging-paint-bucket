using HarmonicEngine.Diagnostics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private HarmonicRunSpawnInfo _lastRunSpawnInfo;

        public HarmonicRunSpawnInfo LastRunSpawnInfo => _lastRunSpawnInfo;

        public ContainerFluidSettings ReadContainerFluid()
        {
            return JsonUtility.FromJson<ContainerFluidSettings>(JsonUtility.ToJson(containerFluid));
        }

        public HarmonicBucketNozzleSnapshot ReadBucketNozzle()
        {
            Vector3 bucketPosition = bucketTransform != null ? bucketTransform.position : Vector3.zero;
            return new HarmonicBucketNozzleSnapshot
            {
                driveBucketFromTransform = driveBucketFromTransform,
                bucketTransformName = bucketTransform != null ? bucketTransform.name : string.Empty,
                bucketPositionX = bucketPosition.x,
                bucketPositionY = bucketPosition.y,
                bucketPositionZ = bucketPosition.z,
                nozzlePlaneLocalY = nozzlePlaneLocalY,
                nozzleRadius = nozzleRadius,
                bucketRimLocalY = bucketRimLocalY
            };
        }

        public HarmonicSphTuningSnapshot ReadSphTuning()
        {
            return new HarmonicSphTuningSnapshot
            {
                cellSize = cellSize,
                smoothingRadius = SmoothingRadius,
                speedOfSound = speedOfSound,
                restDensity = sphSolver.RestDensity,
                gasConstantK = containerFluid.enabled ? containerFluid.gasConstantK : sphSolver.GasConstantK,
                viscosity = containerFluid.enabled ? containerFluid.viscosity : sphSolver.Viscosity,
                velocityDamping = containerFluid.velocityDamping,
                maxSpeed = containerFluid.maxSpeed,
                substeps = containerFluid.substeps,
                maxTimeStep = containerFluid.maxTimeStep,
                maxCflSubsteps = maxCflSubsteps,
                colorDiffusionRate = colorDiffusionRate,
                particleMass = ResolveContainerParticleMass()
            };
        }

        public HarmonicSimulationInitSnapshot ReadSimulationInitSnapshot()
        {
            return new HarmonicSimulationInitSnapshot
            {
                simulationMode = simulationMode,
                qualityTier = qualityTier,
                simulationActive = simulationActive,
                worldFallingOnly = worldFallingOnly,
                containerFluidEnabled = containerFluid.enabled,
                useExternalIngestion = useExternalParticleIngestion,
                autoRunPipeline = autoRunPipeline,
                gravity = gravity,
                worldDrag = worldDrag,
                canvasPlaneY = canvasPlaneY,
                canvasCullingEnabled = canvasCullingEnabled,
                dynamicSortSizing = dynamicSortSizing,
                minSortSize = minSortSize,
                useLatticeSpawn = useLatticeSpawn,
                seedTestParticlesOnStart = seedTestParticlesOnStart,
                testParticleCount = testParticleCount,
                testSpawnRadius = testSpawnRadius
            };
        }

        internal void RecordRunSpawnInfo(HarmonicRunSpawnInfo spawnInfo)
        {
            _lastRunSpawnInfo = spawnInfo;
        }
    }
}
