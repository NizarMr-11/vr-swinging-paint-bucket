using System.Reflection;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Regression tests for exterior-particle physics: gravity in Predict, canvas collision
/// in Apply, non-sticky OtcIsOutsideFootprint / OtcParticipatesInPbf classification.
/// Locks behavior confirmed by canvas isolation D/E diagnostics — no new physics intended.
/// </summary>
[Category("Regression")]
public class SpillPhysicsTests
{
    const float DeltaTime = 1f / 60f;
    const int StationaryFrameCount = 120;
    const int SpinFrameCount = 120;
    const float SpinDegPerFrame = 8f;
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float CanvasPlaneY = -6f;
    const float RadiusEps = 1e-4f;
    const float CanvasCatchEps = 0.02f;
    const float PbfScalarEps = 1e-6f;

    [Test]
    public void ExteriorParticle_StationaryContainer_FallsUnderGravityWhileOutsideFootprint()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            SetupProductionCanvas(pipeline);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            Vector3 startPos = new Vector3(Radius + 0.15f, Height + 0.25f, 0f);
            Assert.Greater(startPos.y, CanvasPlaneY, "Test particle must start above the canvas plane.");
            Assert.Greater(RadialDistance(startPos, localToWorld), Radius + RadiusEps);

            SpawnParticles(pipeline, new[] { MakeParticle(startPos, Vector3.zero) });

            float minYWhileExterior = float.MaxValue;
            bool sawExterior = false;
            int exteriorFrames = 0;

            for (int frame = 0; frame < StationaryFrameCount; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);
                ReadParticle(pipeline, 0, out Vector3 pos, out Vector3 vel);
                float r = RadialDistance(pos, localToWorld);
                bool outside = r > Radius + RadiusEps;

