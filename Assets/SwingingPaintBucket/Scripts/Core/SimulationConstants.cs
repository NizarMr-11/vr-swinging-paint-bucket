
namespace SwingingPaintBucket.Core
{
    public static class SimulationConstants
    {
        
        public const float DefaultGravity = 9.81f;

        
        public const float FixedTimeStep = 0.02f;

        
        public const float DefaultDischargeCoefficent = 0.7f;

        // last VOLUME before stopping
        public const float MinPaintVolume = 0.0001f;

        // last paint HEIGHT before stopping
        public const float MinPaintHeight = 0.001f;
    }
}
