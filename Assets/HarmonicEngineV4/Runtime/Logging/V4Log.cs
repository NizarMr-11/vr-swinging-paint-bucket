using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HarmonicEngineV4.Logging
{
    public enum V4LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public enum V4LogCategory
    {
        General,
        Bake,
        Spawn,
        PassExecution,
        ZoneClassification,
        CanvasSettle,
        BufferBinding,
        Recording,
        BoundaryPressure,
        WallEscapeForensics
    }

    public interface IV4LogSink
    {
        void Write(V4LogLevel level, V4LogCategory category, string message);
        void Flush();
    }

    /// <summary>
    /// Static logging facade (spec section 11). All engine logging funnels through here;
    /// sinks fan out to console / run-record file. Categories are individually toggleable
    /// so per-frame pass logs can be silenced while bake/spawn logs stay on.
    /// </summary>
    public static class V4Log
    {
        private static readonly List<IV4LogSink> Sinks = new List<IV4LogSink>();
        private static readonly HashSet<V4LogCategory> DisabledCategories = new HashSet<V4LogCategory>();

        public static V4LogLevel MinimumLevel = V4LogLevel.Info;

        public static void AddSink(IV4LogSink sink)
        {
            if (sink != null && !Sinks.Contains(sink))
            {
                Sinks.Add(sink);
            }
        }

        public static void RemoveSink(IV4LogSink sink)
        {
            Sinks.Remove(sink);
        }

        public static void ClearSinks()
        {
            Sinks.Clear();
        }

        public static void SetCategoryEnabled(V4LogCategory category, bool enabled)
        {
            if (enabled)
            {
                DisabledCategories.Remove(category);
            }
            else
            {
                DisabledCategories.Add(category);
            }
        }

        public static bool IsCategoryEnabled(V4LogCategory category)
        {
            return !DisabledCategories.Contains(category);
        }

        public static void Verbose(V4LogCategory category, string message) => Write(V4LogLevel.Verbose, category, message);
        public static void Info(V4LogCategory category, string message) => Write(V4LogLevel.Info, category, message);
        public static void Warning(V4LogCategory category, string message) => Write(V4LogLevel.Warning, category, message);
        public static void Error(V4LogCategory category, string message) => Write(V4LogLevel.Error, category, message);

        public static void Write(V4LogLevel level, V4LogCategory category, string message)
        {
            if (level < MinimumLevel || DisabledCategories.Contains(category))
            {
                return;
            }

            for (int i = 0; i < Sinks.Count; i++)
            {
                Sinks[i].Write(level, category, message);
            }
        }

        public static void FlushAll()
        {
            for (int i = 0; i < Sinks.Count; i++)
            {
                Sinks[i].Flush();
            }
        }

        /// <summary>
        /// The single AOP-style seam (spec section 11): every GPU pass dispatch is wrapped
        /// in one of these scopes, which records duration and bound buffer bytes on dispose
        /// without any logging code inside the pass kernels themselves.
        /// </summary>
        public static PassScope BeginPass(string passName, V4LogCategory category = V4LogCategory.PassExecution)
        {
            return new PassScope(passName, category);
        }

        public readonly struct PassScopeData
        {
            public readonly string PassName;
            public readonly double DurationMs;
            public readonly long BufferBytes;

            public PassScopeData(string passName, double durationMs, long bufferBytes)
            {
                PassName = passName;
                DurationMs = durationMs;
                BufferBytes = bufferBytes;
            }
        }

        public sealed class PassScope : IDisposable
        {
            private readonly string _passName;
            private readonly V4LogCategory _category;
            private readonly Stopwatch _stopwatch;
            private long _bufferBytes;
            private bool _disposed;

            internal PassScope(string passName, V4LogCategory category)
            {
                _passName = passName;
                _category = category;
                _stopwatch = Stopwatch.StartNew();
            }

            public void RecordBufferBytes(long bytes)
            {
                _bufferBytes += bytes;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _stopwatch.Stop();
                Verbose(_category, $"pass={_passName} durationMs={_stopwatch.Elapsed.TotalMilliseconds:F3} bufferBytes={_bufferBytes}");
            }
        }
    }
}
