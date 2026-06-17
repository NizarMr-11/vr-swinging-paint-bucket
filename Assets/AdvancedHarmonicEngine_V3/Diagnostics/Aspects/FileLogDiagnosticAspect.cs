using System;
using System.IO;
using System.Text;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class FileLogDiagnosticAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "FileLog";
        public int Order => 0;

        private StreamWriter _writer;
        private string _logFilePath;
        private bool _flushEachLine = true;

        public string LogFilePath => _logFilePath;

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(session.LogDirectory, $"harmonic_session_{stamp}.log");
            _writer = new StreamWriter(_logFilePath, false, Encoding.UTF8) { AutoFlush = _flushEachLine };
            WriteLine($"# Harmonic AOP log — {DateTime.Now:O}");
            WriteLine($"# path={_logFilePath}");
        }

        public void OnDetach()
        {
            if (_writer == null)
            {
                return;
            }

            _writer.Flush();
            _writer.Close();
            _writer = null;
        }

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            string line = FormatLine(diagnosticEvent);
            WriteLine(line);
        }

        private void WriteLine(string line)
        {
            _writer?.WriteLine(line);
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
