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
        public void ResolveContainerParticleMass_UsesLatticeSpacing()
        {
            var go = new GameObject("mass-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetCellSize(0.05f);

            float mass = pipeline.ReadSphTuning().particleMass;
            float expected = 1000f * Mathf.Pow(0.025f, 3f);
            Assert.AreEqual(0.015625f, expected, 1e-6f);
            Assert.AreEqual(expected, mass, 1e-6f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LatticeSpacing_DefaultScale_IsHalfCellSize()
        {
            var go = new GameObject("lattice-spacing-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetCellSize(0.05f);

            Assert.AreEqual(0.025f, pipeline.LatticeSpacing, 1e-6f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResolvePbfEpsilon_AtH05_IsOrderOfMagnitude960()
        {
            var go = new GameObject("pbf-epsilon-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetCellSize(0.05f);

            float epsilon = pipeline.ReadPbfTuning().epsilon;
            float expected = 0.006f / Mathf.Pow(0.05f, 4f);
            Assert.AreEqual(expected, epsilon, 1f);
            Assert.Less(epsilon, 2000f);
            Assert.Greater(epsilon, 100f);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void HarmonicRunManifestRuntime_BuildsFromPipeline()
        {
            var go = new GameObject("runtime-manifest-pipeline");
            var pipeline = go.AddComponent<HarmonicPipelineController>();
            pipeline.SetCellSize(0.05f);
            pipeline.SetUsePbf(true);

            HarmonicRuntimeTuningSnapshot tuning = pipeline.ReadRuntimeSnapshot();
            Assert.IsTrue(tuning.openTopCylinderUsePbf);
            Assert.AreEqual(3, tuning.pbfIterations);
            Assert.AreEqual(1000f, tuning.restDensity, 1e-3f);
            Assert.AreEqual(0.05f, tuning.cellSize, 1e-4f);
            Assert.AreEqual(0.025f, tuning.latticeSpacing, 1e-6f);
            Assert.AreEqual(0.015625f, tuning.particleMass, 1e-6f);
            Assert.AreEqual(0.006f, tuning.pbfEpsilonScale, 1e-6f);
            Assert.AreEqual(1f, tuning.pbfRelaxation, 1e-3f);
            Assert.AreEqual(0.015f, tuning.pbfMaxPositionDelta, 1e-6f);
            Assert.AreEqual(0.3f, tuning.pbfCohesion, 1e-6f);
            Assert.IsFalse(tuning.transferExteriorParticlesToFalling);
            Assert.AreEqual(0.006f / Mathf.Pow(0.05f, 4f), tuning.epsilon, 1f);

            HarmonicRunManifestRuntime manifestRuntime = HarmonicRunManifestSnapshotBuilder.BuildRuntime(pipeline);
            Assert.IsTrue(manifestRuntime.openTopCylinderUsePbf);
            Assert.AreEqual(3, manifestRuntime.pbfIterations);
            Assert.AreEqual(tuning.epsilon, manifestRuntime.epsilon, 0.5f);
            Assert.AreEqual(1f, manifestRuntime.pbfRelaxation, 1e-3f);
            Assert.AreEqual(tuning.particleMass, manifestRuntime.particleMass, 1e-6f);
            Assert.IsFalse(manifestRuntime.transferExteriorParticlesToFalling);

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
                StringAssert.Contains("\"runtime\"", json);
                StringAssert.Contains("\"openTopCylinderUsePbf\": true", json);
                StringAssert.Contains("\"pbfIterations\": 3", json);
                StringAssert.Contains("\"cellSize\"", json);
                StringAssert.Contains("\"latticeSpacing\"", json);
                StringAssert.DoesNotContain("\"sph\":", json);
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

        [Test]
        public void ComputePbfGradSqImpliedStats_MatchesPerParticleFormula()
        {
            float[] densities = { 3300f };
            float[] lambdas = { -0.144f };
            const float restDensity = 1000f;
            const float epsilon = 16f;

            GpuParticleReadbackUtility.ScalarStats stats =
                GpuParticleReadbackUtility.ComputePbfGradSqImpliedStats(
                    densities,
                    lambdas,
                    restDensity,
                    epsilon);

            float constraint = 2.3f;
            float expected = -constraint / lambdas[0] - epsilon;
            Assert.AreEqual(expected, stats.Avg, 0.01f);
        }
    }
}
