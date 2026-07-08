using System;
using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Networking.Transport;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Headless host/client pair over paired <see cref="V4LoopbackTransport"/> for PlayMode verification.
    /// Host simulates and publishes; client is receive-only.
    /// </summary>
    public sealed class V4NetworkLoopbackHarness : IDisposable
    {
        public V4TestRig HostRig { get; }
        public V4TestRig ClientRig { get; }
        public V4NetworkSession HostSession { get; }
        public V4NetworkSession ClientSession { get; }

        private Topology _hostPrevious;

        private V4NetworkLoopbackHarness(
            V4TestRig hostRig,
            V4TestRig clientRig,
            V4NetworkSession hostSession,
            V4NetworkSession clientSession,
            Topology hostPrevious)
        {
            HostRig = hostRig;
            ClientRig = clientRig;
            HostSession = hostSession;
            ClientSession = clientSession;
            _hostPrevious = hostPrevious;
        }

        public static V4NetworkLoopbackHarness Create(
            ulong sessionId = 42,
            V4TestRig.Config config = null,
            bool publishInitialSnapshot = true)
        {
            var hostTransport = new V4LoopbackTransport();
            var clientTransport = new V4LoopbackTransport();
            hostTransport.PairWith(clientTransport);

            V4TestRig hostRig = V4TestRig.Create(config);
            V4TestRig clientRig = V4TestRig.Create(config);
            clientRig.Root.autoRun = false;

            var hostSession = new V4NetworkSession();
            var clientSession = new V4NetworkSession();

            Topology seed = V4TopologyBridge.Capture(hostRig.Root, sessionId);
            hostSession.StartHost(sessionId, seed, hostTransport);
            clientSession.StartClient(sessionId, clientTransport);

            Topology hostPrevious = null;
            if (publishInitialSnapshot)
            {
                hostSession.HostPublishSnapshot(seed);
                clientSession.PollAndApplyIncoming();
                V4TopologyBridge.Apply(clientRig.Root, clientSession.Authoritative);
                hostPrevious = seed.CloneTopology();
            }

            return new V4NetworkLoopbackHarness(hostRig, clientRig, hostSession, clientSession, hostPrevious);
        }

        public void SyncFrame(float dt = 1f / 60f)
        {
            HostRig.Step(1, dt);
            PublishHostState();
            PollClient();
        }

        public void PublishHostState()
        {
            Topology current = V4TopologyBridge.Capture(HostRig.Root, HostSession.SessionId);
            if (_hostPrevious == null)
            {
                HostSession.HostPublishSnapshot(current);
            }
            else
            {
                HostSession.HostPublishPatch(current);
            }

            _hostPrevious = current.CloneTopology();
        }

        public void PollClient()
        {
            if (!ClientSession.PollAndApplyIncoming() || ClientSession.Authoritative == null)
            {
                return;
            }

            V4TopologyBridge.Apply(ClientRig.Root, ClientSession.Authoritative);
        }

        public void AssertClientMatchesHost(float positionEpsilon = 1e-4f)
        {
            Topology hostTopology = V4TopologyBridge.Capture(HostRig.Root, HostSession.SessionId);
            Topology clientTopology = V4TopologyBridge.Capture(ClientRig.Root, ClientSession.SessionId);
            V4TopologyBridge.AssertParticlesApproximatelyEqual(hostTopology, clientTopology, positionEpsilon);
            Assert.AreEqual(hostTopology.frameIndex, clientTopology.frameIndex, "frame index mismatch");
        }

        public void AssertClientGpuMatchesHost(float positionEpsilon = 1e-4f)
        {
            Assert.AreEqual(HostRig.Root.ActiveParticleCount, ClientRig.Root.ActiveParticleCount, "particle count mismatch");

            Vector4[] hostPos = HostRig.ReadPositions();
            Vector4[] clientPos = ClientRig.ReadPositions();
            for (int i = 0; i < hostPos.Length; i++)
            {
                Assert.LessOrEqual(Vector3.Distance(hostPos[i], clientPos[i]), positionEpsilon, $"position mismatch at {i}");
                Assert.AreEqual(hostPos[i].w, clientPos[i].w, positionEpsilon, $"radius mismatch at {i}");
            }

            uint[] hostFlags = HostRig.ReadFlags();
            uint[] clientFlags = ClientRig.ReadFlags();
            for (int i = 0; i < hostFlags.Length; i++)
            {
                Assert.AreEqual(hostFlags[i], clientFlags[i], $"flags mismatch at {i}");
            }
        }

        public void Dispose()
        {
            ClientRig?.Dispose();
            HostRig?.Dispose();
        }
    }
}
