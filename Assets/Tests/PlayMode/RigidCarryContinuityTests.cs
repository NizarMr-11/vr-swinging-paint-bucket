using System.Reflection;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rigid-carry continuity gate: prevents false capture of stationary exterior
/// particles when the moving bucket sweeps over them (case-a only).
/// </summary>
[Category("Regression")]
public class RigidCarryContinuityTests
{
    const float DeltaTime = 1f / 60f;
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float CarryEps = 1e-5f;

    [Test]
    public void ContinuouslyCarriedParticle_ReceivesCarryKinematicsWhenEarned()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            Vector3 floorPivot = Vector3.zero;
            SetupContainer(pipeline, floorPivot, Quaternion.identity);
            SpawnParticles(pipeline, new[] { MakeParticle(new Vector3(0.3f, 0.5f, 0f), Vector3.zero) });

            // Simulate established entrainment: particle was carry-earned last frame.
            WritePrevInsideForCarry(pipeline, 0, 1u);

            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
            Quaternion rot = Quaternion.Euler(0f, 12f, 0f);
            Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);

            ReadParticle(pipeline, 0, out Vector3 posBefore, out Vector3 velBefore);
            StepCarryOnly(pipeline, floorPivot, rot, prevL2W, currL2W);
            ReadParticle(pipeline, 0, out Vector3 posAfter, out Vector3 velAfter);

            float posDelta = Vector3.Distance(posBefore, posAfter);
            Assert.Greater(posDelta, CarryEps,
                "Continuously-carried interior particle must move with container rotation.");
            Assert.Greater(velAfter.magnitude, CarryEps,
                "Continuously-carried interior particle must receive carry velocity contribution.");
            Assert.Greater(Vector3.Distance(velBefore, velAfter), CarryEps,
                "Carry must change velocity for a continuously-carried particle under rotation.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void StationaryGroundParticle_SwingingBucketRotation_NeverPromoted()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            // Particle at rest, off the rotation axis so cross(omega, arm) is non-trivial.
            Vector3 floorPivot = Vector3.zero;
            Vector3 particlePos = new Vector3(0.3f, 0.05f, 0f);
            SetupContainer(pipeline, floorPivot, Quaternion.identity);
            SpawnParticles(pipeline, new[] { MakeParticle(particlePos, Vector3.zero) });

            // Rotation about Y preserves the particle's local radius and height, so it stays
            // geometrically inside the carry volume for the entire swing while the container's
            // rigid point velocity at its location stays large (a fast sweep).
            float angle = 0f;
            const float degPerFrame = 8f;
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);

            bool everInside = false;
            for (int frame = 0; frame < 25; frame++)
            {
                angle += degPerFrame;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
                Matrix4x4 worldToLocal = currL2W.inverse;

                if (IsInsideForRigidCarry(particlePos, worldToLocal, Height, Radius))
                {
                    everInside = true;
                }

                ReadParticle(pipeline, 0, out Vector3 posBefore, out Vector3 velBefore);
                StepCarryOnly(pipeline, floorPivot, rot, prevL2W, currL2W);
                ReadParticle(pipeline, 0, out Vector3 posAfter, out Vector3 velAfter);

                Assert.AreEqual(0u, ReadPrevInsideForCarry(pipeline, 0),
                    $"Frame {frame}: a stationary ground particle must never be promoted to carry-earned under a fast swing.");
                Assert.Less(velAfter.magnitude, CarryEps,
                    $"Frame {frame}: carry must not spike a non-entrained particle's velocity to match the sweep.");
                Assert.Less(Vector3.Distance(posBefore, posAfter), CarryEps,
                    $"Frame {frame}: carry must not drag a non-entrained particle's position.");

                prevL2W = currL2W;
            }

            Assert.IsTrue(everInside,
                "Swing never placed the particle inside the carry volume — test setup invalid.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void LegitimateEntrainment_PromotedOnceVelocityConverges()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            Vector3 floorPivot = Vector3.zero;
            Vector3 particlePos = new Vector3(0.3f, 0.5f, 0f);
            SetupContainer(pipeline, floorPivot, Quaternion.identity);
            SpawnParticles(pipeline, new[] { MakeParticle(particlePos, Vector3.zero) });

            // A gentle constant rotation keeps carry dispatching every frame (dispatch requires a
            // non-identity delta) while the container's own point velocity stays negligible, so the
            // earn gate is driven almost entirely by the particle's decaying velocity — modelling a
            // poured particle whose relative motion settles out via repeated friction contact.
            const float degPerFrame = 0.01f;
            const float frictionPerFrame = 0.8f;
            Vector3 velocity = new Vector3(1.2f, 0f, 0f);

            float angle = 0f;
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);

            int promotedFrame = -1;
            const int maxFrames = 30;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                WriteParticleVelocity(pipeline, 0, velocity);

                angle += degPerFrame;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
                StepCarryOnly(pipeline, floorPivot, rot, prevL2W, currL2W);

                if (ReadPrevInsideForCarry(pipeline, 0) == 1u)
                {
                    promotedFrame = frame;
                    break;
                }

                velocity *= frictionPerFrame;
                prevL2W = currL2W;
            }

            Debug.Log($"[RigidCarryContinuity] legitimate entrainment promoted at frame {promotedFrame} " +
                      $"(EARN_VELOCITY_THRESHOLD=0.25 sanity check).");
            Assert.GreaterOrEqual(promotedFrame, 0,
                "A settling particle whose velocity converges must eventually be promoted to carry-earned.");
            Assert.Greater(promotedFrame, 0,
                "Promotion happened immediately despite an above-threshold initial velocity — threshold may be too loose.");
            Assert.LessOrEqual(promotedFrame, 20,
                "Promotion took implausibly long — EARN_VELOCITY_THRESHOLD may be too strict.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void ContainerFillParticles_AreCarryEarnedFromFrameZero()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            SetupContainer(pipeline, Vector3.zero, Quaternion.identity);

            int spawned = pipeline.TrySpawnContainerLatticeFill();
            Assert.Greater(spawned, 0, "Container lattice fill produced no particles — test setup invalid.");

            // Every initially-filled in-container particle is genuine fluid and must be seeded
            // carry-earned so the velocity-agreement gate admits it from frame 0 (no settling pop).
            Assert.AreEqual(1u, ReadPrevInsideForCarry(pipeline, 0),
                "First container-fill particle must be carry-earned from frame 0.");
            Assert.AreEqual(1u, ReadPrevInsideForCarry(pipeline, spawned / 2),
                "Mid-range container-fill particle must be carry-earned from frame 0.");
            Assert.AreEqual(1u, ReadPrevInsideForCarry(pipeline, spawned - 1),
                "Last container-fill particle must be carry-earned from frame 0.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    static FluidParticle MakeParticle(Vector3 position, Vector3 velocity)
    {
        return new FluidParticle
        {
            Position = new Unity.Mathematics.float3(position.x, position.y, position.z),
            Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
            Density = 1000f,
            Pressure = 0f,
            PackedColorRGBA = 0xFFFFFFFFu,
        };
    }

    static void SpawnParticles(HarmonicPipelineController pipeline, FluidParticle[] particles)
    {
        int appended = pipeline.AppendParticles(particles, particles.Length);
        Assert.AreEqual(particles.Length, appended, "Failed to append test particles.");
    }

    static void SetupContainer(HarmonicPipelineController pipeline, Vector3 floorPivot, Quaternion rotation)
    {
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(
            floorPivot, rotation, Radius, Height, 0.1f, 0.85f, 400f);
    }

    static void StepCarryOnly(
        HarmonicPipelineController pipeline,
        Vector3 floorPivot,
        Quaternion rotation,
        Matrix4x4 prevLocalToWorld,
        Matrix4x4 currLocalToWorld)
    {
        pipeline.SetContainerFluidOriented(
            floorPivot, rotation, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
        SyncGpu();
    }

    static bool IsInsideForRigidCarry(Vector3 worldPos, Matrix4x4 worldToLocal, float height, float radius)
    {
        Vector3 local = worldToLocal.MultiplyPoint3x4(worldPos);
        float radial = new Vector2(local.x, local.z).magnitude;
        return local.y >= 0f && local.y <= height && radial <= radius;
    }

    static void ReadParticle(HarmonicPipelineController pipeline, int index, out Vector3 pos, out Vector3 vel)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint count) && index < count);
        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)count);
        pos = new Vector3(particles[index].Position.x, particles[index].Position.y, particles[index].Position.z);
        vel = new Vector3(particles[index].Velocity.x, particles[index].Velocity.y, particles[index].Velocity.z);
    }

    static void WriteParticleVelocity(HarmonicPipelineController pipeline, int index, Vector3 velocity)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint count) && index < count);
        // Block1 = float4(velocity.xyz, pressure); preserve nothing but velocity for the test.
        var data = new[] { new Vector4(velocity.x, velocity.y, velocity.z, 0f) };
        soa.Block1.SetData(data, 0, index, 1);
    }

    static uint ReadPrevInsideForCarry(HarmonicPipelineController pipeline, int index)
    {
        ComputeBuffer buffer = GetPrevInsideForCarryBuffer(pipeline);
        Assert.NotNull(buffer);
        Assert.Less(index, buffer.count);
        var scratch = new uint[1];
        buffer.GetData(scratch, 0, index, 1);
        return scratch[0];
    }

    static void WritePrevInsideForCarry(HarmonicPipelineController pipeline, int index, uint value)
    {
        ComputeBuffer buffer = GetPrevInsideForCarryBuffer(pipeline);
        Assert.NotNull(buffer);
        Assert.Less(index, buffer.count);
        var scratch = new uint[] { value };
        buffer.SetData(scratch, 0, index, 1);
    }

    static ComputeBuffer GetPrevInsideForCarryBuffer(HarmonicPipelineController pipeline)
    {
        PropertyInfo prop = typeof(HarmonicPipelineController).GetProperty(
            "PrevInsideForCarryBuffer",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return prop?.GetValue(pipeline) as ComputeBuffer;
    }

    static HarmonicPipelineController CreatePipeline()
    {
        var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
        Assert.IsNotNull(settings, "HarmonicPipelineTestSettings missing from Resources.");

#if UNITY_EDITOR
        var carryShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ContainerRigidCarry.compute");
        var otcFieldShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/OtcParticleField.compute");
#else
        ComputeShader carryShader = null;
        ComputeShader otcFieldShader = null;
#endif
        Assert.IsNotNull(carryShader, "ContainerRigidCarry.compute not found.");
        Assert.IsNotNull(otcFieldShader, "OtcParticleField.compute not found.");

        var go = new GameObject("RigidCarryContinuityTestPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        FieldInfo carryField = typeof(HarmonicPipelineController).GetField(
            "containerRigidCarryShader",
            BindingFlags.Instance | BindingFlags.NonPublic);
        carryField?.SetValue(pipeline, carryShader);

        FieldInfo otcField = typeof(HarmonicPipelineController).GetField(
            "otcParticleFieldShader",
            BindingFlags.Instance | BindingFlags.NonPublic);
        otcField?.SetValue(pipeline, otcFieldShader);

        FieldInfo muteField = typeof(HarmonicPipelineController).GetField(
            "perfDiagnosticsMuted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        muteField?.SetValue(pipeline, true);

        pipeline.ConfigureAndInitialize(
            settings.argumentUtilityShader,
            settings.spatialHashGridShader,
            settings.wcsphDensityShader,
            settings.dataCompactionShader,
            capacity: 8192,
            externalIngestion: true,
            autoRun: false,
            fallingShader: settings.fallingFluidWorldShader,
            eulerianShader: settings.eulerianDragGridShader,
            integrateShader: settings.wcsphIntegrationShader,
            pbfShader: settings.pbfSolverShader,
            radixShader: settings.radixSortShader);

        return pipeline;
    }

    static void SyncGpu()
    {
        GraphicsFence fence = Graphics.CreateGraphicsFence(
            GraphicsFenceType.AsyncQueueSynchronisation,
            SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }
}
