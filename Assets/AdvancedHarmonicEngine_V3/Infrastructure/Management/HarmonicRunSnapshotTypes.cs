using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public struct HarmonicBucketNozzleSnapshot
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

    public struct HarmonicSphTuningSnapshot
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

    public struct HarmonicPbfTuningSnapshot
    {
        public bool usePBF;
        public int pbfIterations;
        public float epsilon;
        public float pbfEpsilonScale;
        public float pbfRelaxation;
        public float pbfVelocityDamping;
        public float pbfMaxPositionDelta;
        public float pbfCohesion;
        public float smoothingRadius;
        public float restDensity;
    }

    /// <summary>GPU solver and pipeline tuning captured in run manifest.json.</summary>
    public struct HarmonicRuntimeTuningSnapshot
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
        public bool usePBF;
        public int pbfIterations;
        public float epsilon;
        public float pbfEpsilonScale;
        public float pbfRelaxation;
        public float pbfVelocityDamping;
        public float pbfMaxPositionDelta;
        public float pbfCohesion;
        public bool dynamicSortSizing;
        public int minSortSize;
        public bool spillOverRim;
    }

    public struct HarmonicSimulationInitSnapshot
    {
        public HarmonicSimulationMode simulationMode;
        public HarmonicQualityTier qualityTier;
        public bool simulationActive;
        public bool worldFallingOnly;
        public bool containerFluidEnabled;
        public bool useExternalIngestion;
        public bool autoRunPipeline;
        public Vector3 gravity;
        public float worldDrag;
        public float canvasPlaneY;
        public bool canvasCullingEnabled;
        public bool dynamicSortSizing;
        public int minSortSize;
        public bool useLatticeSpawn;
        public bool seedTestParticlesOnStart;
        public int testParticleCount;
        public float testSpawnRadius;
    }
}
