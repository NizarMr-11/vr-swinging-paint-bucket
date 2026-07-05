using System.IO;
using HarmonicEngineV4.Logging;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>Run-recording tests (plan Phase 7): manifest validity, per-channel logs, summary accuracy.</summary>
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

            Assert.IsTrue(Directory.Exists(recorder.RunDirectory), "run directory was not created");
            Assert.IsTrue(File.Exists(recorder.FilePath), "manifest was not written");
            string json = File.ReadAllText(recorder.FilePath);
            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(json);
            Assert.IsNotNull(parsed, "manifest is not valid JSON");

            Assert.AreEqual(recorder.RunDirectory, parsed.header.runDirectory);
            Assert.IsTrue(Directory.Exists(parsed.header.channelsDirectory));

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
        public void ScriptedRun_WritesPerChannelLogs()
        {
            using var rig = V4TestRig.Create();
            var settings = new V4ChannelLogSettings { recordPassExecution = false };
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir, channelSettings: settings);

            rig.Step(30);
            recorder.FinalizeRun();

            string bakeLog = recorder.ChannelSink.PathFor(V4LogCategory.Bake);
            string generalLog = recorder.ChannelSink.PathFor(V4LogCategory.General);
            string passLog = recorder.ChannelSink.PathFor(V4LogCategory.PassExecution);

            Assert.IsTrue(File.Exists(bakeLog), "bake channel log missing");
            Assert.IsTrue(File.Exists(generalLog), "general channel log missing");
            Assert.IsFalse(File.Exists(passLog), "pass_execution should stay off by default");

            string bakeText = File.ReadAllText(bakeLog);
            Assert.IsTrue(bakeText.Contains("snapshot holes="), "bake log should contain startup snapshot");

            string recordingLog = recorder.ChannelSink.PathFor(V4LogCategory.Recording);
            Assert.IsTrue(File.Exists(recordingLog));
            Assert.IsTrue(File.ReadAllText(recordingLog).Contains("run recording started"));
        }

        [Test]
        public void PassExecutionChannel_RecordedWhenEnabled()
        {
            using var rig = V4TestRig.Create();
            var settings = new V4ChannelLogSettings
            {
                recordPassExecution = true,
                minimumLevel = V4LogLevel.Verbose
            };
            var recorder = new V4RunRecorder(rig.Root, directoryOverride: _tempDir, channelSettings: settings);

            rig.Step(3);
            recorder.FinalizeRun();

            string passLog = recorder.ChannelSink.PathFor(V4LogCategory.PassExecution);
            Assert.IsTrue(File.Exists(passLog), "pass_execution log should exist when enabled");
            Assert.IsTrue(File.ReadAllText(passLog).Contains("pass="), "pass_execution log should contain pass scopes");
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
        public void WarningsAndErrors_AreCapturedInManifestAndChannelFile()
        {
            using var rig = V4TestRig.Create();
            var recorder = new V4RunRecorder(rig.Root, sampleEveryNFrames: 5, directoryOverride: _tempDir);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            V4Log.Warning(V4LogCategory.General, "test-warning-xyz");
            V4Log.Error(V4LogCategory.General, "test-error-xyz");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            recorder.FinalizeRun();

            var parsed = JsonUtility.FromJson<V4RunRecorder.RunFile>(File.ReadAllText(recorder.FilePath));
            Assert.IsTrue(parsed.warnings.Exists(w => w.Contains("test-warning-xyz")));
            Assert.IsTrue(parsed.errors.Exists(e => e.Contains("test-error-xyz")));

            string generalLog = File.ReadAllText(recorder.ChannelSink.PathFor(V4LogCategory.General));
            Assert.IsTrue(generalLog.Contains("test-warning-xyz"));
            Assert.IsTrue(generalLog.Contains("test-error-xyz"));
        }
    }
}
