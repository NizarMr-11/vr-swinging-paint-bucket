namespace HarmonicEngineV4.Networking.Serialization
{
    /// <summary>Primitive wire-type tags used in array heads and dictionary entries.</summary>
    public enum FieldType : byte
    {
        Int = 0,
        UInt = 1,
        Long = 2,
        ULong = 3,
        Float = 4,
        Double = 5,
        Bool = 6,
        String = 7,
        Byte = 8,
        DateTime = 9,
        Decimal = 10,
        Enum = 11,
        Vector3 = 12,
        Vector4 = 13,
        Object = 14
    }
}
