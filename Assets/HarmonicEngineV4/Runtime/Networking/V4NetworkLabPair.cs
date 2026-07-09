using HarmonicEngineV4.Networking.Transport;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>
    /// Wires a host/client <see cref="V4NetworkSettings"/> pair for Lab loopback or UDP demos.
    /// Assign two pipeline objects (duplicate the V4 Pipeline hierarchy for the client mirror).
    /// </summary>
    public sealed class V4NetworkLabPair : MonoBehaviour
    {
        [SerializeField] private V4NetworkSettings host;
        [SerializeField] private V4NetworkSettings client;
        [SerializeField] private bool autoEnableOnPlay = true;
        [SerializeField] private V4TransportKind transportKind = V4TransportKind.Loopback;
        [SerializeField] private ulong sessionId = 0xC0FFEE01UL;
        [SerializeField] private int udpPort = V4NetworkConstants.DefaultUdpPort;
        [SerializeField] private string hostAddress = "127.0.0.1";

        private void Start()
        {
            if (!autoEnableOnPlay)
            {
                return;
            }

            ApplyPair();
        }

        public void ApplyPair()
        {
            if (host == null || client == null)
            {
                Debug.LogWarning("[V4NetworkLabPair] Assign both host and client V4NetworkSettings references.");
                return;
            }

            host.enableNetworking = true;
            host.role = V4NetworkRole.Host;
            host.transportKind = transportKind;
            host.sessionId = sessionId;
            host.udpPort = udpPort;
            host.hostAddress = hostAddress;
            host.clientSimulatesLocally = false;
            host.disableClientBucketInput = false;
            host.hostAllowsBucketInput = true;
            host.publishOnFrameCompleted = true;
            host.loopbackPeer = client;

            client.enableNetworking = true;
            client.role = V4NetworkRole.Client;
            client.transportKind = transportKind;
            client.sessionId = sessionId;
            client.udpPort = udpPort;
            client.hostAddress = hostAddress;
            client.clientSimulatesLocally = false;
            client.disableClientBucketInput = true;
            client.hostAllowsBucketInput = false;
            client.publishOnFrameCompleted = true;
            client.loopbackPeer = host;

            host.Apply();
            client.Apply();
        }
    }
}
