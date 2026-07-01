using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    [System.Serializable]
    public sealed class HarmonicRunSpawnInfo
    {
        public string method = "none";
        public int spawnCount;
        public float spacing;
        public float fillTopY;
        public float spawnRadius;
        public float initialVelocityX;
        public float initialVelocityY;
        public float initialVelocityZ;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestEnvironment
    {
        public string unityVersion;
        public string platform;
        public string scene;
        public string gpu;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestBucket
    {
        public string sceneContainerName;
        public float centerX;
        public float centerY;
        public float centerZ;
        public float radius;
        public float floorY;
        public float rimY;
        public float height;
        public float restitution;
        public float friction;
        public float wallStiffness;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestBucketNozzle
    {
        public bool driveBucketFromTransform;
        public string bucketTransformName;
        public float bucketPositionX;
        public float bucketPositionY;
        public float bucketPositionZ;
        public float nozzlePlaneLocalY;
        public float nozzleRadius;
        public float bucketRimLocalY;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestRuntime
    {
        public float cellSize;
        public float latticeSpacing;
        public float latticeSpacingScale;
        public int latticeSpawnMaxCount;
        public float smoothingRadius;
        public float restDensity;
        public float particleMass;
        public float particleMassOverride;
        public float speedOfSound;
        public float gasConstantK;
        public float viscosity;
        public float velocityDamping;
        public float maxSpeed;
        public int substeps;
        public float maxTimeStep;
        public int maxCflSubsteps;
        public float colorDiffusionRate;
        public bool openTopCylinderUsePbf;
        public int pbfIterations;
        public float epsilon;
        public float pbfEpsilonScale;
        public float pbfRelaxation;
        public float pbfVelocityDamping;
        public float pbfMaxPositionDelta;
        public float pbfCohesion;
        public bool dynamicSortSizing;
        public int minSortSize;
        public bool transferExteriorParticlesToFalling;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestSimulation
    {
        public string simulationMode;
        public string qualityTier;
        public bool simulationActive;
        public bool worldFallingOnly;
        public bool containerFluidEnabled;
        public bool useExternalIngestion;
        public bool autoRunPipeline;
        public float gravityX;
        public float gravityY;
        public float gravityZ;
        public float worldDrag;
        public float canvasPlaneY;
        public bool canvasCullingEnabled;
        public bool dynamicSortSizing;
        public int minSortSize;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestParticles
    {
        public int maxCapacity;
        public int activeCountAtInit;
        public int activeCountAtEnd;
        public string spawnMethod;
        public int spawnCount;
        public float spacing;
        public float fillTopY;
        public float spawnRadius;
        public float initialVelocityX;
        public float initialVelocityY;
        public float initialVelocityZ;
        public float restDensity;
    }

    [System.Serializable]
    public sealed class HarmonicRunManifestInitConditions
    {
        public bool useLatticeSpawn;
        public bool seedTestParticlesOnStart;
        public bool spawnLatticeOnStart;
        public int testParticleCount;
        public float testSpawnRadius;
    }

    public static class HarmonicRunManifestSnapshotBuilder
    {
        public static HarmonicRunManifestEnvironment BuildEnvironment()
        {
            return new HarmonicRunManifestEnvironment
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                gpu = SystemInfo.graphicsDeviceName
            };
        }

        public static HarmonicRunManifestBucket BuildBucket(
            HarmonicPipelineController pipeline,
            string sceneContainerName)
        {
            OpenTopCylinderSettings container = pipeline.ReadContainerFluid();
            return new HarmonicRunManifestBucket
            {
                sceneContainerName = sceneContainerName ?? string.Empty,
                centerX = container.center.x,
                centerY = container.center.y,
                centerZ = container.center.z,
                radius = container.radius,
                floorY = container.floorY,
                rimY = container.rimY,
                height = container.rimY - container.floorY,
                restitution = container.restitution,
                friction = container.friction,
                wallStiffness = container.wallStiffness
            };
        }

        public static HarmonicRunManifestBucketNozzle BuildBucketNozzle(HarmonicPipelineController pipeline)
        {
            HarmonicBucketNozzleSnapshot nozzle = pipeline.ReadBucketNozzle();
            return new HarmonicRunManifestBucketNozzle
            {
                driveBucketFromTransform = nozzle.driveBucketFromTransform,
                bucketTransformName = nozzle.bucketTransformName ?? string.Empty,
                bucketPositionX = nozzle.bucketPositionX,
                bucketPositionY = nozzle.bucketPositionY,
                bucketPositionZ = nozzle.bucketPositionZ,
                nozzlePlaneLocalY = nozzle.nozzlePlaneLocalY,
                nozzleRadius = nozzle.nozzleRadius,
                bucketRimLocalY = nozzle.bucketRimLocalY
            };
        }

        public static HarmonicRunManifestRuntime BuildRuntime(HarmonicPipelineController pipeline)
        {
            HarmonicRuntimeTuningSnapshot tuning = pipeline.ReadRuntimeSnapshot();
            return new HarmonicRunManifestRuntime
            {
                cellSize = tuning.cellSize,
                latticeSpacing = tuning.latticeSpacing,
                latticeSpacingScale = tuning.latticeSpacingScale,
                latticeSpawnMaxCount = tuning.latticeSpawnMaxCount,
                smoothingRadius = tuning.smoothingRadius,
                restDensity = tuning.restDensity,
                particleMass = tuning.particleMass,
                particleMassOverride = tuning.particleMassOverride,
                speedOfSound = tuning.speedOfSound,
                gasConstantK = tuning.gasConstantK,
                viscosity = tuning.viscosity,
                velocityDamping = tuning.velocityDamping,
                maxSpeed = tuning.maxSpeed,
                substeps = tuning.substeps,
                maxTimeStep = tuning.maxTimeStep,
                maxCflSubsteps = tuning.maxCflSubsteps,
                colorDiffusionRate = tuning.colorDiffusionRate,
                openTopCylinderUsePbf = tuning.openTopCylinderUsePbf,
                pbfIterations = tuning.pbfIterations,
                epsilon = tuning.epsilon,
                pbfEpsilonScale = tuning.pbfEpsilonScale,
                pbfRelaxation = tuning.pbfRelaxation,
                pbfVelocityDamping = tuning.pbfVelocityDamping,
                pbfMaxPositionDelta = tuning.pbfMaxPositionDelta,
                pbfCohesion = tuning.pbfCohesion,
                dynamicSortSizing = tuning.dynamicSortSizing,
                minSortSize = tuning.minSortSize,
                transferExteriorParticlesToFalling = tuning.transferExteriorParticlesToFalling
            };
        }

        public static HarmonicRunManifestSimulation BuildSimulation(HarmonicPipelineController pipeline)
        {
            HarmonicSimulationInitSnapshot init = pipeline.ReadSimulationInitSnapshot();
            return new HarmonicRunManifestSimulation
            {
                simulationMode = init.simulationMode.ToString(),
                qualityTier = init.qualityTier.ToString(),
                simulationActive = init.simulationActive,
                worldFallingOnly = init.worldFallingOnly,
                containerFluidEnabled = init.containerFluidEnabled,
                useExternalIngestion = init.useExternalIngestion,
                autoRunPipeline = init.autoRunPipeline,
                gravityX = init.gravity.x,
                gravityY = init.gravity.y,
                gravityZ = init.gravity.z,
                worldDrag = init.worldDrag,
                canvasPlaneY = init.canvasPlaneY,
                canvasCullingEnabled = init.canvasCullingEnabled,
                dynamicSortSizing = init.dynamicSortSizing,
                minSortSize = init.minSortSize
            };
        }

        public static HarmonicRunManifestParticles BuildParticles(
            HarmonicPipelineController pipeline,
            HarmonicRunSpawnInfo spawn)
        {
            spawn ??= pipeline.LastRunSpawnInfo ?? new HarmonicRunSpawnInfo();
            int activeCount = (int)pipeline.GetActiveParticleCount();
            if (spawn.spawnCount <= 0 && activeCount > 0 && spawn.method == "none")
            {
                spawn.method = "unknown";
                spawn.spawnCount = activeCount;
            }

            return new HarmonicRunManifestParticles
            {
                maxCapacity = pipeline.MaxCapacity,
                activeCountAtInit = activeCount,
                spawnMethod = spawn.method,
                spawnCount = spawn.spawnCount,
                spacing = spawn.spacing,
                fillTopY = spawn.fillTopY,
                spawnRadius = spawn.spawnRadius,
                initialVelocityX = spawn.initialVelocityX,
                initialVelocityY = spawn.initialVelocityY,
                initialVelocityZ = spawn.initialVelocityZ,
                restDensity = pipeline.RestDensity
            };
        }

        public static HarmonicRunManifestInitConditions BuildInitConditions(
            HarmonicPipelineController pipeline,
            bool spawnLatticeOnStart)
        {
            HarmonicSimulationInitSnapshot init = pipeline.ReadSimulationInitSnapshot();
            return new HarmonicRunManifestInitConditions
            {
                useLatticeSpawn = init.useLatticeSpawn,
                seedTestParticlesOnStart = init.seedTestParticlesOnStart,
                spawnLatticeOnStart = spawnLatticeOnStart,
                testParticleCount = init.testParticleCount,
                testSpawnRadius = init.testSpawnRadius
            };
        }
    }
}
