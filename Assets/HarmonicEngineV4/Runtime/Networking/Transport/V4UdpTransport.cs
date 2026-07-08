using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using HarmonicEngineV4.Networking;
using UnityEngine;

namespace HarmonicEngineV4.Networking.Transport
{
    /// <summary>
    /// Milestone D UDP transport over <see cref="UdpClient"/>.
    /// Host binds and learns the client endpoint from the first hello/heartbeat datagram.
    /// Client sends to a fixed host address/port and calls <see cref="EnsureHelloSent"/>.
    /// </summary>
    public sealed class V4UdpTransport : IV4NetworkTransport, IDisposable
    {
        public const int MaxDatagramBytes = 60000;

        private enum UdpRole
        {
            Host,
            Client
        }

        private readonly UdpRole _role;
        private readonly UdpClient _client;
        private readonly IPEndPoint _hostEndPoint;
        private readonly Queue<QueuedPacket> _inbound = new Queue<QueuedPacket>();

        private IPEndPoint _clientEndPoint;
        private bool _helloSent;
        private bool _disposed;

        public int BoundPort { get; }

        private V4UdpTransport(UdpRole role, UdpClient client, IPEndPoint hostEndPoint, int boundPort)
        {
            _role = role;
            _client = client;
            _hostEndPoint = hostEndPoint;
            BoundPort = boundPort;
        }

        public static V4UdpTransport CreateHost(int port = 0)
        {
            var client = new UdpClient(port);
            var local = (IPEndPoint)client.Client.LocalEndPoint;
            return new V4UdpTransport(UdpRole.Host, client, null, local.Port);
        }

        public static V4UdpTransport CreateClient(string hostAddress, int port)
        {
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("Host address is required", nameof(hostAddress));
            }

            if (!IPAddress.TryParse(hostAddress, out IPAddress address))
            {
                address = Dns.GetHostAddresses(hostAddress)[0];
            }

            var hostEndPoint = new IPEndPoint(address, port);
            var client = new UdpClient();
            client.Connect(hostEndPoint);
            return new V4UdpTransport(UdpRole.Client, client, hostEndPoint, 0);
        }

        public void EnsureHelloSent(ulong sessionId)
        {
            if (_role != UdpRole.Client || _helloSent)
            {
                return;
            }

            byte[] hello = V4NetworkPacket.Encode(new V4NetworkPacket(
                V4NetworkMessageType.Heartbeat,
                sessionId,
                0,
                Array.Empty<byte>()));
            Send(hello);
            _helloSent = true;
        }

        public void Poll()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                while (_client.Available > 0)
                {
                    IPEndPoint remote = _role == UdpRole.Host
                        ? new IPEndPoint(IPAddress.Any, 0)
                        : _hostEndPoint;
                    byte[] data = _client.Receive(ref remote);
                    if (data == null || data.Length == 0)
                    {
                        continue;
                    }

                    if (_role == UdpRole.Host)
                    {
                        _clientEndPoint = remote;
                    }

                    _inbound.Enqueue(new QueuedPacket(data, 0));
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[V4UdpTransport] Poll socket error: {ex.SocketErrorCode}");
            }
            catch (ObjectDisposedException)
            {
                _disposed = true;
            }
        }

        public void Send(byte[] packet, byte channel = 0)
        {
            if (_disposed || packet == null || packet.Length == 0)
            {
                return;
            }

            if (packet.Length > MaxDatagramBytes)
            {
                throw new InvalidOperationException(
                    $"UDP packet length {packet.Length} exceeds max datagram size {MaxDatagramBytes}");
            }

            try
            {
                if (_role == UdpRole.Host)
                {
                    if (_clientEndPoint == null)
                    {
                        return;
                    }

                    _client.Send(packet, packet.Length, _clientEndPoint);
                }
                else
                {
                    _client.Send(packet, packet.Length);
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[V4UdpTransport] Send socket error: {ex.SocketErrorCode}");
            }
            catch (ObjectDisposedException)
            {
                _disposed = true;
            }
        }

        public bool TryReceive(out byte[] packet, out byte channel)
        {
            if (_inbound.Count == 0)
            {
                packet = null;
                channel = 0;
                return false;
            }

            QueuedPacket queued = _inbound.Dequeue();
            packet = queued.Packet;
            channel = queued.Channel;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _inbound.Clear();
            _client.Close();
            _client.Dispose();
        }

        private readonly struct QueuedPacket
        {
            public readonly byte[] Packet;
            public readonly byte Channel;

            public QueuedPacket(byte[] packet, byte channel)
            {
                Packet = packet;
                Channel = channel;
            }
        }
    }
}
