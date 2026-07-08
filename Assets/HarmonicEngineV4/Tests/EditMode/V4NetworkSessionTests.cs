using System.IO;
using System.Threading;
using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.Serialization;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Networking.Transport;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4NetworkSessionTests
    {
        [Test]
        public void NetworkPacket_EncodeDecode_RoundTrip()
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };
            var original = new V4NetworkPacket(V4NetworkMessageType.Patch, 0xABCu, 17, payload);
            byte[] wire = V4NetworkPacket.Encode(original);
            V4NetworkPacket decoded = V4NetworkPacket.Decode(wire);

            Assert.AreEqual(original.MessageType, decoded.MessageType);
            Assert.AreEqual(original.SessionId, decoded.SessionId);
            Assert.AreEqual(original.FrameIndex, decoded.FrameIndex);
            Assert.AreEqual(original.Payload.Length, decoded.Payload.Length);
            for (int i = 0; i < payload.Length; i++)
            {
                Assert.AreEqual(payload[i], decoded.Payload[i]);
            }
        }

        [Test]
        public void Session_LoopbackSnapshot_ClientMatchesHost()
        {
            var hostTransport = new V4LoopbackTransport();
            var clientTransport = new V4LoopbackTransport();
            hostTransport.PairWith(clientTransport);

            Topology seed = CreateSeedTopology(frameIndex: 1);
            var host = new V4NetworkSession();
            var client = new V4NetworkSession();
            host.StartHost(42, seed, hostTransport);
            client.StartClient(42, clientTransport);

            host.HostPublishSnapshot(seed);
            Assert.IsTrue(client.PollAndApplyIncoming());
            AssertTopologyEqual(seed, client.Authoritative);
        }

        [Test]
        public void Session_LoopbackPatch_ClientReceivesUpdatedFrame()
        {
            var hostTransport = new V4LoopbackTransport();
            var clientTransport = new V4LoopbackTransport();
            hostTransport.PairWith(clientTransport);

            Topology seed = CreateSeedTopology(frameIndex: 1);
            Topology updated = CreateSeedTopology(frameIndex: 2);
            updated.particles[0].eyeDepth += 0.75f;
            updated.particles[1].position += new float3(0.05f, 0f, 0f);

            var host = new V4NetworkSession();
            var client = new V4NetworkSession();
            host.StartHost(99, seed, hostTransport);
            client.StartClient(99, clientTransport);

            host.HostPublishSnapshot(seed);
            client.PollAndApplyIncoming();

            host.HostPublishPatch(updated);
            client.PollAndApplyIncoming();

            Assert.AreEqual(2u, client.Authoritative.frameIndex);
            Assert.AreEqual(updated.particles[0].eyeDepth, client.Authoritative.particles[0].eyeDepth, 1e-5f);
            Assert.AreEqual(updated.particles[1].position, client.Authoritative.particles[1].position);
        }

        [Test]
        public void TopologyBridge_ToSoaSnapshot_MapsParticleFields()
        {
            Topology topology = CreateSeedTopology(3);
            V4SoaCpuSnapshot snapshot = V4TopologyBridge.ToSoaSnapshot(topology);

            Assert.AreEqual(3, snapshot.count);
            Assert.AreEqual(topology.particles[0].position.x, snapshot.block0[0].x, 1e-5f);
            Assert.AreEqual(topology.particles[0].radius, snapshot.block0[0].w, 1e-5f);
            Assert.AreEqual(topology.particles[0].velocityColor.x, snapshot.block1[0].x, 1e-5f);
            Assert.AreEqual(topology.particles[0].eyeDepth, snapshot.block1[0].w, 1e-5f);
            Assert.AreEqual(topology.particles[0].packedColor, snapshot.packedColors[0]);
            Assert.AreEqual(topology.particles[0].flags, snapshot.flags[0]);
        }

        [Test]
        public void Session_SnapshotWireFormat_UsesDataAnnotationEmitter()
        {
            Topology topology = CreateSeedTopology(1);
            var session = new V4NetworkSession();
            byte[] payload = session.SerializeSnapshot(topology);

            Topology restored;
            using (var stream = new MemoryStream(payload))
            using (var reader = new BinaryReader(stream))
            {
                restored = new DataAnnotationEmitter<Topology>().Read(reader);
            }

            Assert.AreEqual(topology.frameIndex, restored.frameIndex);
            Assert.AreEqual(topology.particles.Length, restored.particles.Length);
        }

        [Test]
        public void Session_UdpLocalhost_Snapshot_ClientMatchesHost()
        {
            using var hostTransport = V4UdpTransport.CreateHost(0);
            using var clientTransport = V4UdpTransport.CreateClient("127.0.0.1", hostTransport.BoundPort);

            Topology seed = CreateSeedTopology(frameIndex: 1);
            var host = new V4NetworkSession();
            var client = new V4NetworkSession();
            host.StartHost(42, seed, hostTransport);
            client.StartClient(42, clientTransport);

            clientTransport.EnsureHelloSent(42);
            PumpTransports(hostTransport, clientTransport);

            host.HostPublishSnapshot(seed);
            PumpTransports(hostTransport, clientTransport);

            Assert.IsTrue(client.PollAndApplyIncoming());
            AssertTopologyEqual(seed, client.Authoritative);
        }

        [Test]
        public void Session_UdpLocalhost_Patch_ClientReceivesUpdatedFrame()
        {
            using var hostTransport = V4UdpTransport.CreateHost(0);
            using var clientTransport = V4UdpTransport.CreateClient("127.0.0.1", hostTransport.BoundPort);

            Topology seed = CreateSeedTopology(frameIndex: 1);
            Topology updated = CreateSeedTopology(frameIndex: 2);
            updated.particles[0].eyeDepth += 0.75f;
            updated.particles[1].position += new float3(0.05f, 0f, 0f);

            var host = new V4NetworkSession();
            var client = new V4NetworkSession();
            host.StartHost(99, seed, hostTransport);
            client.StartClient(99, clientTransport);

            clientTransport.EnsureHelloSent(99);
            PumpTransports(hostTransport, clientTransport);

            host.HostPublishSnapshot(seed);
            PumpTransports(hostTransport, clientTransport);
            client.PollAndApplyIncoming();

            host.HostPublishPatch(updated);
            PumpTransports(hostTransport, clientTransport);
            client.PollAndApplyIncoming();

            Assert.AreEqual(2u, client.Authoritative.frameIndex);
            Assert.AreEqual(updated.particles[0].eyeDepth, client.Authoritative.particles[0].eyeDepth, 1e-5f);
            Assert.AreEqual(updated.particles[1].position, client.Authoritative.particles[1].position);
        }

        private static void PumpTransports(IV4NetworkTransport hostTransport, IV4NetworkTransport clientTransport, int iterations = 64)
        {
            for (int i = 0; i < iterations; i++)
            {
                hostTransport.Poll();
                clientTransport.Poll();
                Thread.Sleep(2);
            }
        }

        private static Topology CreateSeedTopology(uint frameIndex)
        {
            return CreateSeedTopology(2, frameIndex);
        }

        private static Topology CreateSeedTopology(int particleCount, uint frameIndex = 1)
        {
            var particles = new ParticleState[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                particles[i] = new ParticleState
                {
                    eyeDepth = 1f + i,
                    position = new float3(i * 0.1f, 0.2f, 0.3f),
                    velocityColor = new float4(0.1f * i, 0.2f, 0.3f, 1f),
                    radius = 0.005f,
                    packedColor = (uint)(0x01010101u * (i + 1)),
                    flags = (uint)i,
                    hasEscaped = false
                };
            }

            return new Topology
            {
                schemaVersion = V4NetworkConstants.SchemaVersion,
                sessionId = 7,
                frameIndex = frameIndex,
                world = new WorldTopology { gravity = new float3(0, -9.81f, 0), fixedDeltaTime = 1f / 60f, randomSeed = 1 },
                simulation = new SimulationTopology
                {
                    activeParticleCount = particleCount,
                    maxParticles = 1024,
                    substeps = 2,
                    globalDensity = 100000f
                },
                particles = particles
            };
        }

        private static void AssertTopologyEqual(Topology expected, Topology actual)
        {
            Assert.NotNull(actual);
            Assert.AreEqual(expected.frameIndex, actual.frameIndex);
            Assert.AreEqual(expected.particles.Length, actual.particles.Length);
            for (int i = 0; i < expected.particles.Length; i++)
            {
                Assert.IsTrue(expected.particles[i].Equals(actual.particles[i]), $"particle {i}");
            }
        }
    }
}
