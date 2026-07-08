using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace HarmonicEngineV4.Networking.Serialization
{
    public sealed class DataAnnotationEmitter<T> where T : class
    {
        private readonly Type _type;

        public DataAnnotationEmitter() => _type = typeof(T);

        public DataAnnotationEmitter(Type type) => _type = type;

        public void Emit(BinaryWriter writer, T target) => Emit(writer, (object)target);

        public void Emit(BinaryWriter writer, object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Type runtimeType = target.GetType();
            writer.WriteVarInt((long)DataAnnotationOpCode.ObjectBegin);
            writer.WriteSizePrefixedAnsi(runtimeType.AssemblyQualifiedName!);

            if (runtimeType.GetCustomAttribute<MetadataTypeAttribute>()?.MetadataType is Type metadataType
                && target is IMetadataTarget metadataTarget
                && metadataTarget.Metadata != null)
            {
                writer.WriteVarInt((long)DataAnnotationOpCode.Metadata);
                metadataTarget.Metadata.Write(writer);
            }

            foreach (MemberInfo member in GetMembersWithResizedArrays(runtimeType))
            {
                var sizeAttribute = member.GetCustomAttribute<DataArraySizeAttribute>()!;
                writer.WriteVarInt((long)DataAnnotationOpCode.ResizeData);
                writer.WriteSizePrefixedAnsi(member.Name);
                writer.WriteVarInt(sizeAttribute.Size);
            }

            foreach (MemberInfo member in GetEmitMembers(runtimeType, target))
            {
                writer.WriteVarInt((long)DataAnnotationOpCode.Member);
                writer.WriteSizePrefixedAnsi(member.Name);
                PrimitiveEmitter.Emit(writer, GetMemberValue(member, target)!);
            }

            foreach (MemberInfo member in GetSubTypeMembers(runtimeType))
            {
                writer.WriteVarInt((long)DataAnnotationOpCode.SubType);
                writer.WriteSizePrefixedAnsi(member.Name);
                Type memberType = GetMemberType(member);
                object value = GetMemberValue(member, target);
                if (memberType.IsArray)
                {
                    EmitComplexArray(writer, (Array)value, memberType.GetElementType()!);
                }
                else
                {
                    EmitComplex(writer, value);
                }
            }

            writer.WriteVarInt((long)DataAnnotationOpCode.ObjectEnd);
        }

        public T Read(BinaryReader reader) => (T)ReadObject(reader, typeof(T));

        public object ReadObject(BinaryReader reader, Type expectedType)
        {
            if (reader.ReadVarInt() != (long)DataAnnotationOpCode.ObjectBegin)
            {
                throw new InvalidDataException("ObjectBegin expected");
            }

            string typeName = reader.ReadSizePrefixedAnsi();
            Type runtimeType = Type.GetType(typeName) ?? throw new InvalidDataException($"Unknown type {typeName}");
            if (!expectedType.IsAssignableFrom(runtimeType))
            {
                throw new InvalidDataException($"Type {runtimeType.FullName} is not assignable to {expectedType.FullName}");
            }

            object target = FormatterServices.GetUninitializedObject(runtimeType);

            if (runtimeType.GetCustomAttribute<MetadataTypeAttribute>()?.MetadataType is Type metadataType)
            {
                if (reader.ReadVarInt() != (long)DataAnnotationOpCode.Metadata)
                {
                    throw new InvalidDataException("Metadata expected");
                }

                var metadata = (IMetadata)FormatterServices.GetUninitializedObject(metadataType);
                metadata.Read(reader);
                if (target is IMetadataTarget metadataTarget)
                {
                    metadataTarget.Metadata = metadata;
                }
            }

            while (true)
            {
                long opcode = reader.ReadVarInt();
                if (opcode == (long)DataAnnotationOpCode.ObjectEnd)
                {
                    break;
                }

                if (opcode == (long)DataAnnotationOpCode.ResizeData)
                {
                    string memberName = reader.ReadSizePrefixedAnsi();
                    int size = (int)reader.ReadVarInt();
                    MemberInfo member = GetDeclaredMember(runtimeType, memberName);
                    Type elementType = GetMemberType(member).GetElementType()!;
                    SetMemberValue(member, target, Array.CreateInstance(elementType, size));
                    continue;
                }

                string memberId = reader.ReadSizePrefixedAnsi();
                MemberInfo memberInfo = GetDeclaredMember(runtimeType, memberId)
                    ?? throw new InvalidDataException($"Unknown member {memberId}");

                if (opcode == (long)DataAnnotationOpCode.SubType)
                {
                    Type memberType = GetMemberType(memberInfo);
                    if (memberType.IsArray)
                    {
                        SetMemberValue(memberInfo, target, ReadComplexArray(reader, memberType));
                    }
                    else
                    {
                        SetMemberValue(memberInfo, target, ReadComplex(reader, memberType));
                    }

                    continue;
                }

                SetMemberValue(memberInfo, target, PrimitiveEmitter.Read(reader, GetMemberType(memberInfo)));
            }

            return target;
        }

        private static void EmitComplexArray(BinaryWriter writer, Array array, Type elementType)
        {
            if (array == null)
            {
                writer.WriteVarInt(-1);
                return;
            }

            writer.WriteVarInt(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                EmitComplex(writer, array.GetValue(i));
            }
        }

        private static Array ReadComplexArray(BinaryReader reader, Type arrayType)
        {
            int length = (int)reader.ReadVarInt();
            if (length < 0)
            {
                return null;
            }

            Type elementType = arrayType.GetElementType()!;
            Array array = Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
            {
                array.SetValue(ReadComplex(reader, elementType), i);
            }

            return array;
        }

        private static void EmitComplex(BinaryWriter writer, object value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
                return;
            }

            writer.Write((byte)1);
            var emitterType = typeof(DataAnnotationEmitter<>).MakeGenericType(value.GetType());
            var emitter = Activator.CreateInstance(emitterType)!;
            emitterType.GetMethod(nameof(Emit), new[] { typeof(BinaryWriter), typeof(object) })!
                .Invoke(emitter, new[] { writer, value });
        }

        private static object ReadComplex(BinaryReader reader, Type type)
        {
            if (reader.ReadByte() == 0)
            {
                return null;
            }

            var emitterType = typeof(DataAnnotationEmitter<>).MakeGenericType(type);
            var emitter = Activator.CreateInstance(emitterType)!;
            return emitterType.GetMethod(nameof(ReadObject), new[] { typeof(BinaryReader), typeof(Type) })!
                .Invoke(emitter, new object[] { reader, type })!;
        }

        private static MemberInfo[] GetMembersWithResizedArrays(Type runtimeType) =>
            GetDeclaredMembers(runtimeType)
                .Where(m => m.GetCustomAttribute<DataArraySizeAttribute>() != null)
                .ToArray();

        private static MemberInfo[] GetSubTypeMembers(Type runtimeType) =>
            GetDeclaredMembers(runtimeType)
                .Where(m => m.GetCustomAttribute<PrimitiveSubTypeAttribute>() != null)
                .ToArray();

        private static MemberInfo[] GetEmitMembers(Type runtimeType, object target) =>
            GetDeclaredMembers(runtimeType)
                .Where(m => m.GetCustomAttribute<PrimitiveSubTypeAttribute>() == null)
                .Where(m =>
                {
                    DataMemberAttribute memberAttribute = m.GetCustomAttribute<DataMemberAttribute>();
                    if (memberAttribute != null && !memberAttribute.EmitDefaultValue)
                    {
                        object value = GetMemberValue(m, target);
                        object defaultValue = GetMemberType(m).IsValueType ? Activator.CreateInstance(GetMemberType(m))! : null;
                        if (Equals(value, defaultValue))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .ToArray();

        private static IEnumerable<MemberInfo> GetDeclaredMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            foreach (FieldInfo field in type.GetFields(flags))
            {
                yield return field;
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                yield return property;
            }
        }

        private static MemberInfo GetDeclaredMember(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            return (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
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
    }
}
