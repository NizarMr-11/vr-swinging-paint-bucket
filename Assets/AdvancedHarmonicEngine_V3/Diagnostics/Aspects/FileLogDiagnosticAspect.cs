using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class FileLogDiagnosticAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "FileLog";
        public int Order => 0;

        private static readonly object LogCreationLock = new();

        private StreamWriter _writer;
        private string _logFilePath;
        private bool _flushEachLine = true;

        public string LogFilePath => _logFilePath;

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            OnDetach();

            lock (LogCreationLock)
            {
                _logFilePath = CreateUniqueLogPath(session.LogDirectory);
                _writer = TryOpenLogWriter(_logFilePath);
            }

            if (_writer == null)
            {
                Debug.LogWarning(
                    $"[HarmonicDiagnostic] Could not open log file at '{_logFilePath}'. " +
                    "Close any program viewing Logs/HarmonicSimulation, or disable File Log on HarmonicDiagnosticHost.");
                return;
            }

            WriteLine($"# Harmonic AOP log — {DateTime.Now:O}");
            WriteLine($"# path={_logFilePath}");
        }

        public void OnDetach()
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                _writer.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HarmonicDiagnostic] Failed flushing log: {ex.Message}");
            }

            _writer.Dispose();
            _writer = null;
        }

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            string line = FormatLine(diagnosticEvent);
            WriteLine(line);
        }

        private void WriteLine(string line)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HarmonicDiagnostic] Failed writing log line: {ex.Message}");
            }
        }

        private static string CreateUniqueLogPath(string logDirectory)
        {
            Directory.CreateDirectory(logDirectory);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string path = Path.Combine(logDirectory, $"harmonic_session_{stamp}.log");
            int attempt = 0;
            while (File.Exists(path) && attempt < 100)
            {
                attempt++;
                path = Path.Combine(logDirectory, $"harmonic_session_{stamp}_{attempt}.log");
            }

            return path;
        }

        private StreamWriter TryOpenLogWriter(string path)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    // FileShare.Read allows tail/viewers while Unity writes; avoids common sharing violations.
                    var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    return new StreamWriter(stream, Encoding.UTF8) { AutoFlush = _flushEachLine };
                }
                catch (IOException ex) when (attempt < 4)
                {
                    Debug.LogWarning($"[HarmonicDiagnostic] Log open retry {attempt + 1}: {ex.Message}");
                    string dir = Path.GetDirectoryName(path) ?? ".";
                    string name = Path.GetFileNameWithoutExtension(path);
                    path = Path.Combine(dir, $"{name}_{Guid.NewGuid():N}.log");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.LogWarning($"[HarmonicDiagnostic] Log access denied: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        private static string FormatLine(in HarmonicDiagnosticEvent e)
        {
            var sb = new StringBuilder(192);
            sb.Append('[').Append(e.TimeSeconds.ToString("F3")).Append("s] ");
            sb.Append('[').Append(e.Type).Append("] ");
            sb.Append('[').Append(e.Category).Append("] ");
            if (!string.IsNullOrEmpty(e.Message))
            {
                sb.Append(e.Message).Append(' ');
            }

            sb.Append("frame=").Append(e.FrameIndex);
            sb.Append(" active=").Append(e.ActiveParticleCount);
            if (e.PeakParticleCount > 0)
            {
                sb.Append(" peak=").Append(e.PeakParticleCount);
            }

            if (e.CanvasHitCount > 0)
            {
                sb.Append(" canvasHits=").Append(e.CanvasHitCount);
            }

            if (e.IntArg0 != 0 || e.IntArg1 != 0)
            {
                sb.Append(" i0=").Append(e.IntArg0).Append(" i1=").Append(e.IntArg1);
            }

            return sb.ToString().TrimEnd();
        }
    }
}
