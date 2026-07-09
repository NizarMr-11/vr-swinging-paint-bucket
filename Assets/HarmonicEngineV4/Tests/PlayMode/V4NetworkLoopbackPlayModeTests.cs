using System.Collections;
using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
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
        public IEnumerator Loopback_CanvasPaint_ClientMatchesHost()
        {
            using var harness = V4NetworkLoopbackHarness.Create();
            yield return null;

            int total = harness.HostRig.Root.CanvasGridSize.x * harness.HostRig.Root.CanvasGridSize.y;
            var grid = new Vector4[total];
            const int paintedIndex = 42;
            grid[paintedIndex] = new Vector4(1f, 0.1f, 0.2f, 0.75f);
            harness.HostRig.Root.ApplyNetworkCanvasSnapshot(grid);

            harness.PublishHostState();
            harness.PollClient();

            Vector4[] clientGrid = harness.ClientRig.ReadCanvasGrid();
            Assert.AreEqual(0.75f, clientGrid[paintedIndex].w, 1e-4f, "canvas depth mismatch");
            Assert.Greater(clientGrid[paintedIndex].x, 0.9f, "canvas color mismatch");
        }

        [UnityTest]
        public IEnumerator Loopback_BakedHoles_ClientMatchesHost()
        {
            var config = new V4TestRig.Config
            {
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef
                    {
                        localPosition = new Vector3(0.1f, 0f, 0f),
                        radius = 0.04f,
                        outwardNormal = Vector3.down
                    }
                }
            };

            using var harness = V4NetworkLoopbackHarness.Create(config: config);
            yield return null;

            Assert.Greater(harness.HostRig.Root.BakedHoles.Count, 0);

            V4BakedHole[] modified = new V4BakedHole[harness.HostRig.Root.BakedHoles.Count];
            for (int i = 0; i < modified.Length; i++)
            {
                modified[i] = harness.HostRig.Root.BakedHoles[i];
            }

            modified[0].d0 *= 1.15f;
            modified[0].d1 *= 1.05f;
            harness.HostRig.Root.ApplyNetworkBakedHoles(modified);

            harness.PublishHostState();
            harness.PollClient();

            Assert.AreEqual(modified[0].d0, harness.ClientRig.Root.BakedHoles[0].d0, 1e-5f, "hole d0 mismatch");
            Assert.AreEqual(modified[0].d1, harness.ClientRig.Root.BakedHoles[0].d1, 1e-5f, "hole d1 mismatch");
            Assert.AreEqual(modified[0].outwardNormal, harness.ClientRig.Root.BakedHoles[0].outwardNormal, "hole normal mismatch");
        }

        [UnityTest]
        public IEnumerator Loopback_BucketRotation_ClientMatchesHost()
        {
            using var harness = V4NetworkLoopbackHarness.Create();
            yield return null;

            harness.HostRig.Bucket.transform.rotation = Quaternion.Euler(15f, 30f, 5f);
            harness.PublishHostState();
            harness.PollClient();

            Quaternion hostRotation = harness.HostRig.Bucket.transform.rotation;
            Quaternion clientRotation = harness.ClientRig.Bucket.transform.rotation;
            Assert.Less(Quaternion.Angle(hostRotation, clientRotation), 0.1f, "bucket rotation mismatch");
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
