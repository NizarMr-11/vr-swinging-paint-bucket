using System;
using System.Collections.Generic;
using System.IO;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Run recording (spec section 12): one folder per run with manifest.json (header,
    /// sampled time-series, warnings/errors, summary) plus per-channel text logs under
    /// channels/*.log via <see cref="V4ChannelFileSink"/>.
    /// </summary>
    public sealed class V4RunRecorder : IV4LogSink, IDisposable
    {
        [Serializable]
        public class RunFile
        {
            public Header header = new Header();
            public List<Sample> samples = new List<Sample>();
            public List<string> warnings = new List<string>();
            public List<string> errors = new List<string>();
            public Summary summary = new Summary();
        }

        [Serializable]
        public class Header
        {
            public string startedAtUtc;
            public string runDirectory;
            public string channelsDirectory;
            public string unityVersion;
            public string gpuName;
            public int gpuMemoryMb;
            public string graphicsApi;
            public string manifestName;
            public int manifestPassCount;
            public List<string> manifestPasses = new List<string>();
            public float globalDensity;
            public float particleRadius;
            public int spawnedTotal;
            public int holeCount;
            public int canvasGridX;
            public int canvasGridY;
            public List<string> profiles = new List<string>();
            public List<string> spawnZones = new List<string>();
        }

        [Serializable]
        public class Sample
        {
            public long frame;
            public int live;
            public int escaped;
            public int settled;
            public float totalExpectedLoss;
        }

        [Serializable]
        public class Summary
        {
            public bool finalized;
            public long totalFrames;
            public int finalLive;
            public int finalEscaped;
            public int finalSettled;
            public float durationSeconds;
        }

        private const int MaxLoggedIssues = 200;

        private readonly V4PipelineRoot _root;
        private readonly int _sampleEveryNFrames;
        private readonly V4ChannelLogSettings _channelSettings;
        private readonly RunFile _data = new RunFile();
        private readonly DateTime _startedUtc;
        private readonly V4ChannelFileSink _channelSink;
        private readonly V4LogLevel _restoredMinimumLevel;
        private bool _finalized;

        /// <summary>Run folder: {base}/run_yyyyMMdd_HHmmss_fff/</summary>
        public string RunDirectory { get; }

        /// <summary>Structured manifest inside the run folder.</summary>
        public string FilePath { get; }

        public V4ChannelFileSink ChannelSink => _channelSink;

        public V4RunRecorder(
            V4PipelineRoot root,
            int sampleEveryNFrames = 10,
            string directoryOverride = null,
            V4ChannelLogSettings channelSettings = null)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _sampleEveryNFrames = Mathf.Max(1, sampleEveryNFrames);
            _startedUtc = DateTime.UtcNow;

            string baseDirectory = directoryOverride ?? V4RunLogPaths.DefaultRunsDirectory();
            Directory.CreateDirectory(baseDirectory);
            RunDirectory = Path.Combine(baseDirectory, $"run_{_startedUtc:yyyyMMdd_HHmmss_fff}");
            Directory.CreateDirectory(RunDirectory);
            FilePath = Path.Combine(RunDirectory, "manifest.json");

            _channelSettings = channelSettings ?? new V4ChannelLogSettings();
            _channelSink = new V4ChannelFileSink(RunDirectory, _channelSettings);

            _restoredMinimumLevel = V4Log.MinimumLevel;
            if (_channelSettings.minimumLevel < V4Log.MinimumLevel)
            {
                V4Log.MinimumLevel = _channelSettings.minimumLevel;
            }

            WriteHeader();
            _root.FrameCompleted += OnFrameCompleted;
            V4Log.AddSink(_channelSink);
            V4Log.AddSink(this);
            SnapshotStartupState();

            // Persist the header immediately so even a hard crash leaves a valid record.
            Flush();
            V4Log.Info(V4LogCategory.Recording, $"run recording started dir={RunDirectory}");
        }

        /// <summary>
        /// Bake/spawn logs often fire before the recorder attaches; emit a one-line snapshot
        /// per channel so channel files always capture run configuration.
        /// </summary>
        private void SnapshotStartupState()
        {
            V4Log.Info(V4LogCategory.Bake,
                $"snapshot holes={_root.BakedHoles?.Count ?? 0} grid={_root.CanvasGridSize.x}x{_root.CanvasGridSize.y} cell={_root.CanvasCellSize:F4} ok={_root.BakeSucceeded}");
            V4Log.Info(V4LogCategory.Spawn,
                $"snapshot spawned={_root.SpawnedTotal} live={_root.ActiveParticleCount} radius={_root.ParticleRadius:F4} density={_root.globalDensity:F0}");
            if (_root.ActiveManifest != null)
            {
                V4Log.Info(V4LogCategory.General,
                    $"snapshot manifest={_root.ActiveManifest.manifestName} passes={_root.ActiveManifest.passes.Count}");
            }
        }

        private void WriteHeader()
        {
            Header h = _data.header;
            h.startedAtUtc = _startedUtc.ToString("O");
            h.runDirectory = RunDirectory;
            h.channelsDirectory = _channelSink.ChannelsDirectory;
            h.unityVersion = Application.unityVersion;
            h.gpuName = SystemInfo.graphicsDeviceName;
            h.gpuMemoryMb = SystemInfo.graphicsMemorySize;
            h.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            h.globalDensity = _root.globalDensity;
            h.particleRadius = _root.ParticleRadius;
            h.spawnedTotal = _root.SpawnedTotal;
            h.holeCount = _root.BakedHoles?.Count ?? 0;
            h.canvasGridX = _root.CanvasGridSize.x;
            h.canvasGridY = _root.CanvasGridSize.y;

            if (_root.ActiveManifest != null)
            {
                h.manifestName = _root.ActiveManifest.manifestName;
                h.manifestPassCount = _root.ActiveManifest.passes.Count;
                foreach (Core.V4PassDef pass in _root.ActiveManifest.passes)
                {
                    h.manifestPasses.Add($"{pass.passId}:{pass.kernelName} rw={pass.readWriteBuffers.Length} ro={pass.readOnlyBuffers.Length}");
                }
            }

            foreach (Profiles.V4LiquidProfile profile in _root.ProfileTable)
            {
                if (profile != null)
                {
                    h.profiles.Add($"{profile.profileName} rest={profile.restDensity} visc={profile.viscosity} settleEps={profile.settleEpsilon}");
                }
            }

            foreach (V4SpawnZone zone in _root.spawnZones)
            {
                if (zone != null)
                {
                    h.spawnZones.Add($"{zone.name} r={zone.radius} vol={zone.Volume:F5} color=#{ColorUtility.ToHtmlStringRGB(zone.color)}");
                }
            }
        }

        private void OnFrameCompleted(long frame, int live, int escaped, int settled)
        {
            if (frame % _sampleEveryNFrames != 0)
            {
                return;
            }

            _data.samples.Add(new Sample
            {
                frame = frame,
                live = live,
                escaped = escaped,
                settled = settled,
                totalExpectedLoss = _root.TotalExpectedLoss
            });
        }

        public void Write(V4LogLevel level, V4LogCategory category, string message)
        {
            if (level == V4LogLevel.Warning && _data.warnings.Count < MaxLoggedIssues)
            {
                _data.warnings.Add($"[{category}] {message}");
            }
            else if (level == V4LogLevel.Error && _data.errors.Count < MaxLoggedIssues)
            {
                _data.errors.Add($"[{category}] {message}");
            }
        }

        public void Flush()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[V4RunRecorder] failed to write {FilePath}: {e.Message}");
            }

            _channelSink.Flush();
        }

        /// <summary>Finalizes the summary and writes the file. Safe to call multiple times.</summary>
        public void FinalizeRun()
        {
            if (_finalized)
            {
                return;
            }

            _finalized = true;
            _root.FrameCompleted -= OnFrameCompleted;
            V4Log.RemoveSink(this);
            V4Log.RemoveSink(_channelSink);
            V4Log.MinimumLevel = _restoredMinimumLevel;

            _data.summary.finalized = true;
            _data.summary.totalFrames = _root.FrameIndex;
            _data.summary.finalLive = _root.ActiveParticleCount;
            _data.summary.finalEscaped = _root.EscapedTotal;
            _data.summary.finalSettled = _root.SettledTotal;
            _data.summary.durationSeconds = (float)(DateTime.UtcNow - _startedUtc).TotalSeconds;
            Flush();
            _channelSink.Dispose();
        }

        public void Dispose()
        {
            FinalizeRun();
        }
    }
}
