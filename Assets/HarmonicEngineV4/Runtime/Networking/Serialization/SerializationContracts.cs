using System.IO;

namespace HarmonicEngineV4.Networking.Serialization
{
    public interface ISelfSerializable
    {
        void Read(BinaryReader reader);
        void Write(BinaryWriter writer);
    }

    public interface IPatchable
    {
        void Read(BinaryReader reader, IPatchable basis);
        void Write(BinaryWriter writer, IPatchable basis);
        IPatchable Clone();
    }

    public interface IMetadata
    {
        void Read(BinaryReader reader);
        void Write(BinaryWriter writer);
    }

    public interface IMetadataTarget
    {
        IMetadata Metadata { get; set; }
    }
}
