using System;
using System.Collections.Generic;
using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    public enum V4BufferLifetime
    {
        /// <summary>Written once during bake, read-only afterwards.</summary>
        StaticBaked,
        /// <summary>Rewritten every frame (counters, scratch).</summary>
        PerFrame,
        /// <summary>Lives for the whole session, mutated incrementally (particle SOA, canvas grid).</summary>
        Persistent
    }

    /// <summary>
    /// Central registry of every ComputeBuffer in the engine (spec section 8.1),
    /// keyed by the HLSL buffer name so PassManifest entries can be bound by name.
    /// Owns lifetime of buffers it creates; also supports logical aliases whose target
    /// swaps per frame (ping-pong) without re-registering metadata.
    /// </summary>
    public sealed class V4BufferRegistry : IDisposable
    {
        public readonly struct BufferInfo
        {
            public readonly int Count;
            public readonly int Stride;
            public readonly V4BufferLifetime Lifetime;

            public BufferInfo(int count, int stride, V4BufferLifetime lifetime)
            {
                Count = count;
                Stride = stride;
                Lifetime = lifetime;
            }

            public long Bytes => (long)Count * Stride;
        }

        private sealed class Entry
        {
            public ComputeBuffer Buffer;
            public BufferInfo Info;
            public bool Owned;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private bool _disposed;

        public ComputeBuffer Create(
            string name,
            int count,
            int stride,
            V4BufferLifetime lifetime,
            ComputeBufferType type = ComputeBufferType.Structured)
        {
            ThrowIfDisposed();
            if (_entries.ContainsKey(name))
            {
                throw new InvalidOperationException($"Buffer '{name}' is already registered.");
            }

            var buffer = new ComputeBuffer(count, stride, type);
            _entries[name] = new Entry
            {
                Buffer = buffer,
                Info = new BufferInfo(count, stride, lifetime),
                Owned = true
            };
            V4Log.Verbose(V4LogCategory.BufferBinding, $"created buffer name={name} count={count} stride={stride} lifetime={lifetime}");
            return buffer;
        }

        /// <summary>
        /// Registers (or retargets) a logical name pointing at an externally owned buffer.
        /// Used for ping-pong read/write aliases that swap targets each frame.
        /// </summary>
        public void Assign(string name, ComputeBuffer buffer, int count, int stride, V4BufferLifetime lifetime)
        {
            ThrowIfDisposed();
            if (_entries.TryGetValue(name, out Entry existing))
            {
                if (existing.Owned)
                {
                    throw new InvalidOperationException($"Buffer '{name}' is owned by the registry and cannot be reassigned.");
                }

                existing.Buffer = buffer;
                existing.Info = new BufferInfo(count, stride, lifetime);
                return;
            }

            _entries[name] = new Entry
            {
                Buffer = buffer,
                Info = new BufferInfo(count, stride, lifetime),
                Owned = false
            };
        }

        public ComputeBuffer Get(string name)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(name, out Entry entry))
            {
                throw new KeyNotFoundException($"Buffer '{name}' is not registered.");
            }

            return entry.Buffer;
        }

        public bool TryGet(string name, out ComputeBuffer buffer)
        {
            ThrowIfDisposed();
            if (_entries.TryGetValue(name, out Entry entry))
            {
                buffer = entry.Buffer;
                return true;
            }

            buffer = null;
            return false;
        }

        public bool Contains(string name) => !_disposed && _entries.ContainsKey(name);

        public BufferInfo GetInfo(string name)
        {
            ThrowIfDisposed();
            return _entries[name].Info;
        }

        public IEnumerable<string> Names => _entries.Keys;

        public long TotalOwnedBytes
        {
            get
            {
                long total = 0;
                foreach (Entry entry in _entries.Values)
                {
                    if (entry.Owned)
                    {
                        total += entry.Info.Bytes;
                    }
                }

                return total;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (Entry entry in _entries.Values)
            {
                if (entry.Owned)
                {
                    entry.Buffer?.Release();
                }
            }

            _entries.Clear();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(V4BufferRegistry));
            }
        }
    }
}
