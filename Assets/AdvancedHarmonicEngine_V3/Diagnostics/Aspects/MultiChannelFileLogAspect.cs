using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class MultiChannelFileLogAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "MultiChannelFileLog";
        public int Order => 0;

        private static readonly object LogCreationLock = new();

        private readonly Dictionary<HarmonicLogChannel, StreamWriter> _writers = new();
        private readonly Dictionary<HarmonicLogChannel, int> _lineCounts = new();
        private string _runDirectory;
        private bool _flushEachLine = true;

        public string RunDirectory => _runDirectory ?? string.Empty;

        public string GetChannelLogPath(HarmonicLogChannel channel) =>
            string.IsNullOrEmpty(_runDirectory)
                ? string.Empty
                : Path.Combine(_runDirectory, channel.FileName());

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            OnDetach();

            _runDirectory = session.RunDirectory;
            if (string.IsNullOrEmpty(_runDirectory))
            {
                Debug.LogWarning("[HarmonicDiagnostic] Run directory is empty; multi-channel file log disabled.");
                return;
            }

            lock (LogCreationLock)
            {
                foreach (HarmonicLogChannel channel in Enum.GetValues(typeof(HarmonicLogChannel)))
                {
                    string path = Path.Combine(_runDirectory, channel.FileName());
                    StreamWriter writer = TryOpenLogWriter(path);
                    if (writer == null)
                    {
                        Debug.LogWarning(
                            $"[HarmonicDiagnostic] Could not open channel log '{path}'. " +
                            "Close any program viewing Logs/HarmonicSimulation, or disable File Log on HarmonicDiagnosticHost.");
                        continue;
                    }

                    writer.WriteLine($"# Harmonic {channel} log — {DateTime.Now:O}");
                    writer.WriteLine($"# runDirectory={_runDirectory}");
                    _writers[channel] = writer;
                    _lineCounts[channel] = 0;
                }
            }
        }

        public void OnDetach()
        {
            if (_writers.Count == 0)
            {
                return;
            }

            string runDirectory = _runDirectory;
            try
            {
                foreach (StreamWriter writer in _writers.Values)
                {
                    writer.Flush();
                    writer.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HarmonicDiagnostic] Failed flushing channel logs: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(runDirectory))
            {
                HarmonicDiagnosticSession session = HarmonicDiagnosticHub.Session;
                uint finalActiveCount = session?.ReadActiveParticleCount() ?? 0;
                float durationSeconds = session?.ElapsedSeconds ?? 0f;
                HarmonicRunManifest.WriteEnd(runDirectory, _lineCounts, finalActiveCount, durationSeconds);
            }

            _writers.Clear();
            _lineCounts.Clear();
            _runDirectory = null;
        }

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            HarmonicLogChannel channel = HarmonicLogRouter.Route(diagnosticEvent);
            if (!_writers.TryGetValue(channel, out StreamWriter writer) || writer == null)
            {
                return;
            }

            string line = HarmonicLogLineFormatter.Format(diagnosticEvent);
            try
            {
                writer.WriteLine(line);
                _lineCounts[channel] = _lineCounts.TryGetValue(channel, out int count) ? count + 1 : 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HarmonicDiagnostic] Failed writing {channel} log line: {ex.Message}");
            }
        }

        private static StreamWriter TryOpenLogWriter(string path)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    return new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                }
                catch (IOException ex) when (attempt < 4)
                {
                    Debug.LogWarning($"[HarmonicDiagnostic] Channel log open retry {attempt + 1}: {ex.Message}");
                    string dir = Path.GetDirectoryName(path) ?? ".";
                    string name = Path.GetFileNameWithoutExtension(path);
                    path = Path.Combine(dir, $"{name}_{Guid.NewGuid():N}.log");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.LogWarning($"[HarmonicDiagnostic] Channel log access denied: {ex.Message}");
                    return null;
                }
            }

            return null;
        }
    }
}
