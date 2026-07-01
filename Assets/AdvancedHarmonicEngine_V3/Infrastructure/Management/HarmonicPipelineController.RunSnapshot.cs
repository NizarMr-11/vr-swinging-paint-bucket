using HarmonicEngine.Diagnostics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        private HarmonicRunSpawnInfo _lastRunSpawnInfo;

        public HarmonicRunSpawnInfo LastRunSpawnInfo => _lastRunSpawnInfo;

        public OpenTopCylinderSettings ReadContainerFluid()
        {
            return JsonUtility.FromJson<OpenTopCylinderSettings>(JsonUtility.ToJson(openTopCylinder));
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
                gasConstantK = openTopCylinder.enabled ? openTopCylinder.gasConstantK : sphSolver.GasConstantK,
                viscosity = openTopCylinder.enabled ? openTopCylinder.viscosity : sphSolver.Viscosity,
                velocityDamping = openTopCylinder.velocityDamping,
                maxSpeed = openTopCylinder.maxSpeed,
                substeps = openTopCylinder.substeps,
                maxTimeStep = openTopCylinder.maxTimeStep,
                maxCflSubsteps = maxCflSubsteps,
                colorDiffusionRate = colorDiffusionRate,
                particleMass = ResolveContainerParticleMass()
            };
        }

        public HarmonicPbfTuningSnapshot ReadPbfTuning()
        {
            float h = SmoothingRadius;
            return new HarmonicPbfTuningSnapshot
            {
                openTopCylinderUsePbf = openTopCylinderUsePbf,
                pbfIterations = pbfIterations,
                epsilon = ResolvePbfEpsilon(h),
                pbfEpsilonScale = pbfEpsilonScale,
                pbfRelaxation = pbfRelaxation,
                pbfVelocityDamping = pbfVelocityDamping,
                pbfMaxPositionDelta = pbfMaxPositionDelta,
                pbfCohesion = pbfCohesion,
                smoothingRadius = h,
                restDensity = sphSolver.RestDensity
            };
        }

        public HarmonicRuntimeTuningSnapshot ReadRuntimeSnapshot()
        {
            HarmonicSphTuningSnapshot sph = ReadSphTuning();
            HarmonicPbfTuningSnapshot pbf = ReadPbfTuning();
            HarmonicSimulationInitSnapshot init = ReadSimulationInitSnapshot();
            return new HarmonicRuntimeTuningSnapshot
            {
                cellSize = sph.cellSize,
                latticeSpacing = LatticeSpacing,
                latticeSpacingScale = latticeSpacingScale,
                latticeSpawnMaxCount = latticeSpawnMaxCount,
                smoothingRadius = sph.smoothingRadius,
                restDensity = sph.restDensity,
                particleMass = sph.particleMass,
                particleMassOverride = openTopCylinder.particleMass,
                speedOfSound = sph.speedOfSound,
                gasConstantK = sph.gasConstantK,
                viscosity = sph.viscosity,
                velocityDamping = sph.velocityDamping,
                maxSpeed = sph.maxSpeed,
                substeps = sph.substeps,
                maxTimeStep = sph.maxTimeStep,
                maxCflSubsteps = sph.maxCflSubsteps,
                colorDiffusionRate = sph.colorDiffusionRate,
                openTopCylinderUsePbf = pbf.openTopCylinderUsePbf,
                pbfIterations = pbf.pbfIterations,
                epsilon = pbf.epsilon,
                pbfEpsilonScale = pbf.pbfEpsilonScale,
                pbfRelaxation = pbf.pbfRelaxation,
                pbfVelocityDamping = pbf.pbfVelocityDamping,
                pbfMaxPositionDelta = pbf.pbfMaxPositionDelta,
                pbfCohesion = pbf.pbfCohesion,
                dynamicSortSizing = init.dynamicSortSizing,
                minSortSize = init.minSortSize,
                transferExteriorParticlesToFalling = transferExteriorParticlesToFalling
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
                containerFluidEnabled = openTopCylinder.enabled,
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