                if (outside)
                {
                    sawExterior = true;
                    exteriorFrames++;
                    minYWhileExterior = Mathf.Min(minYWhileExterior, pos.y);

                    Assert.GreaterOrEqual(pos.y, CanvasPlaneY - CanvasCatchEps,
                        $"Frame {frame}: exterior particle fell past canvasPlaneY (y={pos.y:F4}, plane={CanvasPlaneY:F1}).");

                    if (pos.y <= CanvasPlaneY + CanvasCatchEps)
                    {
                        Assert.Less(Mathf.Abs(vel.y), 0.5f,
                            $"Frame {frame}: production culling should settle exterior particle at canvas (|vy|={vel.y:F4}).");
                    }
                }
            }

            Assert.IsTrue(sawExterior, "Particle never classified outside the footprint.");
            Assert.Greater(exteriorFrames, 3,
                "Expected multiple exterior frames before wall clamp re-enters the footprint.");
            Assert.Less(minYWhileExterior, startPos.y - 0.05f,
                $"Exterior particle did not fall under gravity (startY={startPos.y:F4}, minExteriorY={minYWhileExterior:F4}).");
            Assert.GreaterOrEqual(minYWhileExterior, CanvasPlaneY - CanvasCatchEps,
                "Exterior particle fell past the production canvas plane while still outside the footprint.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void ExteriorParticle_ExcludedFromPbfDensityAndLambdaWhileOutsideFootprint()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            SetupProductionCanvas(pipeline);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            // Above the rim so wall clamp does not immediately pin r to radius on frame 0.
            Vector3 startPos = new Vector3(Radius + 0.15f, Height + 0.20f, 0f);
            SpawnParticles(pipeline, new[] { MakeParticle(startPos, Vector3.zero) });

            for (int frame = 0; frame < StationaryFrameCount; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);
                ReadParticle(pipeline, 0, out Vector3 pos, out _);
                float r = RadialDistance(pos, localToWorld);

                if (r > Radius + RadiusEps)
                {
                    float density = ReadDensity(pipeline, 0);
                    float lambda = ReadLambda(pipeline, 0);
                    float gradSq = ReadGradSqSum(pipeline, 0);

                    Assert.Less(Mathf.Abs(density), PbfScalarEps,
                        $"Frame {frame}: outside footprint (r={r:F4}) must have zero density, got {density:F6}.");
                    Assert.Less(Mathf.Abs(lambda), PbfScalarEps,
                        $"Frame {frame}: outside footprint (r={r:F4}) must have zero lambda, got {lambda:F6}.");
                    Assert.Less(Mathf.Abs(gradSq), PbfScalarEps,
                        $"Frame {frame}: outside footprint (r={r:F4}) must have zero gradSqSum, got {gradSq:F6}.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void ExteriorParticle_ReEntersPbfViaOpenTop_WhenTrajectoryCrossesFootprintAboveRim()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            SetupProductionCanvas(pipeline);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            // Archetype-1 re-entry: the particle must EARN its way back into the footprint
            // through the OPEN TOP (reach r <= radius while localY > height), not via the wall
            // clamp reaching out to capture a drifting exterior particle. A strong inward vx
            // carries it across the radial footprint above the rim; the bounded wall clamp
            // early-returns on localPos.y > height, so it cannot be responsible for the crossing.
            const int outsideIndex = 1;
            SpawnParticles(pipeline, new[]
            {
                MakeParticle(new Vector3(0.40f, 0.50f, 0f), Vector3.zero),
                MakeParticle(
                    new Vector3(Radius + 0.20f, Height + 0.15f, 0f),
                    new Vector3(-8.0f, -0.3f, 0f)),
            });

            bool sawExteriorExcluded = false;
            bool crossingRecorded = false;
            bool crossedAboveRim = false;
            float crossingLocalY = float.NaN;
            bool participatedInteriorBelowRim = false;

            for (int frame = 0; frame < 90; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);
                ReadParticle(pipeline, outsideIndex, out Vector3 pos, out _);
                float r = RadialDistance(pos, localToWorld);
                float localY = LocalHeight(pos, localToWorld);
                float density = ReadDensity(pipeline, outsideIndex);
                float lambda = ReadLambda(pipeline, outsideIndex);

                bool insideFootprint = r <= Radius + RadiusEps;
                if (!insideFootprint)
                {
                    sawExteriorExcluded = true;
                    Assert.Less(Mathf.Abs(density), PbfScalarEps,
                        $"Frame {frame}: exterior particle (r={r:F4}) must be excluded from PBF density.");
                    Assert.Less(Mathf.Abs(lambda), PbfScalarEps,
                        $"Frame {frame}: exterior particle (r={r:F4}) must be excluded from PBF lambda.");
                    continue;
                }

                // First frame inside the radial footprint after having been exterior: record
                // whether the crossing happened above the rim (proof it was earned through the
                // open top, since the wall clamp's localPos.y > height guard makes wall capture
                // impossible there).
                if (sawExteriorExcluded && !crossingRecorded)
                {
                    crossingRecorded = true;
                    crossedAboveRim = localY > Height;
                    crossingLocalY = localY;
                }

                // Genuine PBF pickup once the particle is fully interior (inside radius, below rim).
                if (crossingRecorded && localY >= 0f && localY <= Height
                    && (Mathf.Abs(density) > PbfScalarEps || Mathf.Abs(lambda) > PbfScalarEps))
                {
                    participatedInteriorBelowRim = true;
                    break;
                }
            }

            Assert.IsTrue(sawExteriorExcluded,
                "Particle never started outside the footprint — trajectory setup invalid.");
            Assert.IsTrue(crossingRecorded,
                "Particle never crossed into the radial footprint under its own integration.");
            Assert.IsTrue(crossedAboveRim,
                $"Re-entry must be earned through the OPEN TOP: r<=radius must first be reached while "
                + $"localY > height (rim={Height:F2}), proving the bounded wall clamp (which early-returns "
                + $"above the rim) did not capture the particle. Observed crossing localY={crossingLocalY:F4}.");
            Assert.IsTrue(participatedInteriorBelowRim,
                "Particle fell into the interior (inside radius, below rim) but density/lambda stayed zero — "
                + "PBF participation did not resume after a genuine open-top re-entry.");
        }
        finally
        {
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    [Test]
    public void SpinRegression_OutsideFootprintWorldYNeverFallsBelowCanvasPlane()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            SetupProductionCanvas(pipeline);
            Vector3 floorPivot = Vector3.zero;
            pipeline.SetUsePbf(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetContainerFluidOriented(
                floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);

            int spawned = pipeline.TrySpawnContainerLatticeFill();
            Assert.Greater(spawned, 0, "Lattice spawn required for spin regression profile.");

            Matrix4x4 prevLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
            float globalMinOutsideWorldY = float.MaxValue;
            int outsideSamples = 0;

            for (int frame = 0; frame < SpinFrameCount; frame++)
            {
                float t = frame * SpinDegPerFrame;
                Quaternion rot = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
                Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
                Matrix4x4 worldToLocal = currLocalToWorld.inverse;

                pipeline.SetContainerFluidOriented(
                    floorPivot, rot, Radius, Height, 0.1f, 0.85f, 400f);
                pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
                SyncGpu();
                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
                prevLocalToWorld = currLocalToWorld;

                if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) || activeCount == 0)
                {
                    continue;
                }

                FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
                for (int i = 0; i < particles.Length; i++)
                {
                    Vector3 pos = new Vector3(particles[i].Position.x, particles[i].Position.y, particles[i].Position.z);
                    Vector3 local = worldToLocal.MultiplyPoint3x4(pos);
                    float r = new Vector2(local.x, local.z).magnitude;
                    if (r <= Radius + RadiusEps)
                    {
                        continue;
                    }

                    outsideSamples++;
                    globalMinOutsideWorldY = Mathf.Min(globalMinOutsideWorldY, pos.y);
                    Assert.GreaterOrEqual(pos.y, CanvasPlaneY - CanvasCatchEps,
                        $"Frame {frame} idx={i}: outside-footprint worldY={pos.y:F4} dropped below canvasPlaneY={CanvasPlaneY:F1} (r={r:F4}).");
                }
            }

            Assert.Greater(outsideSamples, 0,
                "Spin run produced no outside-footprint samples — regression guard did not exercise exterior particles.");
            Assert.GreaterOrEqual(globalMinOutsideWorldY, CanvasPlaneY - CanvasCatchEps,
                $"globalMinOutsideWorldY={globalMinOutsideWorldY:F4} violated canvas catch invariant (plane={CanvasPlaneY:F1}).");
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
            Position = new float3(position.x, position.y, position.z),
            Velocity = new float3(velocity.x, velocity.y, velocity.z),
            Density = 1000f,
            Pressure = 0f,
            PackedColorRGBA = 0xFFFFFFFFu,
        };
    }

    static void SpawnParticles(HarmonicPipelineController pipeline, FluidParticle[] particles)
    {
        int appended = pipeline.AppendParticles(particles, particles.Length);
        Assert.AreEqual(particles.Length, appended, "Failed to append test particles.");
        Assert.AreEqual((uint)particles.Length, pipeline.GetActiveParticleCount());
    }

    static void SetupProductionCanvas(HarmonicPipelineController pipeline)
    {
        pipeline.SetCanvasPlaneY(CanvasPlaneY);
        pipeline.SetCanvasCullingEnabled(true);
    }

    static void SetupStationaryContainer(HarmonicPipelineController pipeline, out Matrix4x4 localToWorld)
    {
        Vector3 floorPivot = Vector3.zero;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(
            floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        localToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
    }

    static void StepStationaryFrame(HarmonicPipelineController pipeline, Matrix4x4 localToWorld)
    {
        pipeline.SetContainerFluidOriented(
            Vector3.zero, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.ApplyContainerRigidRotation(localToWorld, localToWorld, DeltaTime);
        SyncGpu();
        pipeline.ExecutePipelineFrame(DeltaTime);
        SyncGpu();
    }

    static float RadialDistance(Vector3 worldPos, Matrix4x4 localToWorld)
    {
        Vector3 local = localToWorld.inverse.MultiplyPoint3x4(worldPos);
        return new Vector2(local.x, local.z).magnitude;
    }

    static float LocalHeight(Vector3 worldPos, Matrix4x4 localToWorld)
    {
        return localToWorld.inverse.MultiplyPoint3x4(worldPos).y;
    }

    static void ReadParticle(HarmonicPipelineController pipeline, int index, out Vector3 pos, out Vector3 vel)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint count) && index < count);
        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)count);
        pos = new Vector3(particles[index].Position.x, particles[index].Position.y, particles[index].Position.z);
        vel = new Vector3(particles[index].Velocity.x, particles[index].Velocity.y, particles[index].Velocity.z);
    }

    static float ReadDensity(HarmonicPipelineController pipeline, int index)
    {
        Assert.IsTrue(pipeline.TryGetDensityCacheBuffers(out ComputeBuffer densities, out _, out uint count) && index < count);
        var scratch = new float[1];
        densities.GetData(scratch, 0, index, 1);
        return scratch[0];
    }

    static float ReadLambda(HarmonicPipelineController pipeline, int index)
    {
        PbfScratchBuffers scratch = GetPbfScratch(pipeline);
        Assert.NotNull(scratch?.Lambdas);
        var value = new float[1];
        scratch.Lambdas.GetData(value, 0, index, 1);
        return value[0];
    }

    static float ReadGradSqSum(HarmonicPipelineController pipeline, int index)
    {
        PbfScratchBuffers scratch = GetPbfScratch(pipeline);
        Assert.NotNull(scratch?.GradSqSum);
        var value = new float[1];
        scratch.GradSqSum.GetData(value, 0, index, 1);
        return value[0];
    }

    static PbfScratchBuffers GetPbfScratch(HarmonicPipelineController pipeline)
    {
        PropertyInfo prop = typeof(HarmonicPipelineController).GetProperty(
            "PbfScratch",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return prop?.GetValue(pipeline) as PbfScratchBuffers;
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

        var go = new GameObject("SpillPhysicsTestPipeline");
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
