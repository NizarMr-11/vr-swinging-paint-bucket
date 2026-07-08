using System.Collections;
using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Networking.Transport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngineV4.Tests.PlayMode
{
    public sealed class V4NetworkLoopbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator Loopback_InitialSnapshot_ClientMatchesHost()
        {
            using var harness = V4NetworkLoopbackHarness.Create();
            yield return null;

            harness.AssertClientMatchesHost();
            harness.AssertClientGpuMatchesHost();
        }

        [UnityTest]
        public IEnumerator Loopback_AfterSimulationFrames_ClientMatchesHost()
        {
            using var harness = V4NetworkLoopbackHarness.Create();
            yield return null;

            for (int i = 0; i < 60; i++)
            {
                harness.SyncFrame();
                if (i % 10 == 9)
                {
                    yield return null;
                }
            }

            harness.AssertClientMatchesHost();
            harness.AssertClientGpuMatchesHost();
            Assert.Greater(harness.HostRig.Root.FrameIndex, 0, "host should have advanced frames");
        }

        [UnityTest]
        public IEnumerator Loopback_CoordinatorHostClient_StaysSynced()
        {
            var hostTransport = new V4LoopbackTransport();
            var clientTransport = new V4LoopbackTransport();
            hostTransport.PairWith(clientTransport);

            using var hostRig = V4TestRig.Create();
            using var clientRig = V4TestRig.Create();
            clientRig.Root.autoRun = false;

            var hostGo = hostRig.Root.gameObject;
            var clientGo = clientRig.Root.gameObject;

            var hostCoordinator = hostGo.AddComponent<V4NetworkCoordinator>();
            var clientCoordinator = clientGo.AddComponent<V4NetworkCoordinator>();
            hostCoordinator.Configure(hostRig.Root, V4NetworkRole.Host, hostTransport, 0xBEEF01UL);
            clientCoordinator.Configure(clientRig.Root, V4NetworkRole.Client, clientTransport, 0xBEEF01UL);

            yield return null;

            hostCoordinator.HostSendSnapshot();
            clientCoordinator.Session.PollAndApplyIncoming();
            V4TopologyBridge.Apply(clientRig.Root, clientCoordinator.Session.Authoritative);

            for (int i = 0; i < 30; i++)
            {
                hostRig.Step(1);
                hostCoordinator.HostSendPatch();
                clientCoordinator.Session.PollAndApplyIncoming();
                if (clientCoordinator.Session.Authoritative != null)
                {
                    V4TopologyBridge.Apply(clientRig.Root, clientCoordinator.Session.Authoritative);
                }
            }

            yield return null;

            Topology hostTopology = V4TopologyBridge.Capture(hostRig.Root, 0xBEEF01UL);
            Topology clientTopology = V4TopologyBridge.Capture(clientRig.Root, 0xBEEF01UL);
            V4TopologyBridge.AssertParticlesApproximatelyEqual(hostTopology, clientTopology);
        }
    }
}
