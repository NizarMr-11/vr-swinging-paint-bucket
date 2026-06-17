using HarmonicEngine.Core.Validation;
using NUnit.Framework;
using System.Linq;

namespace HarmonicEngine.Tests
{
    public class ArchitectureManifestTests
    {
        [Test]
        public void DocumentVersion_IsV31Final()
        {
            Assert.AreEqual("V3.1-Final", ArchitectureManifest.DocumentVersion);
        }

        [Test]
        public void RequiredDomainModels_ContainsAllCoreTypes()
        {
            CollectionAssert.Contains(ArchitectureManifest.RequiredDomainModels, "FluidParticle");
            CollectionAssert.Contains(ArchitectureManifest.RequiredDomainModels, "QuantizedBakeParticle");
            CollectionAssert.Contains(ArchitectureManifest.RequiredDomainModels, "GridKeyPair");
            CollectionAssert.Contains(ArchitectureManifest.RequiredDomainModels, "HashCellGridRange");
            CollectionAssert.Contains(ArchitectureManifest.RequiredDomainModels, "VoxelDragCell");
        }

        [Test]
        public void RequiredComputeKernels_ContainsSixStagePipelineKernels()
        {
            string[] kernels = ArchitectureManifest.RequiredComputeKernels.ToArray();
            Assert.Contains("CalculateGridArgsKernel", kernels);
            Assert.Contains("ClearGridCellsKernel", kernels);
            Assert.Contains("GenerateGridKeysKernel", kernels);
            Assert.Contains("BitonicSortStepKernel", kernels);
            Assert.Contains("BuildCellRangesKernel", kernels);
            Assert.Contains("ExecuteSphDensityPass", kernels);
            Assert.Contains("ExecuteInternalFluidIntegration", kernels);
            Assert.Contains("ExecuteFallingFluidIntegration", kernels);
            Assert.Contains("QuantizeFallingParticlesKernel", kernels);
        }

        [Test]
        public void FeatureMatrix_CorePillars_AreImplementedOrPartial()
        {
            Assert.GreaterOrEqual(
                (int)ArchitectureManifest.FeatureMatrix["PingPongBufferLayout"],
                (int)ArchitectureFeatureStatus.Partial);

            Assert.GreaterOrEqual(
                (int)ArchitectureManifest.FeatureMatrix["UnifiedGpuIndirectExecution"],
                (int)ArchitectureFeatureStatus.Partial);

            Assert.GreaterOrEqual(
                (int)ArchitectureManifest.FeatureMatrix["MultiPassSphDeconstruction"],
                (int)ArchitectureFeatureStatus.Partial);

            Assert.GreaterOrEqual(
                (int)ArchitectureManifest.FeatureMatrix["SixStageGpuPipeline"],
                (int)ArchitectureFeatureStatus.Partial);
        }

        [Test]
        public void FeatureMatrix_EulerianAndRk4_AreImplemented()
        {
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["EulerianVoxelDragField"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["Rk4BurstIntegrator"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["BakePlaybackImpastoReplay"]);
        }

        [Test]
        public void FeatureMatrix_ReportsImplementedBlueprintItems()
        {
            Assert.AreEqual(
                ArchitectureFeatureStatus.Implemented,
                ArchitectureManifest.FeatureMatrix["ImpastoCanvasShader"]);

            Assert.AreEqual(
                ArchitectureFeatureStatus.Implemented,
                ArchitectureManifest.FeatureMatrix["SimulationManagerHarmonicWiring"]);
        }
    }
}
