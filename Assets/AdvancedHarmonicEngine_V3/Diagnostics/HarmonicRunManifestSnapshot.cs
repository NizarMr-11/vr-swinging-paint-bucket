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
    public sealed class HarmonicRunManifestSph
    {
        public float cellSize;
        public float smoothingRadius;
        public float speedOfSound;
        public float restDensity;
        public float gasConstantK;
        public float viscosity;
        public float velocityDamping;
        public float maxSpeed;
        public int substeps;
        public float maxTimeStep;
        public int maxCflSubsteps;
        public float colorDiffusionRate;
        public float particleMass;
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
            PipelineExecutionController pipeline,
            string sceneContainerName)
        {
            ContainerFluidSettings container = pipeline.ReadContainerFluid();
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

        public static HarmonicRunManifestBucketNozzle BuildBucketNozzle(PipelineExecutionController pipeline)
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

        public static HarmonicRunManifestSph BuildSph(PipelineExecutionController pipeline)
        {
            HarmonicSphTuningSnapshot tuning = pipeline.ReadSphTuning();
            return new HarmonicRunManifestSph
            {
                cellSize = tuning.cellSize,
                smoothingRadius = tuning.smoothingRadius,
                speedOfSound = tuning.speedOfSound,
                restDensity = tuning.restDensity,
                gasConstantK = tuning.gasConstantK,
                viscosity = tuning.viscosity,
                velocityDamping = tuning.velocityDamping,
                maxSpeed = tuning.maxSpeed,
                substeps = tuning.substeps,
                maxTimeStep = tuning.maxTimeStep,
                maxCflSubsteps = tuning.maxCflSubsteps,
                colorDiffusionRate = tuning.colorDiffusionRate,
                particleMass = tuning.particleMass
            };
        }

        public static HarmonicRunManifestSimulation BuildSimulation(PipelineExecutionController pipeline)
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
            PipelineExecutionController pipeline,
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
            PipelineExecutionController pipeline,
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
