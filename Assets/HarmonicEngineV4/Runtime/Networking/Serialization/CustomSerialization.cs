using System;

namespace HarmonicEngineV4.Networking.Serialization
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DataMemberAttribute : Attribute
    {
        public bool EmitDefaultValue { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class DataArraySizeAttribute : Attribute
    {
        public DataArraySizeAttribute(int size) => Size = size;
        public int Size { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class PrimitiveSubTypeAttribute : Attribute
    {
        public PrimitiveSubTypeAttribute(FieldType fieldType) => FieldType = fieldType;
        public FieldType FieldType { get; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class MultiplexedArrayAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MetadataTypeAttribute : Attribute
    {
        public MetadataTypeAttribute(Type metadataType) => MetadataType = metadataType;
        public Type MetadataType { get; }
    }
}
