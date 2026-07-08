using System;
using System.IO;
using HarmonicEngineV4.Networking.Serialization;

namespace HarmonicEngineV4.Networking
{
    /// <summary>Length-prefixed network envelope around Milestone A serialization payloads.</summary>
    public readonly struct V4NetworkPacket
    {
        public const int HeaderSize = 1 + 8 + 4 + 4;

        public V4NetworkMessageType MessageType { get; }
        public ulong SessionId { get; }
        public uint FrameIndex { get; }
        public byte[] Payload { get; }

        public V4NetworkPacket(V4NetworkMessageType messageType, ulong sessionId, uint frameIndex, byte[] payload)
        {
            MessageType = messageType;
            SessionId = sessionId;
            FrameIndex = frameIndex;
            Payload = payload ?? Array.Empty<byte>();
        }

        public static byte[] Encode(V4NetworkPacket packet)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)packet.MessageType);
            writer.Write(packet.SessionId);
            writer.Write(packet.FrameIndex);
            writer.WriteVarInt(packet.Payload.Length);
            if (packet.Payload.Length > 0)
            {
                writer.Write(packet.Payload);
            }

            return stream.ToArray();
        }

        public static V4NetworkPacket Decode(byte[] wire)
        {
            if (wire == null || wire.Length < HeaderSize)
            {
                throw new InvalidDataException("Packet too short");
            }

            using var stream = new MemoryStream(wire);
            using var reader = new BinaryReader(stream);
            var messageType = (V4NetworkMessageType)reader.ReadByte();
            ulong sessionId = reader.ReadUInt64();
            uint frameIndex = reader.ReadUInt32();
            int payloadLength = (int)reader.ReadVarInt();
            byte[] payload = payloadLength > 0 ? reader.ReadBytes(payloadLength) : Array.Empty<byte>();
            return new V4NetworkPacket(messageType, sessionId, frameIndex, payload);
        }
    }
}
