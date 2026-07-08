using System;
using System.IO;
using System.Text;

namespace HarmonicEngineV4.Networking.Serialization
{
    public static class BinaryWriterExtensions
    {
        public static void WriteVarInt(this BinaryWriter writer, long value)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            while (zigzag >= 0x80)
            {
                writer.Write((byte)(zigzag | 0x80));
                zigzag >>= 7;
            }

            writer.Write((byte)zigzag);
        }

        public static void WriteSizePrefixedAnsi(this BinaryWriter writer, string value)
        {
            value ??= string.Empty;
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.WriteVarInt(bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteDataAnnotationHead(this BinaryWriter writer, FieldType fieldType, int elementCount)
        {
            writer.Write((byte)fieldType);
            writer.WriteVarInt(elementCount);
        }

        public static void WriteString(this BinaryWriter writer, string value)
        {
            writer.WriteSizePrefixedAnsi(value);
        }
    }

    public static class BinaryReaderExtensions
    {
        public static long ReadVarInt(this BinaryReader reader)
        {
            ulong result = 0;
            int shift = 0;
            while (true)
            {
                byte b = reader.ReadByte();
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            return (long)((result >> 1) ^ (ulong)(-(long)(result & 1)));
        }

        public static string ReadSizePrefixedAnsi(this BinaryReader reader)
        {
            int length = (int)reader.ReadVarInt();
            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void ReadDataAnnotationHead(this BinaryReader reader, out FieldType fieldType, out int elementCount)
        {
            fieldType = (FieldType)reader.ReadByte();
            elementCount = (int)reader.ReadVarInt();
        }

        public static string ReadString(this BinaryReader reader) => reader.ReadSizePrefixedAnsi();
    }
}
