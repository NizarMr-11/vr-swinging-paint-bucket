using System;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Simulation;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>Maps between live V4 simulation objects and networking <see cref="Topology"/> DTOs.</summary>
    public static class V4TopologyBridge
    {
        public static Topology Capture(V4PipelineRoot pipeline, ulong sessionId)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (!pipeline.Initialized)
            {
                throw new InvalidOperationException("Pipeline must be initialized before capture");
            }

            int count = pipeline.ActiveParticleCount;
            var block0 = new Vector4[count];
            var block1 = new Vector4[count];
            var colors = new uint[count];
            var flags = new uint[count];
            pipeline.ReadParticleSnapshot(block0, block1, colors, flags, count);

            var particles = new ParticleState[count];
            for (int i = 0; i < count; i++)
            {
                float3 rgb = UnpackColor(colors[i]);
                particles[i] = new ParticleState
                {
                    eyeDepth = block1[i].w,
                    position = new float3(block0[i].x, block0[i].y, block0[i].z),
                    velocityColor = new float4(block1[i].x, block1[i].y, block1[i].z, 1f),
                    radius = block0[i].w,
                    packedColor = colors[i],
                    flags = flags[i],
                    hasEscaped = V4ParticleFlags.HasEscaped(flags[i])
                };
            }

            V4Bucket bucket = pipeline.bucket;
            V4Canvas canvas = pipeline.canvas;
            return new Topology
            {
                schemaVersion = V4NetworkConstants.SchemaVersion,
                sessionId = sessionId,
                frameIndex = (uint)Mathf.Clamp(pipeline.FrameIndex, 0, uint.MaxValue),
                world = new WorldTopology
                {
                    gravity = new float3(pipeline.gravity.x, pipeline.gravity.y, pipeline.gravity.z),
                    fixedDeltaTime = pipeline.maxDeltaTime,
                    randomSeed = 0
                },
                bucket = bucket == null ? null : new BucketTopology
                {
                    center = new float3(bucket.transform.position.x, bucket.transform.position.y, bucket.transform.position.z),
                    rotation = new float4(
                        bucket.transform.rotation.x,
                        bucket.transform.rotation.y,
                        bucket.transform.rotation.z,
                        bucket.transform.rotation.w),
                    innerRadius = bucket.innerRadius,
                    outerRadius = bucket.innerRadius + bucket.wallThickness,
                    height = bucket.height,
                    wallThickness = bucket.wallThickness,
                    bottomThickness = bucket.wallThickness,
                    topBandHeight = bucket.topBandHeight,
                    topBandOffset = 0f,
                    holes = V4HoleBridge.CaptureHoles(pipeline)
                },
                simulation = new SimulationTopology
                {
                    activeParticleCount = count,
                    maxParticles = pipeline.Capacity,
                    substeps = pipeline.pbfIterations,
                    globalDensity = pipeline.globalDensity,
                    profiles = null
                },
                canvas = canvas == null ? null : new CanvasGrid
                {
                    resolutionX = pipeline.CanvasGridSize.x,
                    resolutionY = pipeline.CanvasGridSize.y,
                    cellSize = pipeline.CanvasCellSize,
                    origin = new float3(canvas.MinCorner.x, canvas.PlaneY, canvas.MinCorner.y),
                    cells = V4CanvasGridBridge.CapturePaintedCells(pipeline)
                },
                particles = particles
            };
        }

        public static void Apply(V4PipelineRoot pipeline, Topology topology)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (topology == null)
            {
                throw new ArgumentNullException(nameof(topology));
            }

            if (!pipeline.Initialized)
            {
                throw new InvalidOperationException("Pipeline must be initialized before apply");
            }

            if (topology.world != null)
            {
                pipeline.gravity = new Vector3(topology.world.gravity.x, topology.world.gravity.y, topology.world.gravity.z);
                pipeline.maxDeltaTime = topology.world.fixedDeltaTime;
            }

            if (topology.simulation != null)
            {
                pipeline.globalDensity = topology.simulation.globalDensity;
                pipeline.pbfIterations = Mathf.Clamp(topology.simulation.substeps, 1, 4);
            }

            if (topology.bucket != null && pipeline.bucket != null)
            {
                pipeline.bucket.innerRadius = topology.bucket.innerRadius;
                pipeline.bucket.height = topology.bucket.height;
                pipeline.bucket.wallThickness = topology.bucket.wallThickness;
                pipeline.bucket.topBandHeight = topology.bucket.topBandHeight;
                float4 rot = topology.bucket.rotation;
                Quaternion rotation = rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f
                    ? pipeline.bucket.transform.rotation
                    : new Quaternion(rot.x, rot.y, rot.z, rot.w);
                pipeline.bucket.transform.SetPositionAndRotation(
                    new Vector3(topology.bucket.center.x, topology.bucket.center.y, topology.bucket.center.z),
                    rotation);

                if (topology.bucket.holes != null)
                {
                    V4HoleBridge.ApplyHoles(pipeline, topology.bucket.holes);
                }
            }

            ParticleState[] particles = topology.particles ?? Array.Empty<ParticleState>();
            int count = particles.Length;
            var block0 = new Vector4[count];
            var block1 = new Vector4[count];
            var colors = new uint[count];
            var flags = new uint[count];

            for (int i = 0; i < count; i++)
            {
                ParticleState p = particles[i];
                block0[i] = new Vector4(p.position.x, p.position.y, p.position.z, p.radius);
                block1[i] = new float4(p.velocityColor.x, p.velocityColor.y, p.velocityColor.z, p.eyeDepth);
                colors[i] = p.packedColor;
                flags[i] = p.flags;
                if (p.hasEscaped)
                {
                    flags[i] = V4ParticleFlags.SetEscaped(flags[i]);
                }
            }

            pipeline.ApplyNetworkParticleSnapshot(block0, block1, colors, flags, count);
            pipeline.SetNetworkFrameIndex(topology.frameIndex);

            if (topology.canvas != null)
            {
                V4CanvasGridBridge.ApplyPaintedCells(pipeline, topology.canvas);
            }
        }

        public static V4SoaCpuSnapshot ToSoaSnapshot(Topology topology)
        {
            if (topology?.particles == null)
            {
                return new V4SoaCpuSnapshot { count = 0 };
            }

            int count = topology.particles.Length;
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
                ParticleState p = topology.particles[i];
                snapshot.block0[i] = new Vector4(p.position.x, p.position.y, p.position.z, p.radius);
                snapshot.block1[i] = new Vector4(p.velocityColor.x, p.velocityColor.y, p.velocityColor.z, p.eyeDepth);
                snapshot.packedColors[i] = p.packedColor;
                snapshot.flags[i] = p.flags;
            }

            return snapshot;
        }

        public static void AssertParticlesApproximatelyEqual(Topology expected, Topology actual, float positionEpsilon = 1e-4f)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            ParticleState[] expectedParticles = expected.particles ?? Array.Empty<ParticleState>();
            ParticleState[] actualParticles = actual.particles ?? Array.Empty<ParticleState>();
            if (expectedParticles.Length != actualParticles.Length)
            {
                throw new InvalidOperationException(
                    $"particle count mismatch: expected {expectedParticles.Length}, got {actualParticles.Length}");
            }

            for (int i = 0; i < expectedParticles.Length; i++)
            {
                ParticleState e = expectedParticles[i];
                ParticleState a = actualParticles[i];
                if (math.distance(e.position, a.position) > positionEpsilon)
                {
                    throw new InvalidOperationException($"position mismatch at {i}: {e.position} vs {a.position}");
                }

                if (math.abs(e.radius - a.radius) > positionEpsilon)
                {
                    throw new InvalidOperationException($"radius mismatch at {i}: {e.radius} vs {a.radius}");
                }

                if (e.packedColor != a.packedColor || e.flags != a.flags || e.hasEscaped != a.hasEscaped)
                {
                    throw new InvalidOperationException($"metadata mismatch at particle {i}");
                }
            }
        }

        private static float3 UnpackColor(uint packed)
        {
            float r = (packed & 0xFFu) / 255f;
            float g = ((packed >> 8) & 0xFFu) / 255f;
            float b = ((packed >> 16) & 0xFFu) / 255f;
            return new float3(r, g, b);
        }
    }
}
