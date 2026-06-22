using HarmonicEngine.Diagnostics;
using HarmonicEngine.Diagnostics.Aspects;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class HarmonicMultiChannelLogTests
    {
        private string _tempRoot;
        private string _runDirectory;
        private MultiChannelFileLogAspect _fileLog;
        private ParticleTelemetryAspect _telemetry;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "HarmonicMultiChannelLogTests_" + System.Guid.NewGuid().ToString("N"));
            _runDirectory = Path.Combine(_tempRoot, "run_test");
            Directory.CreateDirectory(_runDirectory);

            HarmonicDiagnosticHub.Enabled = true;
            _fileLog = new MultiChannelFileLogAspect();
            _telemetry = new ParticleTelemetryAspect();
            _telemetry.Configure(0.05f, logToConsole: false, overlay: null);

            HarmonicDiagnosticHub.Register(_fileLog);
            HarmonicDiagnosticHub.Register(_telemetry);
            HarmonicDiagnosticHub.Initialize(null, forceReset: true, runDirectoryOverride: _runDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            HarmonicDiagnosticHub.Shutdown();
            HarmonicDiagnosticHub.Unregister(_telemetry);
            HarmonicDiagnosticHub.Unregister(_fileLog);

            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        public void Initialize_CreatesRunDirectoryWithManifest()
        {
            Assert.IsTrue(Directory.Exists(_runDirectory));
            string manifestPath = Path.Combine(_runDirectory, HarmonicRunManifest.FileName);
            Assert.IsTrue(File.Exists(manifestPath));

            string json = File.ReadAllText(manifestPath);
            StringAssert.Contains("run_test", json);
            StringAssert.Contains("session.log", json);
            StringAssert.Contains("sph.log", json);
            StringAssert.Contains("perf.log", json);
            StringAssert.Contains("diagnostics", json);
            StringAssert.Contains("enableProfileTelemetry", json);
        }

        [Test]
        public void Manifest_InitSnapshot_WritesBucketAndParticles()
        {
            var go = new GameObject("manifest-pipeline");
            var pipeline = go.AddComponent<PipelineExecutionController>();
            pipeline.SetContainerFluid(Vector3.zero, 0.55f, 0f, 1.1f, 0.1f, 0.85f, 400f);
            pipeline.SetCellSize(0.015f);
            pipeline.SetContainerFluidEnabled(true);

            HarmonicDiagnosticHub.Initialize(
                pipeline,
                HarmonicPipelineDiagnosticsSettings.CreateDefault(),
                forceReset: true,
                runDirectoryOverride: _runDirectory);
            HarmonicDiagnosticHub.RefreshManifestInit(
                spawnOverride: new HarmonicRunSpawnInfo
                {
                    method = "lattice",
                    spawnCount = 100,
                    spacing = 0.015f,
                    fillTopY = 0.55f,
                    spawnRadius = 0.495f
                },
                spawnLatticeOnStart: true,
                sceneContainerName: "TestContainer");

            string manifestPath = Path.Combine(_runDirectory, HarmonicRunManifest.FileName);
            string json = File.ReadAllText(manifestPath);
            StringAssert.Contains("bucket", json);
            StringAssert.Contains("0.55", json);
            StringAssert.Contains("cellSize", json);
            StringAssert.Contains("lattice", json);
            StringAssert.Contains("spawnMethod", json);
            StringAssert.Contains("initSnapshotAt", json);
            StringAssert.Contains("TestContainer", json);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Publish_RoutesEventsToCorrectChannelFiles()
        {
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.SessionStart, "HUB", "session marker", 0, 0f));
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.ParticlesAppended, "PIPELINE", "pipeline marker", 1, 0.1f, 256));
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage, "ENGINE", "stage=containerFluid engine marker", 2, 0.2f, 256));
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.RainStart, "RAIN", "rain marker", 3, 0.3f));
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage, "SPH", "[SPH CFL] OK c=12.0", 4, 0.4f, 256));
            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineFrameAfter, "TELEMETRY", "PERIODIC delta=0 total=256", 5, 0.5f, 256));

            HarmonicDiagnosticHub.Shutdown();

            AssertChannelContains(_runDirectory, HarmonicLogChannel.Session, "session marker");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Pipeline, "pipeline marker");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Engine, "engine marker");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Rain, "rain marker");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Sph, "[SPH CFL]");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Telemetry, "PERIODIC");

            string sphLog = ReadChannel(_runDirectory, HarmonicLogChannel.Sph);
            Assert.IsFalse(sphLog.Contains("rain marker"));
            Assert.IsFalse(ReadChannel(_runDirectory, HarmonicLogChannel.Rain).Contains("[SPH CFL]"));
        }

        [Test]
        public void Router_SphMarkerInMessage_RoutesToSphChannel()
        {
            var evt = new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "ENGINE",
                "[SPH HASH] rebuilds/frame: 3.0 (saved 29.0)",
                0,
                0f);
            Assert.AreEqual(HarmonicLogChannel.Sph, HarmonicLogRouter.Route(evt));
        }

        [Test]
        public void TelemetryAspect_PublishesToHub()
        {
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.ParticlesAppended,
                "PIPELINE",
                "appended=256 total=256",
                1,
                0.1f,
                256,
                intArg0: 256,
                intArg1: 256));

            HarmonicDiagnosticHub.Shutdown();

            AssertChannelContains(_runDirectory, HarmonicLogChannel.Telemetry, "COUNT_CHANGE");
            AssertChannelContains(_runDirectory, HarmonicLogChannel.Pipeline, "appended=256");
        }

        [Test]
        public void DefaultLogRoot_CreatesRunFolderWithAllChannelLogs()
        {
            HarmonicDiagnosticHub.Shutdown();
            HarmonicDiagnosticHub.Unregister(_telemetry);
            HarmonicDiagnosticHub.Unregister(_fileLog);

            string logRoot = HarmonicDiagnosticHub.GetDefaultLogDirectory();
            var aspect = new MultiChannelFileLogAspect();
            HarmonicDiagnosticHub.Register(aspect);
            HarmonicDiagnosticHub.Initialize(null, forceReset: true);

            string runDirectory = HarmonicDiagnosticHub.Session.RunDirectory;
            Assert.IsTrue(runDirectory.StartsWith(logRoot));
            Assert.IsTrue(Directory.Exists(runDirectory));

            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "SPH",
                "[SPH CFL] OK smoke",
                0,
                0f));
            HarmonicDiagnosticHub.Shutdown();
            HarmonicDiagnosticHub.Unregister(aspect);

            foreach (HarmonicLogChannel channel in System.Enum.GetValues(typeof(HarmonicLogChannel)))
            {
                Assert.IsTrue(
                    File.Exists(Path.Combine(runDirectory, channel.FileName())),
                    $"Missing channel log: {channel.FileName()}");
            }

            Assert.IsTrue(File.ReadAllText(Path.Combine(runDirectory, HarmonicLogChannel.Sph.FileName())).Contains("[SPH CFL]"));

            try
            {
                Directory.Delete(runDirectory, true);
            }
            catch (IOException ex)
            {
                Assert.Inconclusive($"Created run directory but could not delete it: {ex.Message}");
            }
        }

        [Test]
        public void HostLifecycle_UnregisterOnShutdown_PreventingDuplicateChannelLogs()
        {
            HarmonicDiagnosticHub.Shutdown();
            HarmonicDiagnosticHub.Unregister(_telemetry);
            HarmonicDiagnosticHub.Unregister(_fileLog);

            var aspect = new MultiChannelFileLogAspect();
            HarmonicDiagnosticHub.Register(aspect);
            string secondRun = Path.Combine(_tempRoot, "run_no_duplicates");
            Directory.CreateDirectory(secondRun);
            HarmonicDiagnosticHub.Initialize(null, forceReset: true, runDirectoryOverride: secondRun);

            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.SessionStart,
                "HUB",
                "smoke",
                0,
                0f));

            HarmonicDiagnosticHub.Unregister(aspect);
            HarmonicDiagnosticHub.Shutdown();
            Assert.AreEqual(0, HarmonicDiagnosticHub.RegisteredAspects.Count);
            Assert.AreEqual(1, Directory.GetFiles(secondRun, "session*.log").Length);
            Assert.AreEqual(1, Directory.GetFiles(secondRun, "engine*.log").Length);
        }

        [Test]
        public void PerfChannel_RoutesPerformanceEvents()
        {
            var routed = new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "PERF",
                "spike=1 frameMs=25.0 gridMs=1.2",
                10,
                1.5f,
                512);
            Assert.AreEqual(HarmonicLogChannel.Perf, HarmonicLogRouter.Route(routed));

            Publish(routed);
            HarmonicDiagnosticHub.Shutdown();

            AssertChannelContains(_runDirectory, HarmonicLogChannel.Perf, "frameMs=25.0");
            Assert.IsFalse(ReadChannel(_runDirectory, HarmonicLogChannel.Engine).Contains("frameMs=25.0"));
        }

        [Test]
        public void DiagnosticsController_AppliesSettingsToPipeline()
        {
            var go = new GameObject("pipeline-diagnostics-test");
            var pipeline = go.AddComponent<PipelineExecutionController>();
            var settings = HarmonicPipelineDiagnosticsSettings.CreateDefault();
            settings.frameDiagnosticInterval = 42;
            settings.muteSphTelemetry = true;
            settings.positionSampleInterval = 7;

            pipeline.ApplyDiagnosticsSettings(settings);

            Assert.AreEqual(42, GetPrivateField<int>(pipeline, "frameDiagnosticInterval"));
            Assert.AreEqual(true, GetPrivateField<bool>(pipeline, "muteSphTelemetry"));
            Assert.AreEqual(7, GetPrivateField<int>(pipeline, "positionSampleInterval"));

            Object.DestroyImmediate(go);
        }

        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}'");
            return (T)field.GetValue(instance);
        }

        private static void Publish(in HarmonicDiagnosticEvent evt) => HarmonicDiagnosticHub.Publish(evt);

        private static void AssertChannelContains(string runDirectory, HarmonicLogChannel channel, string expected)
        {
            string text = ReadChannel(runDirectory, channel);
            Assert.IsTrue(
                text.Contains(expected),
                $"Expected '{expected}' in {channel.FileName()} but got:\n{text}");
        }

        private static string ReadChannel(string runDirectory, HarmonicLogChannel channel)
        {
            string path = Path.Combine(runDirectory, channel.FileName());
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
    }
}
