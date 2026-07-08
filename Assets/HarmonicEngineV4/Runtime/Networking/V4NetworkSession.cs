using System;
using System.IO;
using HarmonicEngineV4.Networking.Serialization;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Networking.Transport;

namespace HarmonicEngineV4.Networking
{
    public enum V4NetworkRole
    {
        Offline = 0,
        Host = 1,
        Client = 2
    }

    /// <summary>
    /// Milestone B session: authoritative topology on host, snapshot bootstrap, per-frame patches.
    /// Transport is injected; default tests use <see cref="V4LoopbackTransport"/>.
    /// </summary>
    public sealed class V4NetworkSession
    {
        private readonly DataAnnotationEmitter<Topology> _snapshotEmitter = new DataAnnotationEmitter<Topology>();

        public V4NetworkRole Role { get; private set; }
        public ulong SessionId { get; private set; }
        public Topology Authoritative { get; private set; }
        public IV4NetworkTransport Transport { get; set; }

        public void StartHost(ulong sessionId, Topology initialTopology, IV4NetworkTransport transport)
        {
            Role = V4NetworkRole.Host;
            SessionId = sessionId;
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Authoritative = initialTopology?.CloneTopology() ?? throw new ArgumentNullException(nameof(initialTopology));
        }

        public void StartClient(ulong sessionId, IV4NetworkTransport transport)
        {
            Role = V4NetworkRole.Client;
            SessionId = sessionId;
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Authoritative = null;
        }

        public byte[] BuildSnapshotPacket(Topology topology)
        {
            topology ??= Authoritative;
            if (topology == null)
            {
                throw new InvalidOperationException("No topology available for snapshot");
            }

            byte[] payload = SerializeSnapshot(topology);
            return V4NetworkPacket.Encode(new V4NetworkPacket(
                V4NetworkMessageType.Snapshot,
                SessionId,
                topology.frameIndex,
                payload));
        }

        public byte[] BuildPatchPacket(Topology previous, Topology current)
        {
            if (previous == null || current == null)
            {
                throw new ArgumentNullException(previous == null ? nameof(previous) : nameof(current));
            }

            Patch patch = Patch.CreateDelta(previous, current);
            byte[] payload = Patch.Serialize(patch, previous);
            return V4NetworkPacket.Encode(new V4NetworkPacket(
                V4NetworkMessageType.Patch,
                SessionId,
                current.frameIndex,
                payload));
        }

        public bool PollAndApplyIncoming()
        {
            if (Transport == null)
            {
                return false;
            }

            Transport.Poll();
            bool applied = false;
            while (Transport.TryReceive(out byte[] wire, out _))
            {
                ApplyIncoming(wire);
                applied = true;
            }

            return applied;
        }

        public void ApplyIncoming(byte[] wire)
        {
            V4NetworkPacket packet = V4NetworkPacket.Decode(wire);
            if (packet.SessionId != SessionId)
            {
                throw new InvalidDataException($"Session mismatch: expected {SessionId}, got {packet.SessionId}");
            }

            switch (packet.MessageType)
            {
                case V4NetworkMessageType.Snapshot:
                    Authoritative = DeserializeSnapshot(packet.Payload);
                    break;
                case V4NetworkMessageType.Patch:
                    if (Authoritative == null)
                    {
                        throw new InvalidOperationException("Client must receive snapshot before patches");
                    }

                    Authoritative = Patch.DeserializeAndApply(packet.Payload, Authoritative);
                    Authoritative.frameIndex = packet.FrameIndex;
                    break;
                case V4NetworkMessageType.Heartbeat:
                    break;
                default:
                    throw new InvalidDataException($"Unknown message type {(byte)packet.MessageType}");
            }
        }

        public void HostPublishSnapshot(Topology topology)
        {
            EnsureHost();
            Authoritative = topology.CloneTopology();
            Transport.Send(BuildSnapshotPacket(Authoritative));
        }

        public void HostPublishPatch(Topology nextTopology)
        {
            EnsureHost();
            Topology previous = Authoritative.CloneTopology();
            Authoritative = nextTopology.CloneTopology();
            Transport.Send(BuildPatchPacket(previous, Authoritative));
        }

        public byte[] SerializeSnapshot(Topology topology)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            _snapshotEmitter.Emit(writer, topology);
            return stream.ToArray();
        }

        public Topology DeserializeSnapshot(byte[] payload)
        {
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream);
            return _snapshotEmitter.Read(reader);
        }

        private void EnsureHost()
        {
            if (Role != V4NetworkRole.Host)
            {
                throw new InvalidOperationException("Host-only operation");
            }

            if (Transport == null)
            {
                throw new InvalidOperationException("Transport is not configured");
            }
        }
    }
}
