namespace HarmonicEngineV4.Networking.Serialization
{
    public enum DataAnnotationOpCode : byte
    {
        ObjectBegin = 1,
        ObjectEnd = 2,
        Member = 3,
        SubType = 4,
        Metadata = 5,
        ResizeData = 6
    }
}
