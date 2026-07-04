using System;
using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Ping-pong particle SOA (plan Phase 2):
    ///   _Block0        float4(position.xyz, radius)
    ///   _Block1        float4(velocity.xyz, unused)
    ///   _PackedColors  uint RGBA8
    ///   _Flags         uint (V4ParticleFlags layout)
    /// Each frame reads one set and compacts survivors into the other, then swaps.
    /// Settled particles are simply not appended, so the live count shrinks (spec section 7).
    /// </summary>
    public sealed class V4ParticleSoa : IDisposable
    {
        public const string Block0Name = "_Block0";
        public const string Block1Name = "_Block1";
        public const string ColorsName = "_PackedColors";
        public const string FlagsName = "_Flags";
        public const string WriteBlock0Name = "_WriteBlock0";
        public const string WriteBlock1Name = "_WriteBlock1";
        public const string WriteColorsName = "_WritePackedColors";
        public const string WriteFlagsName = "_WriteFlags";

        private sealed class Set
        {
            public ComputeBuffer Block0;
            public ComputeBuffer Block1;
            public ComputeBuffer Colors;
            public ComputeBuffer Flags;

            public Set(int capacity)
            {
                Block0 = new ComputeBuffer(capacity, sizeof(float) * 4, ComputeBufferType.Structured);
                Block1 = new ComputeBuffer(capacity, sizeof(float) * 4, ComputeBufferType.Structured);
                Colors = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
                Flags = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
            }

            public void Release()
            {
                Block0?.Release();
                Block1?.Release();
                Colors?.Release();
                Flags?.Release();
                Block0 = Block1 = Colors = Flags = null;
            }
        }

        private Set _read;
        private Set _write;

        public int Capacity { get; }

        public ComputeBuffer ReadBlock0 => _read.Block0;
        public ComputeBuffer ReadBlock1 => _read.Block1;
        public ComputeBuffer ReadColors => _read.Colors;
        public ComputeBuffer ReadFlags => _read.Flags;
        public ComputeBuffer WriteBlock0 => _write.Block0;
        public ComputeBuffer WriteBlock1 => _write.Block1;
        public ComputeBuffer WriteColors => _write.Colors;
        public ComputeBuffer WriteFlags => _write.Flags;

        public V4ParticleSoa(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
            _read = new Set(Capacity);
            _write = new Set(Capacity);
        }

        /// <summary>Registers read/write aliases; call again after Swap (or use RefreshRegistry each frame).</summary>
        public void RegisterBuffers(V4BufferRegistry registry)
        {
            registry.Assign(Block0Name, _read.Block0, Capacity, sizeof(float) * 4, V4BufferLifetime.Persistent);
            registry.Assign(Block1Name, _read.Block1, Capacity, sizeof(float) * 4, V4BufferLifetime.Persistent);
            registry.Assign(ColorsName, _read.Colors, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            registry.Assign(FlagsName, _read.Flags, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            registry.Assign(WriteBlock0Name, _write.Block0, Capacity, sizeof(float) * 4, V4BufferLifetime.Persistent);
            registry.Assign(WriteBlock1Name, _write.Block1, Capacity, sizeof(float) * 4, V4BufferLifetime.Persistent);
            registry.Assign(WriteColorsName, _write.Colors, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            registry.Assign(WriteFlagsName, _write.Flags, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
        }

        public void Swap()
        {
            (_read, _write) = (_write, _read);
        }

        public void Upload(Vector4[] block0, Vector4[] block1, uint[] colors, uint[] flags, int count)
        {
            if (count > Capacity)
            {
                throw new ArgumentException($"count {count} exceeds capacity {Capacity}");
            }

            _read.Block0.SetData(block0, 0, 0, count);
            _read.Block1.SetData(block1, 0, 0, count);
            _read.Colors.SetData(colors, 0, 0, count);
            _read.Flags.SetData(flags, 0, 0, count);
        }

        public void Dispose()
        {
            _read?.Release();
            _write?.Release();
            _read = null;
            _write = null;
        }
    }

    /// <summary>
    /// GPU counter slot layout, shared between C# and V4Common.hlsl consumers.
    /// Slots 0..7 are global counters; slots 8..39 are per-hole ejected-this-frame counts.
    /// </summary>
    public static class V4Counters
    {
        public const string BufferName = "_Counters";
        public const int WriteCount = 0;
        public const int EscapedTotal = 1;
        public const int SettledTotal = 2;
        public const int TopBandCount = 3;
        public const int SplatEventCount = 4;
        public const int EjectedPerHoleBase = 8;
        public const int SlotCount = EjectedPerHoleBase + V4ParticleFlags.MaxHoles;
    }
}
