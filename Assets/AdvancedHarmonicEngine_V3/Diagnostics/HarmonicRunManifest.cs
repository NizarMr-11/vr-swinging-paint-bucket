using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    [Serializable]
    public sealed class HarmonicRunManifestData
    {
        public string startedAt;
        public string initSnapshotAt;
        public string endedAt;
        public float durationSeconds;
        public string runDirectory;
        public string logRoot;
        public string unityVersion;
        public string platform;
        public string scene;
        public string gpu;
        public int maxCapacity;
        public bool worldFallingOnly;
        public HarmonicRunManifestEnvironment environment;
        public HarmonicRunManifestBucket bucket;
        public HarmonicRunManifestBucketNozzle bucketNozzle;
        public HarmonicRunManifestSph sph;
        public HarmonicRunManifestSimulation simulation;
        public HarmonicRunManifestParticles particles;
        public HarmonicRunManifestInitConditions initConditions;
        public HarmonicPipelineDiagnosticsSettings diagnostics;
        public string[] channelFiles;
        public int[] channelLineCounts;
    }

    public static class HarmonicRunManifest
    {
        public const string FileName = "manifest.json";

        public static string[] ChannelFileNames { get; } =
        {
            HarmonicLogChannel.Session.FileName(),
            HarmonicLogChannel.Pipeline.FileName(),
            HarmonicLogChannel.Engine.FileName(),
            HarmonicLogChannel.Rain.FileName(),
            HarmonicLogChannel.Sph.FileName(),
            HarmonicLogChannel.Telemetry.FileName(),
            HarmonicLogChannel.Perf.FileName()
        };

        public static void WriteStart(
            HarmonicDiagnosticSession session,
            HarmonicPipelineDiagnosticsSettings diagnosticsSettings)
        {
            var environment = HarmonicRunManifestSnapshotBuilder.BuildEnvironment();
            var data = new HarmonicRunManifestData
            {
                startedAt = DateTime.Now.ToString("O"),
                runDirectory = session.RunDirectory,
                logRoot = session.LogDirectory,
                unityVersion = environment.unityVersion,
                platform = environment.platform,
                scene = environment.scene,
                gpu = environment.gpu,
                maxCapacity = session.Pipeline?.MaxCapacity ?? 0,
                worldFallingOnly = session.Pipeline?.WorldFallingOnly ?? false,
                environment = environment,
                diagnostics = diagnosticsSettings,
                channelFiles = ChannelFileNames,
                channelLineCounts = new int[ChannelFileNames.Length]
            };

            WriteManifest(session.RunDirectory, data);
        }

        public static void WriteInitSnapshot(
            HarmonicDiagnosticSession session,
            HarmonicRunSpawnInfo spawnOverride = null,
            bool spawnLatticeOnStart = false,
            string sceneContainerName = null)
        {
            if (session == null || string.IsNullOrEmpty(session.RunDirectory))
            {
                return;
            }

            HarmonicRunManifestData data = ReadManifest(session.RunDirectory);
            if (data == null)
            {
                return;
            }

            PipelineExecutionController pipeline = session.Pipeline;
            if (pipeline == null)
            {
                return;
            }

            HarmonicRunSpawnInfo spawn = spawnOverride ?? pipeline.LastRunSpawnInfo;
            data.initSnapshotAt = DateTime.Now.ToString("O");
            data.maxCapacity = pipeline.MaxCapacity;
            data.worldFallingOnly = pipeline.WorldFallingOnly;
            data.bucket = HarmonicRunManifestSnapshotBuilder.BuildBucket(pipeline, sceneContainerName);
            data.bucketNozzle = HarmonicRunManifestSnapshotBuilder.BuildBucketNozzle(pipeline);
            data.sph = HarmonicRunManifestSnapshotBuilder.BuildSph(pipeline);
            data.simulation = HarmonicRunManifestSnapshotBuilder.BuildSimulation(pipeline);
            data.particles = HarmonicRunManifestSnapshotBuilder.BuildParticles(pipeline, spawn);
            data.initConditions = HarmonicRunManifestSnapshotBuilder.BuildInitConditions(pipeline, spawnLatticeOnStart);

            WriteManifest(session.RunDirectory, data);
        }

        public static void WriteEnd(
            string runDirectory,
            IReadOnlyDictionary<HarmonicLogChannel, int> lineCounts,
            uint finalActiveCount,
            float durationSeconds)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                return;
            }

            HarmonicRunManifestData data = ReadManifest(runDirectory);
            if (data == null)
            {
                return;
            }

            data.endedAt = DateTime.Now.ToString("O");
            data.durationSeconds = durationSeconds;
            data.channelLineCounts = new int[ChannelFileNames.Length];
            for (int i = 0; i < ChannelFileNames.Length; i++)
            {
                HarmonicLogChannel channel = (HarmonicLogChannel)i;
                data.channelLineCounts[i] = lineCounts != null && lineCounts.TryGetValue(channel, out int count)
                    ? count
                    : 0;
            }

            if (data.particles == null)
            {
                data.particles = new HarmonicRunManifestParticles();
            }

            data.particles.activeCountAtEnd = (int)finalActiveCount;

            WriteManifest(runDirectory, data);
        }

        private static HarmonicRunManifestData ReadManifest(string runDirectory)
        {
            string path = Path.Combine(runDirectory, FileName);
            if (!File.Exists(path))
            {
                return null;
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<HarmonicRunManifestData>(json);
        }

        private static void WriteManifest(string runDirectory, HarmonicRunManifestData data)
        {
            string path = Path.Combine(runDirectory, FileName);
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true), Encoding.UTF8);
        }
    }
}
