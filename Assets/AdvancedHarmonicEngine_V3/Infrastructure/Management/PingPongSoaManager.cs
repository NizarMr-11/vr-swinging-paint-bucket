namespace HarmonicEngine.Infrastructure.Management
{
    public sealed class PingPongSoaManager
    {
        private readonly ParticleSoaBuffers _setA;
        private readonly ParticleSoaBuffers _setB;
        private bool _isPingFrame = true;

        public PingPongSoaManager(ParticleSoaBuffers setA, ParticleSoaBuffers setB)
        {
            _setA = setA;
            _setB = setB;
        }

        public ParticleSoaBuffers ReadSet => _isPingFrame ? _setA : _setB;
        public ParticleSoaBuffers WriteSet => _isPingFrame ? _setB : _setA;

        public void BeginFrame()
        {
            WriteSet.SetCounterValue(0);
        }

        public void Swap()
        {
            _isPingFrame = !_isPingFrame;
        }
    }
}
