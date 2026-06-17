using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public sealed class PingPongCounterManager
    {
        private readonly ComputeBuffer _bufferA;
        private readonly ComputeBuffer _bufferB;
        private bool _isPingFrame = true;

        public PingPongCounterManager(ComputeBuffer bufferA, ComputeBuffer bufferB)
        {
            _bufferA = bufferA;
            _bufferB = bufferB;
        }

        public ComputeBuffer ReadBuffer => _isPingFrame ? _bufferA : _bufferB;
        public ComputeBuffer WriteBuffer => _isPingFrame ? _bufferB : _bufferA;

        public void BeginFrame()
        {
            WriteBuffer.SetCounterValue(0);
        }

        public void Swap()
        {
            _isPingFrame = !_isPingFrame;
        }
    }
}
