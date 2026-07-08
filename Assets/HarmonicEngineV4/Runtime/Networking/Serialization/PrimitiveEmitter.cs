using System;
using System.IO;
using System.Runtime.Serialization;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.Serialization
{
    public static class PrimitiveEmitter
    {
        public static void Emit(BinaryWriter writer, object value)
        {
            Emit(writer, value, value?.GetType() ?? typeof(object));
        }

        public static void Emit(BinaryWriter writer, object value, Type type)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
            {
                writer.Write((bool)value);
                return;
            }

            if (type == typeof(byte))
            {
                writer.Write((byte)value);
                return;
            }

            if (type == typeof(int))
            {
                writer.Write((int)value);
                return;
            }

            if (type == typeof(uint))
            {
                writer.Write((uint)value);
                return;
            }

            if (type == typeof(long))
            {
                writer.Write((long)value);
                return;
            }

            if (type == typeof(ulong))
            {
                writer.Write((ulong)value);
                return;
            }

            if (type == typeof(float))
            {
                writer.Write((float)value);
                return;
            }

            if (type == typeof(double))
            {
                writer.Write((double)value);
                return;
            }

            if (type == typeof(string))
            {
                writer.WriteString((string)value);
                return;
            }

            if (type == typeof(DateTime))
            {
                writer.Write(((DateTime)value).ToBinary());
                return;
            }

            if (type == typeof(decimal))
            {
                int[] bits = decimal.GetBits((decimal)value);
                writer.Write(bits[0]);
                writer.Write(bits[1]);
                writer.Write(bits[2]);
                writer.Write(bits[3]);
                return;
            }

            if (type == typeof(float3))
            {
                var v = (float3)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                return;
            }

            if (type == typeof(float4))
            {
                var v = (float4)value;
                writer.Write(v.x);
                writer.Write(v.y);
                writer.Write(v.z);
                writer.Write(v.w);
                return;
            }

            if (type.IsEnum)
            {
                writer.WriteVarInt(Convert.ToInt64(value));
                return;
            }

            if (type == typeof(byte[]))
            {
                var bytes = (byte[])value;
                writer.WriteVarInt(bytes.Length);
                writer.Write(bytes);
                return;
            }

            if (type.IsArray)
            {
                EmitArray(writer, (Array)value, type.GetElementType()!);
                return;
            }

            throw new NotSupportedException($"PrimitiveEmitter cannot emit type {type.FullName}");
        }

        public static object Read(BinaryReader reader, Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
            {
                return reader.ReadBoolean();
            }

            if (type == typeof(byte))
            {
                return reader.ReadByte();
            }

            if (type == typeof(int))
            {
                return reader.ReadInt32();
            }

            if (type == typeof(uint))
            {
                return reader.ReadUInt32();
            }

            if (type == typeof(long))
            {
                return reader.ReadInt64();
            }

            if (type == typeof(ulong))
            {
                return reader.ReadUInt64();
            }

            if (type == typeof(float))
            {
                return reader.ReadSingle();
            }

            if (type == typeof(double))
            {
                return reader.ReadDouble();
            }

            if (type == typeof(string))
            {
                return reader.ReadString();
            }

            if (type == typeof(DateTime))
            {
                return DateTime.FromBinary(reader.ReadInt64());
            }

            if (type == typeof(decimal))
            {
                return new decimal(new[]
                {
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()
                });
            }

            if (type == typeof(float3))
            {
                return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            if (type == typeof(float4))
            {
                return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            if (type.IsEnum)
            {
                return Enum.ToObject(type, reader.ReadVarInt());
            }

            if (type == typeof(byte[]))
            {
                int length = (int)reader.ReadVarInt();
                return reader.ReadBytes(length);
            }

            if (type.IsArray)
            {
                return ReadArray(reader, type);
            }

            throw new NotSupportedException($"PrimitiveEmitter cannot read type {type.FullName}");
        }

        public static FieldType GetFieldType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
            {
                return FieldType.Bool;
            }

            if (type == typeof(byte))
            {
                return FieldType.Byte;
            }

            if (type == typeof(int))
            {
                return FieldType.Int;
            }

            if (type == typeof(uint))
            {
                return FieldType.UInt;
            }

            if (type == typeof(long))
            {
                return FieldType.Long;
            }

            if (type == typeof(ulong))
            {
                return FieldType.ULong;
            }

            if (type == typeof(float))
            {
                return FieldType.Float;
            }

            if (type == typeof(double))
            {
                return FieldType.Double;
            }

            if (type == typeof(string))
            {
                return FieldType.String;
            }

            if (type == typeof(DateTime))
            {
                return FieldType.DateTime;
            }

            if (type == typeof(decimal))
            {
                return FieldType.Decimal;
            }

            if (type == typeof(float3))
            {
                return FieldType.Vector3;
            }

            if (type == typeof(float4))
            {
                return FieldType.Vector4;
            }

            if (type.IsEnum)
            {
                return FieldType.Enum;
            }

            return FieldType.Object;
        }

        private static void EmitArray(BinaryWriter writer, Array array, Type elementType)
        {
            writer.WriteDataAnnotationHead(GetFieldType(elementType), array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                Emit(writer, array.GetValue(i), elementType);
            }
        }

        private static Array ReadArray(BinaryReader reader, Type arrayType)
        {
            reader.ReadDataAnnotationHead(out FieldType fieldType, out int length);
            Type elementType = arrayType.GetElementType()!;
            if (GetFieldType(elementType) != fieldType)
            {
                throw new InvalidDataException($"Array element type mismatch: expected {fieldType}, wire {fieldType}");
            }

            Array array = Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
            {
                array.SetValue(Read(reader, elementType), i);
            }

            return array;
        }
    }
}
