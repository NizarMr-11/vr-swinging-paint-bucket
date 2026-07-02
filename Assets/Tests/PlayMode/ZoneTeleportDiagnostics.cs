using System;
using System.Reflection;
using System.Text;
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
/// Zone-B floor-clamp regression. Reproduces the exact smoking-gun repro (R=0.55, ground particles
/// resting at y=-2 well below the bucket floor, dt=0.008) and sweeps the bucket column over them,
/// reading GPU state back per frame with carry-vs-PBF attribution:
///
///   before (frame start) --[rigid carry]--> afterCarry --[PBF Predict..Apply]--> afterPbf
///
/// Before the volume-continuity floor-clamp gate, PBF teleported these particles from y=-2 up to the
/// floor (y=0) in a single frame and launched them at ~212 m/s (Zone B), then they flew off
/// ballistically (Zone A). With the gate, particles that were never inside the cup volume are no
/// longer floor-clamped, so they stay put / fall gently under gravity — no teleport, no launch.
///
/// Writes a timestamped regression trace to Logs/HarmonicSimulation/zone_teleport_regression_trace.log.
/// </summary>
[Category("Regression")]
public class ZoneTeleportDiagnostics
{
    const float DeltaTime = 0.008f;   // single PBF substep (<= container maxTimeStep) for clean Old/Predicted
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float FloorEps = 1e-4f;
    const float CanvasPlaneY = -2f;   // resting ground particles sit here
    const float LaunchSpeedThreshold = 100f;  // the pre-fix launch was ~212 m/s
    const float MaxTolerableSpeed = 25f;      // gate on: only gentle gravity/settling motion expected

