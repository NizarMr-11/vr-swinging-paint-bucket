using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonicEngineV4.Networking.State;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.Serialization
{
    public sealed class PatchEmitter
    {
        private readonly Type _type;

        public PatchEmitter(Type type) => _type = type;

        public void Emit(BinaryWriter writer, object target, IPatchable basis = null) =>
            Emit(writer, target, (object)basis);

        public void Emit(BinaryWriter writer, object target, object basis)
        {
            if (target == null)
            {
                writer.WriteVarInt(0);
                return;
            }

            if (target.GetType() == _type && target is IPatchable)
            {
                EmitObject(writer, target, basis);
                return;
            }

            if (target is IPatchable patchable)
            {
                patchable.Write(writer, basis as IPatchable);
                return;
            }

            if (target is ISelfSerializable selfSerializable)
            {
                selfSerializable.Write(writer);
                return;
            }

            EmitObject(writer, target, basis);
        }

        public object Read(BinaryReader reader, IPatchable basis = null) =>
            Read(reader, (object)basis);

        public object Read(BinaryReader reader, object basis)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return null;
            }

            if (typeof(IPatchable).IsAssignableFrom(_type))
            {
                if (_type == typeof(Patch))
                {
                    return ReadObject(reader, basis);
                }

                IPatchable patchBasis = basis as IPatchable;
                var patchable = (IPatchable)FormatterServices.GetUninitializedObject(_type);
                patchable.Read(reader, patchBasis);
                return patchable;
            }

            if (typeof(ISelfSerializable).IsAssignableFrom(_type))
            {
                var self = (ISelfSerializable)FormatterServices.GetUninitializedObject(_type);
                self.Read(reader);
                return self;
            }

            return ReadObject(reader, basis);
        }

        private void EmitObject(BinaryWriter writer, object target, object basis)
        {
            writer.WriteVarInt(1);
            writer.WriteSizePrefixedAnsi(target.GetType().AssemblyQualifiedName!);

            foreach (MemberInfo member in GetSortedMembers(_type))
            {
                if (member is PropertyInfo prop && !prop.CanRead)
                {
                    continue;
                }

                object value = GetMemberValue(member, target);
                object basisValue = GetBasisMemberValue(basis, member.Name);
                writer.WriteSizePrefixedAnsi(member.Name);

                if (member.GetCustomAttribute<MultiplexedArrayAttribute>() != null && value is Array array)
                {
                    EmitMultiplexedArray(writer, array, basisValue as Array);
                    continue;
                }

                EmitValue(writer, value, basisValue);
            }
        }

        private object ReadObject(BinaryReader reader, object basis)
        {
            if (reader.ReadVarInt() == 0)
            {
                return null;
            }

            string typeName = reader.ReadSizePrefixedAnsi();
            Type runtimeType = Type.GetType(typeName) ?? throw new InvalidDataException($"Unknown type {typeName}");
            object target = FormatterServices.GetUninitializedObject(runtimeType);

            foreach (MemberInfo member in GetSortedMembers(runtimeType))
            {
                if (member is PropertyInfo prop && !prop.CanWrite)
                {
                    continue;
                }

                string memberName = reader.ReadSizePrefixedAnsi();
                if (member.Name != memberName)
                {
                    throw new InvalidDataException($"Member order mismatch: expected {member.Name}, got {memberName}");
                }

                object basisValue = GetBasisMemberValue(basis, member.Name);
                if (member.GetCustomAttribute<MultiplexedArrayAttribute>() != null)
                {
                    SetMemberValue(member, target, ReadMultiplexedArray(reader, GetMemberType(member), basisValue as Array));
                    continue;
                }

                SetMemberValue(member, target, ReadValue(reader, GetMemberType(member), basisValue));
            }

            return target;
        }

        private static object GetBasisMemberValue(object basis, string memberName)
        {
            if (basis == null)
            {
                return null;
            }

            Type type = basis.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            return type.GetField(memberName, flags)?.GetValue(basis)
                ?? type.GetProperty(memberName, flags)?.GetValue(basis);
        }

        private static MemberInfo[] GetSortedMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            return type.GetFields(flags)
                .Cast<MemberInfo>()
                .Concat(type.GetProperties(flags))
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static Type GetMemberType(MemberInfo member) =>
            member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property => property.PropertyType,
                _ => throw new InvalidOperationException($"Unsupported member {member.Name}")
            };

        private static object GetMemberValue(MemberInfo member, object target) =>
            member switch
            {
                FieldInfo field => field.GetValue(target),
                PropertyInfo property => property.GetValue(target),
                _ => throw new InvalidOperationException($"Unsupported member {member.Name}")
            };

        private static void SetMemberValue(MemberInfo member, object target, object value)
        {
            switch (member)
            {
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
                case PropertyInfo property:
                    property.SetValue(target, value);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported member {member.Name}");
            }
        }

        private static MemberInfo[] GetSortedProperties(Type type) =>
            GetSortedMembers(type);

        private void EmitMultiplexedArray(BinaryWriter writer, Array array, Array basisArray)
        {
            writer.WriteVarInt(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                object element = array.GetValue(i);
                object basisElement = basisArray != null && i < basisArray.Length ? basisArray.GetValue(i) : null;
                EmitValue(writer, element, basisElement);
            }
        }

        private Array ReadMultiplexedArray(BinaryReader reader, Type propertyType, Array basisArray)
        {
            int length = (int)reader.ReadVarInt();
            Type elementType = propertyType.GetElementType()!;
            Array array = Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
            {
                object basisElement = basisArray != null && i < basisArray.Length ? basisArray.GetValue(i) : null;
                array.SetValue(ReadValue(reader, elementType, basisElement), i);
            }

            return array;
        }

        private void EmitValue(BinaryWriter writer, object value, object basisValue)
        {
            if (value == null)
            {
                writer.WriteVarInt(0);
                return;
            }

            writer.WriteVarInt(1);
            EmitTypedValue(writer, value, basisValue);
        }

        private object ReadValue(BinaryReader reader, Type type, object basisValue)
        {
            if (reader.ReadVarInt() == 0)
            {
                return null;
            }

            return ReadTypedValue(reader, type, basisValue);
        }

        private void EmitTypedValue(BinaryWriter writer, object value, object basisValue)
        {
            switch (value)
            {
                case float f:
                    Emit(writer, f, basisValue as float?);
                    break;
                case double d:
                    Emit(writer, d, basisValue as double?);
                    break;
                case decimal m:
                    Emit(writer, m, basisValue as decimal?);
                    break;
                case bool b:
                    Emit(writer, b, basisValue as bool?);
                    break;
                case string s:
                    Emit(writer, s);
                    break;
                case byte[] bytes:
                    Emit(writer, bytes);
                    break;
                case int i:
                    Emit(writer, i, basisValue as int?);
                    break;
                case uint ui:
                    Emit(writer, ui, basisValue as uint?);
                    break;
                case long l:
                    Emit(writer, l, basisValue as long?);
                    break;
                case ulong ul:
                    Emit(writer, ul, basisValue as ulong?);
                    break;
                case float3 f3:
                    Emit(writer, f3, basisValue is float3 b3 ? b3 : (float3?)null);
                    break;
                case float4 f4:
                    Emit(writer, f4, basisValue is float4 b4 ? b4 : (float4?)null);
                    break;
                case DateTime dt:
                    Emit(writer, dt);
                    break;
                case IPatchable patchable:
                    patchable.Write(writer, basisValue as IPatchable);
                    break;
                case IEnumerable enumerable when value is not string:
                    EmitEnumerable(writer, enumerable, basisValue as IEnumerable);
                    break;
                default:
                {
                    Type valueType = value.GetType();
                    if (valueType.IsEnum)
                    {
                        Emit(writer, (Enum)value, basisValue as Enum);
                        break;
                    }

                    if (valueType.IsClass && valueType != typeof(string))
                    {
                        using var nested = new MemoryStream();
                        using var nestedWriter = new BinaryWriter(nested);
                        var emitterType = typeof(DataAnnotationEmitter<>).MakeGenericType(valueType);
                        var emitter = Activator.CreateInstance(emitterType)!;
                        emitterType.GetMethod(nameof(Emit), new[] { typeof(BinaryWriter), typeof(object) })!
                            .Invoke(emitter, new[] { nestedWriter, value });
                        byte[] payload = nested.ToArray();
                        writer.WriteVarInt(payload.Length);
                        writer.Write(payload);
                        break;
                    }

                    new PatchEmitter(valueType).Emit(writer, value, basisValue as IPatchable);
                    break;
                }
            }
        }

        private object ReadTypedValue(BinaryReader reader, Type type, object basisValue)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(float))
            {
                return Read(reader, basisValue as float?);
            }

            if (type == typeof(double))
            {
                return Read(reader, basisValue as double?);
            }

            if (type == typeof(decimal))
            {
                return ReadDecimal(reader, basisValue as decimal?);
            }

            if (type == typeof(bool))
            {
                return reader.ReadBoolean();
            }

            if (type == typeof(string))
            {
                return reader.ReadString();
            }

            if (type == typeof(byte[]))
            {
                int length = (int)reader.ReadVarInt();
                return reader.ReadBytes(length);
            }

            if (type == typeof(int))
            {
                return Read(reader, basisValue as int?);
            }

            if (type == typeof(uint))
            {
                return ReadUInt(reader, basisValue as uint?);
            }

            if (type == typeof(long))
            {
                return Read(reader, basisValue as long?);
            }

            if (type == typeof(ulong))
            {
                return ReadULong(reader, basisValue as ulong?);
            }

            if (type == typeof(float3))
            {
                return Read(reader, basisValue is float3 f3 ? f3 : (float3?)null);
            }

            if (type == typeof(float4))
            {
                return Read(reader, basisValue is float4 f4 ? f4 : (float4?)null);
            }

            if (type == typeof(DateTime))
            {
                return DateTime.FromBinary(reader.ReadInt64());
            }

            if (typeof(IPatchable).IsAssignableFrom(type))
            {
                var patchable = (IPatchable)FormatterServices.GetUninitializedObject(type);
                patchable.Read(reader, basisValue as IPatchable);
                return patchable;
            }

            if (type.IsEnum)
            {
                return ReadEnum(reader, type, basisValue as Enum);
            }

            if (type.IsClass && type != typeof(string))
            {
                int payloadLength = (int)reader.ReadVarInt();
                byte[] payload = reader.ReadBytes(payloadLength);
                using var nested = new MemoryStream(payload);
                using var nestedReader = new BinaryReader(nested);
                var emitterType = typeof(DataAnnotationEmitter<>).MakeGenericType(type);
                var emitter = Activator.CreateInstance(emitterType)!;
                return emitterType.GetMethod(nameof(ReadObject), new[] { typeof(BinaryReader), typeof(Type) })!
                    .Invoke(emitter, new object[] { nestedReader, type })!;
            }

            return new PatchEmitter(type).Read(reader, basisValue as IPatchable);
        }

        private static void EmitEnumerable(BinaryWriter writer, IEnumerable values, IEnumerable basisValues)
        {
            var list = values.Cast<object>().ToList();
            writer.WriteVarInt(list.Count);
            var basisList = basisValues?.Cast<object>().ToList();
            for (int i = 0; i < list.Count; i++)
            {
                object basis = basisList != null && i < basisList.Count ? basisList[i] : null;
                EmitDictionaryOrObject(writer, list[i], basis);
            }
        }

        private static void EmitDictionaryOrObject(BinaryWriter writer, object value, object basis)
        {
            if (value is IDictionary dictionary)
            {
                writer.WriteVarInt(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                {
                    EmitDictionaryEntry(writer, entry.Key, entry.Value);
                }

                return;
            }

            new PatchEmitter(value.GetType()).Emit(writer, value, basis as IPatchable);
        }

        private static void EmitDictionaryEntry(BinaryWriter writer, object key, object value)
        {
            FieldType keyType = PrimitiveEmitter.GetFieldType(key.GetType());
            FieldType valueType = PrimitiveEmitter.GetFieldType(value.GetType());
            writer.Write((byte)keyType);
            writer.Write((byte)valueType);
            PrimitiveEmitter.Emit(writer, key);
            PrimitiveEmitter.Emit(writer, value);
        }

        private static void Emit(BinaryWriter writer, float target, float? basis = null)
        {
            if (basis == null)
            {
                writer.Write(target);
            }
            else
            {
                writer.Write(target - basis.Value);
            }
        }

        private static float Read(BinaryReader reader, float? basis = null) =>
            basis == null ? reader.ReadSingle() : basis.Value + reader.ReadSingle();

        private static void Emit(BinaryWriter writer, double target, double? basis = null)
        {
            if (basis == null)
            {
                writer.Write(target);
            }
            else
            {
                writer.Write(target - basis.Value);
            }
        }

        private static double Read(BinaryReader reader, double? basis = null) =>
            basis == null ? reader.ReadDouble() : basis.Value + reader.ReadDouble();

        private static void Emit(BinaryWriter writer, decimal target, decimal? basis = null)
        {
            if (basis == null)
            {
                int[] bits = decimal.GetBits(target);
                writer.Write(bits[0]);
                writer.Write(bits[1]);
                writer.Write(bits[2]);
                writer.Write(bits[3]);
            }
            else
            {
                Emit(writer, target - basis.Value);
            }
        }

        private static decimal ReadDecimal(BinaryReader reader, decimal? basis = null)
        {
            if (basis == null)
            {
                return new decimal(new[]
                {
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()
                });
            }

            return basis.Value + reader.ReadDecimal();
        }

        private static void Emit(BinaryWriter writer, bool target, bool? basis = null) => writer.Write(target);

        private static void Emit(BinaryWriter writer, string target) => writer.WriteString(target);

        private static void Emit(BinaryWriter writer, byte[] target)
        {
            writer.WriteVarInt(target.Length);
            writer.Write(target);
        }

        private static void Emit(BinaryWriter writer, int target, int? basis = null)
        {
            if (basis == null)
            {
                writer.WriteVarInt(target);
            }
            else
            {
                writer.WriteVarInt(target - basis.Value);
            }
        }

        private static int Read(BinaryReader reader, int? basis = null) =>
            basis == null ? (int)reader.ReadVarInt() : basis.Value + (int)reader.ReadVarInt();

        private static void Emit(BinaryWriter writer, uint target, uint? basis = null)
        {
            if (basis == null)
            {
                writer.WriteVarInt(target);
            }
            else
            {
                writer.WriteVarInt((long)target - basis.Value);
            }
        }

        private static uint ReadUInt(BinaryReader reader, uint? basis = null) =>
            basis == null ? (uint)reader.ReadVarInt() : (uint)(basis.Value + reader.ReadVarInt());

        private static void Emit(BinaryWriter writer, long target, long? basis = null)
        {
            if (basis == null)
            {
                writer.WriteVarInt(target);
            }
            else
            {
                writer.WriteVarInt(target - basis.Value);
            }
        }

        private static long Read(BinaryReader reader, long? basis = null) =>
            basis == null ? reader.ReadVarInt() : basis.Value + reader.ReadVarInt();

        private static void Emit(BinaryWriter writer, ulong target, ulong? basis = null)
        {
            if (basis == null)
            {
                writer.WriteVarInt((long)target);
            }
            else
            {
                writer.WriteVarInt((long)target - (long)basis.Value);
            }
        }

        private static ulong ReadULong(BinaryReader reader, ulong? basis = null) =>
            basis == null ? (ulong)reader.ReadVarInt() : (ulong)((long)basis.Value + reader.ReadVarInt());

        private static void Emit(BinaryWriter writer, Enum target, Enum basis = null)
        {
            if (basis == null)
            {
                writer.WriteVarInt(Convert.ToInt64(target));
            }
            else
            {
                writer.WriteVarInt(Convert.ToInt64(target) - Convert.ToInt64(basis));
            }
        }

        private static Enum ReadEnum(BinaryReader reader, Type enumType, Enum basis = null)
        {
            long delta = reader.ReadVarInt();
            long value = basis == null ? delta : Convert.ToInt64(basis) + delta;
            return (Enum)Enum.ToObject(enumType, value);
        }

        private static void Emit(BinaryWriter writer, float3 target, float3? basis = null)
        {
            if (basis == null)
            {
                writer.Write(target.x);
                writer.Write(target.y);
                writer.Write(target.z);
            }
            else
            {
                writer.Write(target.x - basis.Value.x);
                writer.Write(target.y - basis.Value.y);
                writer.Write(target.z - basis.Value.z);
            }
        }

        private static float3 Read(BinaryReader reader, float3? basis = null)
        {
            if (basis == null)
            {
                return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            return basis.Value + new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void Emit(BinaryWriter writer, float4 target, float4? basis = null)
        {
            if (basis == null)
            {
                writer.Write(target.x);
                writer.Write(target.y);
                writer.Write(target.z);
                writer.Write(target.w);
            }
            else
            {
                writer.Write(target.x - basis.Value.x);
                writer.Write(target.y - basis.Value.y);
                writer.Write(target.z - basis.Value.z);
                writer.Write(target.w - basis.Value.w);
            }
        }

        private static float4 Read(BinaryReader reader, float4? basis = null)
        {
            if (basis == null)
            {
                return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            return basis.Value + new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void Emit(BinaryWriter writer, DateTime target) => writer.Write(target.ToBinary());
    }
}
