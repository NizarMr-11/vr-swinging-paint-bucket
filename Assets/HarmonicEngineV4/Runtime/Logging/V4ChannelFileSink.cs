using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Writes one append-only text log per V4LogCategory under
    /// {runDirectory}/channels/{category}.log for the lifetime of a run.
    /// </summary>
    public sealed class V4ChannelFileSink : IV4LogSink, IDisposable
    {
        private const int FlushEveryLines = 32;

        private readonly string _channelsDirectory;
        private readonly V4ChannelLogSettings _settings;
        private readonly Dictionary<V4LogCategory, StreamWriter> _writers = new Dictionary<V4LogCategory, StreamWriter>();
        private readonly Dictionary<V4LogCategory, int> _pendingLines = new Dictionary<V4LogCategory, int>();
        private bool _disposed;

        public string ChannelsDirectory => _channelsDirectory;

        public V4ChannelFileSink(string runDirectory, V4ChannelLogSettings settings)
        {
            if (string.IsNullOrEmpty(runDirectory))
            {
                throw new ArgumentException("run directory is required", nameof(runDirectory));
            }

            _settings = settings ?? new V4ChannelLogSettings();
            _channelsDirectory = Path.Combine(runDirectory, "channels");
            Directory.CreateDirectory(_channelsDirectory);
        }

        public static string FileNameFor(V4LogCategory category)
        {
            switch (category)
            {
                case V4LogCategory.General: return "general.log";
                case V4LogCategory.Bake: return "bake.log";
                case V4LogCategory.Spawn: return "spawn.log";
                case V4LogCategory.PassExecution: return "pass_execution.log";
                case V4LogCategory.ZoneClassification: return "zone_classification.log";
                case V4LogCategory.CanvasSettle: return "canvas_settle.log";
                case V4LogCategory.BufferBinding: return "buffer_binding.log";
                case V4LogCategory.Recording: return "recording.log";
                default: return $"{category.ToString().ToLowerInvariant()}.log";
            }
        }

        public string PathFor(V4LogCategory category)
        {
            return Path.Combine(_channelsDirectory, FileNameFor(category));
        }

        public void Write(V4LogLevel level, V4LogCategory category, string message)
        {
            if (_disposed || level < _settings.minimumLevel || !_settings.IsCategoryRecorded(category))
            {
                return;
            }

            StreamWriter writer = GetWriter(category);
            writer.WriteLine(FormatLine(level, category, message));

            int pending = 1;
            if (_pendingLines.TryGetValue(category, out int existing))
            {
                pending = existing + 1;
            }

            _pendingLines[category] = pending;
            if (pending >= FlushEveryLines)
            {
                writer.Flush();
                _pendingLines[category] = 0;
            }
        }

        public void Flush()
        {
            foreach (StreamWriter writer in _writers.Values)
            {
                writer.Flush();
            }

            foreach (V4LogCategory key in Enum.GetValues(typeof(V4LogCategory)))
            {
                _pendingLines[key] = 0;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Flush();

            foreach (StreamWriter writer in _writers.Values)
            {
                writer.Dispose();
            }

            _writers.Clear();
            _pendingLines.Clear();
        }

        private StreamWriter GetWriter(V4LogCategory category)
        {
            if (_writers.TryGetValue(category, out StreamWriter writer))
            {
                return writer;
            }

            string path = PathFor(category);
            writer = new StreamWriter(path, append: true, Encoding.UTF8)
            {
                AutoFlush = false
            };
            _writers[category] = writer;
            return writer;
        }

        private static string FormatLine(V4LogLevel level, V4LogCategory category, string message)
        {
            return $"[{DateTime.UtcNow:O}] [{level}] {message}";
        }
    }
}
