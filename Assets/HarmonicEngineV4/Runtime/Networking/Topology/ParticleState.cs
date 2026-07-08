using System;
using System.IO;
using HarmonicEngineV4.Networking.Serialization;
using Unity.Mathematics;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class ParticleState : IPatchable, IEquatable<ParticleState>
    {
        public float eyeDepth;
        public float3 position;
        public float4 velocityColor;
        public float radius;
        public uint packedColor;
        public uint flags;
        public bool hasEscaped;

        public void Write(BinaryWriter writer, IPatchable basis)
        {
            var basisParticle = basis as ParticleState;
            EmitFloat(writer, eyeDepth, basisParticle?.eyeDepth);
            EmitFloat3(writer, position, basisParticle?.position);
            EmitFloat4(writer, velocityColor, basisParticle?.velocityColor);
            EmitFloat(writer, radius, basisParticle?.radius);
            EmitUInt(writer, packedColor, basisParticle?.packedColor);
            EmitUInt(writer, flags, basisParticle?.flags);
            writer.Write(hasEscaped);
        }

        public void Read(BinaryReader reader, IPatchable basis)
        {
            var basisParticle = basis as ParticleState;
            eyeDepth = ReadFloat(reader, basisParticle?.eyeDepth);
            position = ReadFloat3(reader, basisParticle?.position);
            velocityColor = ReadFloat4(reader, basisParticle?.velocityColor);
            radius = ReadFloat(reader, basisParticle?.radius);
            packedColor = ReadUInt(reader, basisParticle?.packedColor);
            flags = ReadUInt(reader, basisParticle?.flags);
            hasEscaped = reader.ReadBoolean();
        }

        public IPatchable Clone() => new ParticleState
        {
            eyeDepth = eyeDepth,
            position = position,
            velocityColor = velocityColor,
            radius = radius,
            packedColor = packedColor,
            flags = flags,
            hasEscaped = hasEscaped
        };

        public ParticleState Apply(ParticleState basis) => new ParticleState
        {
            eyeDepth = eyeDepth,
            position = position,
            velocityColor = velocityColor,
            radius = radius,
            packedColor = packedColor,
            flags = flags,
            hasEscaped = hasEscaped
        };

        public bool Equals(ParticleState other)
        {
            if (other == null)
            {
                return false;
            }

            return eyeDepth.Equals(other.eyeDepth)
                && position.Equals(other.position)
                && velocityColor.Equals(other.velocityColor)
                && radius.Equals(other.radius)
                && packedColor == other.packedColor
                && flags == other.flags
                && hasEscaped == other.hasEscaped;
        }

        public override bool Equals(object obj) => Equals(obj as ParticleState);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = eyeDepth.GetHashCode();
                hash = (hash * 397) ^ position.GetHashCode();
                hash = (hash * 397) ^ velocityColor.GetHashCode();
                hash = (hash * 397) ^ radius.GetHashCode();
                hash = (hash * 397) ^ (int)packedColor;
                hash = (hash * 397) ^ (int)flags;
                hash = (hash * 397) ^ hasEscaped.GetHashCode();
                return hash;
            }
        }

        private static void EmitFloat(BinaryWriter writer, float value, float? basis)
        {
            if (basis == null)
            {
                writer.Write(value);
            }
            else
            {
                writer.Write(value - basis.Value);
            }
        }

        private static float ReadFloat(BinaryReader reader, float? basis) =>
            basis == null ? reader.ReadSingle() : basis.Value + reader.ReadSingle();

        private static void EmitUInt(BinaryWriter writer, uint value, uint? basis)
        {
            if (basis == null)
            {
                writer.WriteVarInt(value);
            }
            else
            {
                writer.WriteVarInt((long)value - basis.Value);
            }
        }

        private static uint ReadUInt(BinaryReader reader, uint? basis) =>
            basis == null ? (uint)reader.ReadVarInt() : (uint)(basis.Value + reader.ReadVarInt());

        private static void EmitFloat3(BinaryWriter writer, float3 value, float3? basis)
        {
            if (basis == null)
            {
                writer.Write(value.x);
                writer.Write(value.y);
                writer.Write(value.z);
            }
            else
            {
                writer.Write(value.x - basis.Value.x);
                writer.Write(value.y - basis.Value.y);
                writer.Write(value.z - basis.Value.z);
            }
        }

        private static float3 ReadFloat3(BinaryReader reader, float3? basis)
        {
            if (basis == null)
            {
                return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            return basis.Value + new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void EmitFloat4(BinaryWriter writer, float4 value, float4? basis)
        {
            if (basis == null)
            {
                writer.Write(value.x);
                writer.Write(value.y);
                writer.Write(value.z);
                writer.Write(value.w);
            }
            else
            {
                writer.Write(value.x - basis.Value.x);
                writer.Write(value.y - basis.Value.y);
                writer.Write(value.z - basis.Value.z);
                writer.Write(value.w - basis.Value.w);
            }
        }

        private static float4 ReadFloat4(BinaryReader reader, float4? basis)
        {
            if (basis == null)
            {
                return new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            return basis.Value + new float4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
