using System.IO;
using HarmonicEngineV4.Logging;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>Run-recording tests (plan Phase 7): file validity, header completeness, summary accuracy, abnormal teardown.</summary>
    public sealed class V4RunRecorderTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "V4RunRecorderTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public void ScriptedRun_ProducesValidJson_WithHeaderAndSummary()
        {
            using var rig = V4TestRig.Create();
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir);

            rig.Step(30);
            recorder.FinalizeRun();

            Assert.IsTrue(File.Exists(recorder.FilePath), "run file was not written");
            string json = File.ReadAllText(recorder.FilePath);
            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(json);
            Assert.IsNotNull(parsed, "run file is not valid JSON");

            // Header (spec section 12): device, manifest, profiles, spawn zones, density, bake.
            Assert.IsNotEmpty(parsed.header.gpuName);
            Assert.IsNotEmpty(parsed.header.unityVersion);
            Assert.IsNotEmpty(parsed.header.graphicsApi);
            Assert.IsNotEmpty(parsed.header.manifestName);
            Assert.Greater(parsed.header.manifestPassCount, 0);
            Assert.AreEqual(parsed.header.manifestPassCount, parsed.header.manifestPasses.Count);
            Assert.Greater(parsed.header.globalDensity, 0f);
            Assert.Greater(parsed.header.particleRadius, 0f);
            Assert.Greater(parsed.header.spawnedTotal, 0);
            Assert.Greater(parsed.header.canvasGridX, 0);
            Assert.IsNotEmpty(parsed.header.profiles);
            Assert.IsNotEmpty(parsed.header.spawnZones);

            // Time series sampled every 5 frames over 30 frames.
            Assert.GreaterOrEqual(parsed.samples.Count, 5);

            // Summary matches the pipeline's final GPU-readback state.
            Assert.IsTrue(parsed.summary.finalized);
            Assert.AreEqual(30, parsed.summary.totalFrames);
            Assert.AreEqual(rig.Root.ActiveParticleCount, parsed.summary.finalLive);
            Assert.AreEqual(rig.Root.EscapedTotal, parsed.summary.finalEscaped);
            Assert.AreEqual(rig.Root.SettledTotal, parsed.summary.finalSettled);
        }

        [Test]
        public void AbnormalTeardown_DisposeWithoutFinalize_StillWritesSummary()
        {
            using var rig = V4TestRig.Create();
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir);
            rig.Step(10);

            recorder.Dispose(); // simulates object destruction mid-run

            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(File.ReadAllText(recorder.FilePath));
            Assert.IsTrue(parsed.summary.finalized, "dispose must finalize the run");
            Assert.AreEqual(10, parsed.summary.totalFrames);
        }

        [Test]
        public void HeaderIsPersistedImmediately_BeforeAnyFrame()
        {
            using var rig = V4TestRig.Create();
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir);

            // No frames stepped, no finalize: the header must already be on disk.
            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(File.ReadAllText(recorder.FilePath));
            Assert.IsNotEmpty(parsed.header.gpuName);
            Assert.IsFalse(parsed.summary.finalized);

            recorder.Dispose();
        }

        [Test]
        public void WarningsAndErrors_AreCapturedInRunFile()
        {
            using var rig = V4TestRig.Create();
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir);

            // No console sink is registered in the headless test context, so these only
            // reach the recorder sink; guard with LogAssert in case one is registered.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            V4Log.Warning(V4LogCategory.General, "test-warning-xyz");
            V4Log.Error(V4LogCategory.General, "test-error-xyz");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            recorder.FinalizeRun();

            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(File.ReadAllText(recorder.FilePath));
            Assert.IsTrue(parsed.warnings.Exists(w => w.Contains("test-warning-xyz")));
            Assert.IsTrue(parsed.errors.Exists(e => e.Contains("test-error-xyz")));
        }
    }
}
