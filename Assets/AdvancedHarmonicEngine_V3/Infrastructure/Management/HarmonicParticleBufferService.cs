using HarmonicEngine.Domain.Models;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// CPU-side ingestion for external emitters (e.g. ParticleEmitter) into append buffers.
    /// </summary>
    public sealed class HarmonicParticleBufferService
    {
        private readonly ComputeBuffer _bufferA;
        private readonly ComputeBuffer _bufferB;
        private readonly PingPongCounterManager _pingPong;
        private readonly int _maxCapacity;
        private readonly ComputeBuffer _counterReadback;
        private readonly uint[] _counterScratch = new uint[1];
        private FluidParticle[] _ingestScratch;

        public HarmonicParticleBufferService(
            ComputeBuffer bufferA,
            ComputeBuffer bufferB,
            PingPongCounterManager pingPong,
            int maxCapacity,
            ComputeBuffer counterReadback)
        {
            _bufferA = bufferA;
            _bufferB = bufferB;
            _pingPong = pingPong;
            _maxCapacity = maxCapacity;
            _counterReadback = counterReadback;
        }

        public uint GetActiveCount()
        {
            ComputeBuffer source = _pingPong.ReadBuffer;
            ComputeBuffer.CopyCount(source, _counterReadback, 0);
            _counterReadback.GetData(_counterScratch);
            return _counterScratch[0];
        }

        public int AppendParticles(FluidParticle[] particles, int count)
        {
            if (particles == null || count <= 0)
            {
                return 0;
            }

            uint current = GetActiveCount();
            if (current >= _maxCapacity)
            {
                return 0;
            }

            int writable = Mathf.Min(count, _maxCapacity - (int)current);
            ComputeBuffer target = _pingPong.ReadBuffer;
            target.SetData(particles, 0, (int)current, writable);
            target.SetCounterValue(current + (uint)writable);
            return writable;
        }

        public FluidParticle[] RentScratch(int size)
        {
            if (_ingestScratch == null || _ingestScratch.Length < size)
            {
                _ingestScratch = new FluidParticle[size];
            }

            return _ingestScratch;
        }

        public void ClearAll()
        {
            _bufferA.SetCounterValue(0);
            _bufferB.SetCounterValue(0);
        }
    }
}
