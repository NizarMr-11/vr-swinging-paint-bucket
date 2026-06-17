using HarmonicEngine.Core.Validation;
using NUnit.Framework;
using System.Runtime.InteropServices;
using HarmonicEngine.Domain.Models;

namespace HarmonicEngine.Tests
{
    public class CanvasPaintHitTests
    {
        [Test]
        public void CanvasPaintHit_Size_Is16Bytes()
        {
            Assert.AreEqual(16, Marshal.SizeOf<CanvasPaintHit>());
        }

        [Test]
        public void FeatureMatrix_CanvasAndVrFeatures_AreImplemented()
        {
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["CanvasGpuHitPipeline"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["ImpastoCanvasIntegration"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["Rk4BurstIntegrator"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["HarmonicQualityPresets"]);
            Assert.AreEqual(ArchitectureFeatureStatus.Implemented, ArchitectureManifest.FeatureMatrix["EulerianVoxelDragField"]);
        }
    }
}
