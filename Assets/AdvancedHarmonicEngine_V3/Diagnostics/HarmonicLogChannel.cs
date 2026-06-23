namespace HarmonicEngine.Diagnostics
{
    public enum HarmonicLogChannel
    {
        Session,
        Pipeline,
        Engine,
        Rain,
        Sph,
        Telemetry,
        Perf,
        Pbf
    }

    public static class HarmonicLogChannelExtensions
    {
        public static string FileName(this HarmonicLogChannel channel) =>
            channel switch
            {
                HarmonicLogChannel.Session => "session.log",
                HarmonicLogChannel.Pipeline => "pipeline.log",
                HarmonicLogChannel.Engine => "engine.log",
                HarmonicLogChannel.Rain => "rain.log",
                HarmonicLogChannel.Sph => "sph.log",
                HarmonicLogChannel.Telemetry => "telemetry.log",
                HarmonicLogChannel.Perf => "perf.log",
                HarmonicLogChannel.Pbf => "pbf.log",
                _ => "unknown.log"
            };
    }
}
