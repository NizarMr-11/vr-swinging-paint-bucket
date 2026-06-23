using HarmonicEngine.Diagnostics;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class PbfDiagnosticsTests
    {
        [Test]
        public void HarmonicLogRouter_RoutesPbfCategoryToPbfLog()
        {
            var categoryEvent = new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "PBF",
                "constraint telemetry",
                0,
                0f);
            Assert.AreEqual(HarmonicLogChannel.Pbf, HarmonicLogRouter.Route(categoryEvent));

            var markerEvent = new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "ENGINE",
                "[PBF FRAME] active=3000 substeps=1",
                1,
                0.1f);
            Assert.AreEqual(HarmonicLogChannel.Pbf, HarmonicLogRouter.Route(markerEvent));
        }

        [Test]
        public void HarmonicRunManifestPbf_BuildsFromPipeline()
        {
            var go = new GameObject("pbf-manifest-pipeline");
            var pipeline = go.AddComponent<PipelineExecutionController>();
            pipeline.SetCellSize(0.05f);
            pipeline.SetUsePbf(true);

            HarmonicPbfTuningSnapshot tuning = pipeline.ReadPbfTuning();
            Assert.IsTrue(tuning.usePBF);
            Assert.AreEqual(3, tuning.pbfIterations);
            Assert.AreEqual(1000f, tuning.restDensity, 1e-3f);
            Assert.AreEqual(0.05f, tuning.smoothingRadius, 1e-4f);
            Assert.AreEqual(600f / (0.05f * 0.05f), tuning.epsilon, 1f);

            HarmonicRunManifestPbf manifestPbf = HarmonicRunManifestSnapshotBuilder.BuildPbf(pipeline);
            Assert.IsTrue(manifestPbf.usePBF);
            Assert.AreEqual(3, manifestPbf.pbfIterations);
            Assert.AreEqual(tuning.epsilon, manifestPbf.epsilon, 1f);

            string tempRoot = Path.Combine(Path.GetTempPath(), "PbfDiagnosticsTests_" + System.Guid.NewGuid().ToString("N"));
            string runDirectory = Path.Combine(tempRoot, "run_pbf");
            Directory.CreateDirectory(runDirectory);

            try
            {
                HarmonicDiagnosticHub.Enabled = true;
                HarmonicDiagnosticHub.Initialize(
                    pipeline,
                    HarmonicPipelineDiagnosticsSettings.CreateDefault(),
                    forceReset: true,
                    runDirectoryOverride: runDirectory);
                HarmonicDiagnosticHub.RefreshManifestInit(sceneContainerName: "PbfContainer");
                HarmonicDiagnosticHub.Shutdown();

                string json = File.ReadAllText(Path.Combine(runDirectory, HarmonicRunManifest.FileName));
                StringAssert.Contains("\"pbf\"", json);
                StringAssert.Contains("\"usePBF\": true", json);
                StringAssert.Contains("\"pbfIterations\": 3", json);
                StringAssert.Contains("pbf.log", json);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }

                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputePbfConstraintStats_ComputesExpectedRange()
        {
            float[] densities = { 1000f, 1100f, 900f };
            GpuParticleReadbackUtility.ScalarStats stats =
                GpuParticleReadbackUtility.ComputePbfConstraintStats(densities, 1000f);

            Assert.AreEqual(-0.1f, stats.Min, 1e-4f);
            Assert.AreEqual(0.1f, stats.Max, 1e-4f);
            Assert.AreEqual(0f, stats.Avg, 1e-4f);
        }
    }
}
