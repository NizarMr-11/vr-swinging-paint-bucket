using System.Collections.Generic;

namespace HarmonicEngine.Core.Validation
{
    public static class ArchitectureManifest
    {
        public const string DocumentVersion = "V3.1-Final";

        public static readonly IReadOnlyList<string> RequiredDomainModels = new[]
        {
            "FluidParticle",
            "QuantizedBakeParticle",
            "GridKeyPair",
            "HashCellGridRange",
            "VoxelDragCell",
            "CanvasPaintHit"
        };

        public static readonly IReadOnlyList<string> RequiredComputeKernels = new[]
        {
            "CalculateGridArgsKernel",
            "ClearGridCellsKernel",
            "GenerateGridKeysKernel",
            "BitonicSortStepKernel",
            "BuildCellRangesKernel",
            "ExecuteSphDensityPass",
            "ExecuteInternalFluidIntegration",
            "ExecuteFallingFluidIntegration",
            "ClearDragGridKernel",
            "AdvectDragGridKernel",
            "ScatterParticleToGridKernel",
            "ApplyDragFromGridKernel",
            "QuantizeFallingParticlesKernel"
        };

        public static readonly IReadOnlyList<string> RequiredBlueprintAssets = new[]
        {
            "Rk4SystemSolver.cs",
            "PendulumRk4Integrator.cs",
            "PendulumRk4Job.cs",
            "ImpastoCanvasDisplace.shader",
            "ParticleDebugPoints.shader",
            "IUniversalPhysicsSolver.cs",
            "HarmonicBakeRecorder.cs",
            "HarmonicBakePlaybackDriver.cs",
            "HarmonicCanvasHitBridge.cs",
            "QuantizedFrameEncoder.cs",
            "QuantizedBakeDecoder.cs",
            "HarmonicParticleDebugRenderer.cs",
            "HarmonicSimulationControls.cs",
            "HarmonicGpuCapabilityGuard.cs"
        };

        public static readonly IReadOnlyDictionary<string, ArchitectureFeatureStatus> FeatureMatrix =
            new Dictionary<string, ArchitectureFeatureStatus>
            {
                ["PingPongBufferLayout"] = ArchitectureFeatureStatus.Implemented,
                ["UnifiedGpuIndirectExecution"] = ArchitectureFeatureStatus.Implemented,
                ["MultiPassSphDeconstruction"] = ArchitectureFeatureStatus.Implemented,
                ["QuantizedVramCache"] = ArchitectureFeatureStatus.Implemented,
                ["SixStageGpuPipeline"] = ArchitectureFeatureStatus.Implemented,
                ["SpatialHash27CellNeighbors"] = ArchitectureFeatureStatus.Implemented,
                ["StreamCompactionAppendBuffers"] = ArchitectureFeatureStatus.Partial,
                ["EulerianVoxelDragField"] = ArchitectureFeatureStatus.Implemented,
                ["BakePlaybackImpastoReplay"] = ArchitectureFeatureStatus.Implemented,
                ["FallingFluidWorldPass"] = ArchitectureFeatureStatus.Implemented,
                ["GpuParticleDebugRenderer"] = ArchitectureFeatureStatus.Implemented,
                ["AsyncQuantizedBakeReadback"] = ArchitectureFeatureStatus.Implemented,
                ["CanvasGpuHitPipeline"] = ArchitectureFeatureStatus.Implemented,
                ["ImpastoCanvasIntegration"] = ArchitectureFeatureStatus.Implemented,
                ["ImpastoCanvasShader"] = ArchitectureFeatureStatus.Implemented,
                ["Rk4BurstIntegrator"] = ArchitectureFeatureStatus.Implemented,
                ["HarmonicQualityPresets"] = ArchitectureFeatureStatus.Implemented,
                ["HarmonicSimulationModes"] = ArchitectureFeatureStatus.Implemented,
                ["GpuCapabilityFallback"] = ArchitectureFeatureStatus.Implemented,
                ["IUniversalPhysicsSolver"] = ArchitectureFeatureStatus.Implemented,
                ["AsyncCompressedDiskWriter"] = ArchitectureFeatureStatus.Implemented,
                ["SlidingWindowPlaybackThread"] = ArchitectureFeatureStatus.Implemented,
                ["FullNozzleSdf"] = ArchitectureFeatureStatus.Implemented,
                ["ParticleEmitterGpuBridge"] = ArchitectureFeatureStatus.Implemented,
                ["SimulationManagerHarmonicWiring"] = ArchitectureFeatureStatus.Implemented,
                ["NonInertialBucketPseudoForces"] = ArchitectureFeatureStatus.Implemented,
                ["FiveMillionParticleCapacity"] = ArchitectureFeatureStatus.Partial
            };
    }

    public enum ArchitectureFeatureStatus
    {
        NotImplemented = 0,
        InProgress = 1,
        Partial = 2,
        Implemented = 3
    }
}
