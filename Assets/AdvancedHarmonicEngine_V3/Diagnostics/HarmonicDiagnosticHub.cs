using HarmonicEngine.Infrastructure.Management;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// Central publish/subscribe hub for engine-wide diagnostic aspects (AOP-style cross-cutting).
    /// </summary>
    public static class HarmonicDiagnosticHub
    {
        private static readonly List<IHarmonicDiagnosticAspect> Aspects = new();
        private static HarmonicDiagnosticSession _session;
        private static bool _initialized;
        private static bool _initSnapshotWritten;

        public static bool Enabled { get; set; } = true;
        public static HarmonicDiagnosticSession Session => _session;
        public static IReadOnlyList<IHarmonicDiagnosticAspect> RegisteredAspects => Aspects;
        public static bool InitSnapshotWritten => _initSnapshotWritten;

        public static void Initialize(
            PipelineExecutionController pipeline,
            bool forceReset = false,
            string runDirectoryOverride = null)
        {
            Initialize(
                pipeline,
                HarmonicPipelineDiagnosticsSettings.CreateDefault(),
                forceReset,
                runDirectoryOverride);
        }

        public static void Initialize(
            PipelineExecutionController pipeline,
            HarmonicPipelineDiagnosticsSettings settings,
            bool forceReset = false,
            string runDirectoryOverride = null)
        {
            if (_initialized && !forceReset)
            {
                if (pipeline != null && _session != null)
                {
                    _session.Pipeline = pipeline;
                }

                return;
            }

            Shutdown();

            string logRoot = GetDefaultLogDirectory();
            Directory.CreateDirectory(logRoot);
            string runDirectory = runDirectoryOverride ?? CreateRunDirectory(logRoot);
            Directory.CreateDirectory(runDirectory);

            _session = new HarmonicDiagnosticSession(pipeline, logRoot, runDirectory, settings);
            _initSnapshotWritten = false;
            HarmonicRunManifest.WriteStart(_session, settings);
            _initialized = true;
            _session.FrameIndex = 0;

            var aspects = Aspects.ToArray();
            Array.Sort(aspects, (a, b) => a.Order.CompareTo(b.Order));
            foreach (IHarmonicDiagnosticAspect aspect in aspects)
            {
                try
                {
                    aspect.OnAttach(_session);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HarmonicDiagnosticHub] Failed to attach {aspect.AspectName}: {ex.Message}");
                }
            }

            Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.SessionStart,
                "HUB",
                _session.BuildHeader(),
                _session.FrameIndex,
                _session.ElapsedSeconds));
        }

        public static void Shutdown()
        {
            if (_session != null)
            {
                Publish(new HarmonicDiagnosticEvent(
                    HarmonicDiagnosticEventType.SessionEnd,
                    "HUB",
                    "shutdown",
                    _session.FrameIndex,
                    _session.ElapsedSeconds,
                    _session.ReadActiveParticleCount()));
            }

            for (int i = Aspects.Count - 1; i >= 0; i--)
            {
                Aspects[i].OnDetach();
            }

            _session = null;
            _initialized = false;
            _initSnapshotWritten = false;
        }

        public static void RefreshManifestInit(
            HarmonicRunSpawnInfo spawnOverride = null,
            bool spawnLatticeOnStart = false,
            string sceneContainerName = null)
        {
            if (_session == null || _initSnapshotWritten)
            {
                return;
            }

            HarmonicRunManifest.WriteInitSnapshot(
                _session,
                spawnOverride,
                spawnLatticeOnStart,
                sceneContainerName);
            _initSnapshotWritten = true;
        }
        public static void Register(IHarmonicDiagnosticAspect aspect)
        {
            if (aspect == null || Aspects.Contains(aspect))
            {
                return;
            }

            Aspects.Add(aspect);

            if (_session != null)
            {
                aspect.OnAttach(_session);
            }
        }

        public static void Unregister(IHarmonicDiagnosticAspect aspect)
        {
            if (aspect == null)
            {
                return;
            }

            aspect.OnDetach();
            Aspects.Remove(aspect);
        }

        public static void TickFrame()
        {
            if (_session != null)
            {
                _session.FrameIndex++;
            }
        }

        public static void Publish(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            if (!Enabled)
            {
                return;
            }

            for (int i = 0; i < Aspects.Count; i++)
            {
                try
                {
                    Aspects[i].OnEvent(diagnosticEvent);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public static void PublishSimple(
            HarmonicDiagnosticEventType type,
            string category,
            string message,
            int intArg0 = 0,
            int intArg1 = 0)
        {
            float t = _session?.ElapsedSeconds ?? 0f;
            int frame = _session?.FrameIndex ?? 0;
            uint active = _session?.ReadActiveParticleCount() ?? 0;
            Publish(new HarmonicDiagnosticEvent(type, category, message, frame, t, active, intArg0: intArg0, intArg1: intArg1));
        }

        public static string GetDefaultLogDirectory()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "HarmonicSimulation"));
#else
            return Path.Combine(Application.persistentDataPath, "HarmonicSimulation");
#endif
        }

        internal static string CreateRunDirectory(string logRoot)
        {
            Directory.CreateDirectory(logRoot);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string path = Path.Combine(logRoot, $"run_{stamp}");
            int attempt = 0;
            while (Directory.Exists(path) && attempt < 100)
            {
                attempt++;
                path = Path.Combine(logRoot, $"run_{stamp}_{attempt}");
            }

            return path;
        }
    }
}
