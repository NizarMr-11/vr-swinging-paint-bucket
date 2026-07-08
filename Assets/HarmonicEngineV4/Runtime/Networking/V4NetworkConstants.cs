namespace HarmonicEngineV4.Networking
{
    public enum V4NetworkMessageType : byte
    {
        Snapshot = 1,
        Patch = 2,
        Heartbeat = 3
    }

    public static class V4NetworkConstants
    {
        public const int SchemaVersion = 1;
        public const int ProtocolVersion = 1;
        public const int DefaultUdpPort = 17777;
    }
}
