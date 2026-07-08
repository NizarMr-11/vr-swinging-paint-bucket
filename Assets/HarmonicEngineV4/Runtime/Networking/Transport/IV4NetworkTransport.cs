using System.Collections.Generic;

namespace HarmonicEngineV4.Networking.Transport
{
    public interface IV4NetworkTransport
    {
        void Poll();
        void Send(byte[] packet, byte channel = 0);
        bool TryReceive(out byte[] packet, out byte channel);
    }

    /// <summary>In-process transport for EditMode tests and local host/client pairs.</summary>
    public sealed class V4LoopbackTransport : IV4NetworkTransport
    {
        private readonly Queue<byte[]> _outbound = new Queue<byte[]>();
        private readonly Queue<byte[]> _inbound = new Queue<byte[]>();

        public V4LoopbackTransport Peer { get; set; }

        public void Poll()
        {
        }

        public void Send(byte[] packet, byte channel = 0)
        {
            if (packet == null || packet.Length == 0)
            {
                return;
            }

            var copy = new byte[packet.Length];
            System.Buffer.BlockCopy(packet, 0, copy, 0, packet.Length);
            if (Peer != null)
            {
                Peer._inbound.Enqueue(copy);
            }
            else
            {
                _outbound.Enqueue(copy);
            }
        }

        public bool TryReceive(out byte[] packet, out byte channel)
        {
            channel = 0;
            if (_inbound.Count == 0)
            {
                packet = null;
                return false;
            }

            packet = _inbound.Dequeue();
            return true;
        }

        public void PairWith(V4LoopbackTransport peer)
        {
            Peer = peer;
            peer.Peer = this;
        }
    }
}
