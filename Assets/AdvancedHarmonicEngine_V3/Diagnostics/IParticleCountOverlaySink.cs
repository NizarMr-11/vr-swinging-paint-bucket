namespace HarmonicEngine.Diagnostics
{
    public interface IParticleCountOverlaySink
    {
        void SetPeak(uint peak);
        void SetLastActive(uint active);
    }
}
