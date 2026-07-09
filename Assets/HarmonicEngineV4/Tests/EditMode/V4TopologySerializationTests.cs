using System;
using System.IO;
using HarmonicEngineV4.Networking;
using HarmonicEngineV4.Networking.Serialization;
using HarmonicEngineV4.Networking.State;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4TopologySerializationTests
    {
        [Test]
        public void Topology_DataAnnotationRoundTrip_MatchesFixture()
        {
            Topology original = CreateTopologyFixture();
            var emitter = new DataAnnotationEmitter<Topology>();

            byte[] wire;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                emitter.Emit(writer, original);
                wire = stream.ToArray();
            }

            Topology restored;
            using (var stream = new MemoryStream(wire))
            using (var reader = new BinaryReader(stream))
            {
                restored = emitter.Read(reader);
            }

            AssertTopologyEqual(original, restored);
        }

        [Test]
        public void ParticleStateFieldArrays_RawLayoutRoundTrip_ByteIdentical()
        {
            AssertRawArrayRoundTrip(new[] { 1.25f, 2.5f, 3.75f });
            AssertRawArrayRoundTrip(new uint[] { 0u, 255u, 0xAABBCCDDu });
            AssertRawArrayRoundTrip(new[] { true, false, true });
            AssertRawArrayRoundTrip(new[] { new float3(1f, 2f, 3f), new float3(-4f, 0.5f, 8f) });
            AssertRawArrayRoundTrip(new[] { new float4(1f, 2f, 3f, 4f), new float4(5f, 6f, 7f, 8f) });
        }

        [Test]
        public void Patch_DeltaSerializeAndApply_RoundTrip_MatchesNewTopology()
        {
            Topology oldState = CreateTopologyFixture();
            Topology newState = CreateTopologyFixture();
            newState.frameIndex = 99;
            newState.particles[0].eyeDepth += 0.5f;
            newState.particles[0].position += new float3(0.1f, 0.2f, 0.3f);
            newState.particles[2].flags ^= 0x10u;
            newState.particles[2].hasEscaped = true;

            Patch patch = Patch.CreateDelta(oldState, newState);
            byte[] wire = Patch.Serialize(patch, oldState);
            Topology applied = Patch.DeserializeAndApply(wire, oldState);

            AssertTopologyEqual(newState, applied);
        }

        [Test]
        public void SoaDoubleBuffer_UploadFlip_ReadMatchesUpload()
        {
            var layout = new V4SoaLayout(16);
            using var buffer = new V4SoaDoubleBuffer(layout);
            V4SoaCpuSnapshot uploaded = CreateSoaSnapshot(4);

            buffer.UploadSoa(uploaded);
            buffer.Flip();

            AssertSoaEqual(uploaded, buffer.ReadActiveSnapshot());
        }

        [Test]
        public void SoaDoubleBuffer_PatchWrite_DoesNotDisturbInFlightRead()
        {
            var layout = new V4SoaLayout(16);
            using var buffer = new V4SoaDoubleBuffer(layout);
            V4SoaCpuSnapshot inFlight = CreateSoaSnapshot(4);
            V4SoaCpuSnapshot patched = CreateSoaSnapshot(4);
            patched.block0[0] = new Vector4(9f, 8f, 7f, 6f);
            patched.flags[1] = 0xDEADBEEFu;

            buffer.UploadSoa(inFlight);
            buffer.Flip();
            V4SoaCpuSnapshot readBeforePatch = buffer.ReadActiveSnapshot().Clone();

            buffer.ApplyPatchToWrite(patched);
            AssertSoaEqual(readBeforePatch, buffer.ReadActiveSnapshot());

            buffer.Flip();
            AssertSoaEqual(patched, buffer.ReadActiveSnapshot());
        }

        private static void AssertRawArrayRoundTrip(Array array)
        {
            byte[] encoded;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                PrimitiveEmitter.Emit(writer, array);
                encoded = stream.ToArray();
            }

            Array decoded;
            using (var stream = new MemoryStream(encoded))
            using (var reader = new BinaryReader(stream))
            {
                decoded = (Array)PrimitiveEmitter.Read(reader, array.GetType());
            }

            byte[] reencoded;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                PrimitiveEmitter.Emit(writer, decoded);
                reencoded = stream.ToArray();
            }

            Assert.AreEqual(encoded.Length, reencoded.Length);
            for (int i = 0; i < encoded.Length; i++)
            {
                Assert.AreEqual(encoded[i], reencoded[i], $"byte mismatch at {i}");
            }

            Assert.AreEqual(array.Length, decoded.Length);
            for (int i = 0; i < array.Length; i++)
            {
                Assert.AreEqual(array.GetValue(i), decoded.GetValue(i), $"value mismatch at {i}");
            }
        }

        private static Topology CreateTopologyFixture()
        {
            return new Topology
            {
                schemaVersion = 1,
                sessionId = 0x123456789ABCDEF0UL,
                frameIndex = 42,
                world = new WorldTopology
                {
                    gravity = new float3(0f, -9.81f, 0f),
                    fixedDeltaTime = 1f / 90f,
                    randomSeed = 1337
                },
                bucket = new BucketTopology
                {
                    center = new float3(0f, 0.5f, 0f),
                    rotation = new float4(0f, 0f, 0f, 1f),
                    innerRadius = 0.18f,
                    outerRadius = 0.22f,
                    height = 0.35f,
                    wallThickness = 0.02f,
                    bottomThickness = 0.015f,
                    topBandHeight = 0.04f,
                    topBandOffset = 0.01f,
                    holes = new[]
                    {
                        new HoleDefinition
                        {
                            holeIndex = 0,
                            localCenter = new float3(0.1f, 0.2f, 0.3f),
                            radius = 0.03f,
                            outwardNormal = new float3(0f, -1f, 0f),
                            d0 = 0.02f,
                            d1 = 0.1f,
                            d2 = 0.18f,
                            sdfBias = 0.001f,
                            isActive = true
                        }
                    }
                },
                simulation = new SimulationTopology
                {
                    activeParticleCount = 3,
                    maxParticles = 4096,
                    substeps = 2,
                    globalDensity = 900000f,
                    profiles = new[]
                    {
                        new LiquidProfileTopology
                        {
                            profileIndex = 0,
                            restDensity = 1000f,
                            particleRadius = 0.005f,
                            smoothingRadius = 0.02f,
                            viscosity = 0.1f,
                            surfaceTension = 0.2f,
                            cohesion = 0.05f,
                            packedTint = 0xFF8040FFu
                        }
                    }
                },
                canvas = new CanvasGrid
                {
                    resolutionX = 2,
                    resolutionY = 2,
                    cellSize = 0.01f,
                    origin = new float3(-0.01f, 0f, -0.01f),
                    cells = new[]
                    {
                        new CanvasCell
                        {
                            cellIndex = 0,
                            worldPosition = new float3(0f, 0f, 0f),
                            splatRadius = 0.005f,
                            packedColor = 0xFF0000FFu,
                            opacity = 0.75f
                        }
                    }
                },
                particles = new[]
                {
                    new ParticleState
                    {
                        eyeDepth = 1.2f,
                        position = new float3(0.1f, 0.2f, 0.3f),
                        velocityColor = new float4(0.4f, 0.5f, 0.6f, 1f),
                        radius = 0.005f,
                        packedColor = 0x00FF00FFu,
                        flags = 0x1u,
                        hasEscaped = false
                    },
                    new ParticleState
                    {
                        eyeDepth = 2.2f,
                        position = new float3(0.2f, 0.3f, 0.4f),
                        velocityColor = new float4(0.1f, 0.2f, 0.3f, 0.9f),
                        radius = 0.006f,
                        packedColor = 0x0000FFFFu,
                        flags = 0x2u,
                        hasEscaped = false
                    },
                    new ParticleState
                    {
                        eyeDepth = 3.2f,
                        position = new float3(0.3f, 0.4f, 0.5f),
                        velocityColor = new float4(0.7f, 0.1f, 0.2f, 0.8f),
                        radius = 0.007f,
                        packedColor = 0xFFFF00FFu,
                        flags = 0x4u,
                        hasEscaped = false
                    }
                }
            };
        }

        private static V4SoaCpuSnapshot CreateSoaSnapshot(int count)
        {
            var snapshot = new V4SoaCpuSnapshot { count = count };
            snapshot.block0 = new Vector4[count];
            snapshot.block1 = new Vector4[count];
            snapshot.packedColors = new uint[count];
            snapshot.flags = new uint[count];
            snapshot.activeHoleSdf = new float[count];
            snapshot.holeOwner = new uint[count];
            snapshot.radialSegments = new Vector4[count];
            snapshot.activeHoleWorldPos = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                snapshot.block0[i] = new Vector4(i * 0.1f, i * 0.2f, i * 0.3f, 0.005f + i * 0.001f);
                snapshot.block1[i] = new Vector4(0.1f * i, 0.2f, 0.3f, i * 0.01f);
                snapshot.packedColors[i] = (uint)(0x01000000u * (i + 1));
                snapshot.flags[i] = (uint)i;
                snapshot.activeHoleSdf[i] = 0.01f * i;
                snapshot.holeOwner[i] = (uint)(i % 3);
                snapshot.radialSegments[i] = new Vector4(1f, 0f, 0f, 0.5f);
                snapshot.activeHoleWorldPos[i] = new Vector3(i, i * 2f, i * 3f);
            }

            return snapshot;
        }

        private static void AssertTopologyEqual(Topology expected, Topology actual)
        {
            Assert.NotNull(actual);
            Assert.AreEqual(expected.schemaVersion, actual.schemaVersion);
            Assert.AreEqual(expected.sessionId, actual.sessionId);
            Assert.AreEqual(expected.frameIndex, actual.frameIndex);
            Assert.NotNull(actual.world);
            Assert.AreEqual(expected.world.gravity, actual.world.gravity);
            Assert.AreEqual(expected.world.fixedDeltaTime, actual.world.fixedDeltaTime);
            Assert.AreEqual(expected.world.randomSeed, actual.world.randomSeed);
            Assert.NotNull(actual.bucket);
            Assert.AreEqual(expected.bucket.center, actual.bucket.center);
            Assert.AreEqual(expected.bucket.innerRadius, actual.bucket.innerRadius);
            Assert.AreEqual(expected.bucket.outerRadius, actual.bucket.outerRadius);
            Assert.AreEqual(expected.simulation.activeParticleCount, actual.simulation.activeParticleCount);
            Assert.AreEqual(expected.simulation.globalDensity, actual.simulation.globalDensity);
            Assert.AreEqual(expected.canvas.resolutionX, actual.canvas.resolutionX);
            Assert.AreEqual(expected.canvas.cells.Length, actual.canvas.cells.Length);
            Assert.AreEqual(expected.particles.Length, actual.particles.Length);
            for (int i = 0; i < expected.particles.Length; i++)
            {
                Assert.IsTrue(expected.particles[i].Equals(actual.particles[i]), $"particle {i} mismatch");
            }
        }

        private static void AssertSoaEqual(V4SoaCpuSnapshot expected, V4SoaCpuSnapshot actual)
        {
            Assert.AreEqual(expected.count, actual.count);
            for (int i = 0; i < expected.count; i++)
            {
                Assert.AreEqual(expected.block0[i], actual.block0[i]);
                Assert.AreEqual(expected.block1[i], actual.block1[i]);
                Assert.AreEqual(expected.packedColors[i], actual.packedColors[i]);
                Assert.AreEqual(expected.flags[i], actual.flags[i]);
                Assert.AreEqual(expected.activeHoleSdf[i], actual.activeHoleSdf[i], 1e-6f);
                Assert.AreEqual(expected.holeOwner[i], actual.holeOwner[i]);
                Assert.AreEqual(expected.radialSegments[i], actual.radialSegments[i]);
                Assert.AreEqual(expected.activeHoleWorldPos[i], actual.activeHoleWorldPos[i]);
            }
        }
    }
}