    [Test]
    public void FloorClampContinuityGate_SweptGroundParticles_AreNotTeleportedOrLaunched()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        var sb = new StringBuilder();
        try
        {
            pipeline.SetUsePbf(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetSimulationActive(true);
            pipeline.SetCanvasPlaneY(CanvasPlaneY);
            // Culling OFF so un-teleported ground particles persist (bounce/rest on the canvas plane)
            // and stay trackable in the container SoA. With culling ON, particles that are correctly
            // left below the floor migrate out of the SoA and their readback slots read as zeros,
            // which the position-based teleport heuristic would misread as a snap to y=0.
            pipeline.SetCanvasCullingEnabled(false);

            // Ground particles resting on the canvas plane (well below the bucket floor at y=0),
            // spread along x so the sweeping bucket column passes over each in turn.
            float[] groundX = { -1.2f, -0.6f, 0.0f, 0.6f, 1.2f };
            var particles = new FluidParticle[groundX.Length];
            for (int k = 0; k < groundX.Length; k++)
            {
                particles[k] = MakeParticle(new Vector3(groundX[k], CanvasPlaneY, 0f), Vector3.zero);
            }
            SpawnParticles(pipeline, particles);

            sb.AppendLine($"[ZoneTeleport:regression] {DateTime.Now:yyyy-MM-dd HH:mm:ss} floor@y=0 R={Radius} canvasPlaneY={CanvasPlaneY} dt={DeltaTime}");
            sb.AppendLine("[ZoneTeleport:regression] cols: frame pivotX idx | localY_old r_old underColumn | carryDy pbfDy | localY_final | |vCarry| |vPbf| | zone");
            sb.AppendLine(new string('-', 120));

            // Settle: bucket far away so ground particles rest on the canvas plane.
            Vector3 farPivot = new Vector3(-5f, 0f, 0f);
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(farPivot, Quaternion.identity);
            SetContainer(pipeline, farPivot);
            for (int s = 0; s < 3; s++)
            {
                Matrix4x4 curr = ContainerOrientedBounds.BuildLocalToWorld(farPivot, Quaternion.identity);
                StepFrame(pipeline, farPivot, prevL2W, curr, out _, out _, out _, groundX.Length);
                prevL2W = curr;
            }

            bool sawTeleport = false;
            float peakSpeed = 0f;
            float maxUpwardJumpToFloor = 0f;

            // Sweep: bucket translates its column across the resting particles.
            for (int frame = 0; frame < 45; frame++)
            {
                float pivotX = -2.0f + frame * 0.1f;
                Vector3 pivot = new Vector3(pivotX, 0f, 0f);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(pivot, Quaternion.identity);
                Matrix4x4 worldToLocal = currL2W.inverse;
                SetContainer(pipeline, pivot);

                StepFrame(pipeline, pivot, prevL2W, currL2W,
                    out ParticleState[] before, out ParticleState[] afterCarry, out ParticleState[] afterPbf,
                    groundX.Length);

                for (int k = 0; k < groundX.Length; k++)
                {
                    Vector3 oldPos = before[k].Pos;
                    Vector3 localOld = worldToLocal.MultiplyPoint3x4(oldPos);
                    float rOld = new Vector2(localOld.x, localOld.z).magnitude;
                    bool underColumn = localOld.y < 0f && rOld <= Radius + FloorEps;

                    float carryDy = afterCarry[k].Pos.y - before[k].Pos.y;
                    float pbfDy = afterPbf[k].Pos.y - afterCarry[k].Pos.y;
                    Vector3 localFinal = worldToLocal.MultiplyPoint3x4(afterPbf[k].Pos);
                    float vCarry = afterCarry[k].Vel.magnitude;
                    float vPbf = afterPbf[k].Vel.magnitude;

                    // A migrated/removed particle reads back as an exact-zero slot in the container
                    // SoA; that is NOT a teleport (the particle simply left the tracked buffer).
                    bool validSlot = afterPbf[k].Pos.sqrMagnitude > 1e-8f || afterPbf[k].Vel.sqrMagnitude > 1e-8f;

                    // Teleport = started deep below the floor, ended snapped up at/near the floor plane.
                    bool teleported = validSlot && localOld.y < -0.5f && localFinal.y > -0.25f;
                    if (teleported)
                    {
                        sawTeleport = true;
                        maxUpwardJumpToFloor = Mathf.Max(maxUpwardJumpToFloor, localFinal.y - localOld.y);
                    }

                    string zone = teleported ? "ZONE_B(teleport!)"
                        : (vPbf > LaunchSpeedThreshold ? "ZONE_A(ballistic!)" : "-");

                    // Log the frames a particle is under the column (where the pre-fix teleport used
                    // to fire) plus any anomalies, so the trace is easy to cross-check.
                    if (underColumn || teleported || vPbf > MaxTolerableSpeed)
                    {
                        sb.AppendLine(
                            $"f{frame,3} px={pivotX,5:F2} idx{k} | yOld={localOld.y,7:F3} r={rOld,5:F3} col={(underColumn ? 1 : 0)} | " +
                            $"carryDy={carryDy,8:F4} pbfDy={pbfDy,8:F4} | yFinal={localFinal.y,7:F3} | " +
                            $"|vCarry|={vCarry,8:F2} |vPbf|={vPbf,9:F2} | {zone}");
                    }

                    peakSpeed = Mathf.Max(peakSpeed, vPbf);
                }

                prevL2W = currL2W;
            }

            sb.AppendLine(new string('-', 120));
            sb.AppendLine($"[ZoneTeleport:regression] peakSpeed={peakSpeed:F2} m/s (pre-fix was ~212) " +
                          $"teleportObserved={sawTeleport} maxUpwardJumpToFloor={maxUpwardJumpToFloor:F3}");

            string report = sb.ToString();
            WriteTrace(report);
            Debug.Log(report);

            Assert.IsFalse(sawTeleport,
                "Volume-continuity gate FAILED: a swept ground particle was still teleported from below the floor " +
                "up to the floor plane. See zone_teleport_regression_trace.log.");
            Assert.Less(peakSpeed, MaxTolerableSpeed,
                $"Volume-continuity gate FAILED: swept ground particle velocity spiked to {peakSpeed:F1} m/s " +
                $"(pre-fix launch was ~212 m/s). Expected only gentle gravity/settling motion.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    struct ParticleState
    {
        public Vector3 Pos;
        public Vector3 Vel;
    }

    static void StepFrame(
        HarmonicPipelineController pipeline,
        Vector3 pivot,
        Matrix4x4 prevL2W,
        Matrix4x4 currL2W,
        out ParticleState[] before,
        out ParticleState[] afterCarry,
        out ParticleState[] afterPbf,
        int count)
    {
        before = ReadStates(pipeline, count);
        pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
        SyncGpu();
        afterCarry = ReadStates(pipeline, count);
        pipeline.ExecutePipelineFrame(DeltaTime);
        SyncGpu();
        afterPbf = ReadStates(pipeline, count);
    }

    static ParticleState[] ReadStates(HarmonicPipelineController pipeline, int count)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active));
        int n = Mathf.Min(count, (int)active);
        FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
        var states = new ParticleState[count];
        for (int i = 0; i < count; i++)
        {
            if (i < n)
            {
                states[i].Pos = new Vector3(p[i].Position.x, p[i].Position.y, p[i].Position.z);
                states[i].Vel = new Vector3(p[i].Velocity.x, p[i].Velocity.y, p[i].Velocity.z);
            }
        }
        return states;
    }

    static void SetContainer(HarmonicPipelineController pipeline, Vector3 floorPivot)
    {
        pipeline.SetContainerFluidOriented(floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
    }

    static void WriteTrace(string report)
    {
        string logPath = System.IO.Path.Combine(
            Application.dataPath, "..", "Logs", "HarmonicSimulation", "zone_teleport_regression_trace.log");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log($"[ZoneTeleport:regression] full trace written to {logPath}");
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
        Assert.AreEqual(particles.Length, appended, "Failed to append ground particles.");
    }

    static HarmonicPipelineController CreatePipeline()
    {
        var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
        Assert.IsNotNull(settings, "HarmonicPipelineTestSettings missing from Resources.");

#if UNITY_EDITOR
        var carryShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ContainerRigidCarry.compute");
#else
        ComputeShader carryShader = null;
#endif
        Assert.IsNotNull(carryShader, "ContainerRigidCarry.compute not found.");

        var go = new GameObject("ZoneTeleportDiagnosticsPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        FieldInfo carryField = typeof(HarmonicPipelineController).GetField(
            "containerRigidCarryShader", BindingFlags.Instance | BindingFlags.NonPublic);
        carryField?.SetValue(pipeline, carryShader);

        FieldInfo muteField = typeof(HarmonicPipelineController).GetField(
            "perfDiagnosticsMuted", BindingFlags.Instance | BindingFlags.NonPublic);
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
            GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }
}
