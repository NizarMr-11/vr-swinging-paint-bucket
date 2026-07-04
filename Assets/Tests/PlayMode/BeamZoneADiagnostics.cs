using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
/// Zone-A "beam" REGRESSION tests (post-fix). The beam was: any particle radially inside the
/// container footprint participated in the PBF density/pressure solve at ANY depth (radial-only
/// OtcParticipatesInPbf), and — being radially inside — was never claimed by canvas collision
/// (radial-only OtcIsOutsideFootprint) nor by the volume-gated floor clamp. Net: a suspended,
/// pressure-coupled column of sub-floor particles.
///
/// The fix (predicate/gating only, no solve-math change):
///   - OtcParticipatesInPbf now also requires localY >= -OTC_FLOOR_GATE_TOLERANCE (no upper bound,
///     so slosh above the rim still participates) — sub-floor "beam" particles are excluded.
///   - OtcCanvasCollisionApplies (Option A) claims a particle when it is radially outside the
///     footprint OR radially inside but below the floor band — beam particles now hit the canvas.
///   - Predict's world-drag branch uses !OtcParticipatesInPbf, so beam-depth particles free-fall
///     with world drag instead of interior viscosity/cohesion.
///
/// These tests lock the fixed behavior. They replace the earlier pre-fix "documentation" versions.
///
/// Traces: Logs/HarmonicSimulation/beam_zonea_*.log
/// </summary>
[Category("Regression")]
public class BeamZoneADiagnostics
{
    const float DeltaTime = 1f / 60f;
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float FloorTol = 0.01f;   // matches OTC_FLOOR_GATE_TOLERANCE
    const float RadiusEps = 1e-4f;
    const float PbfScalarEps = 1e-6f;
    const float SpinDegPerFrame = 8f;

    // ---------------------------------------------------------------------------------------------
    // Regression 1: a sub-floor, radially-inside beam cluster is EXCLUDED from the PBF solve
    // (density/lambda == 0). Pre-fix this coupled into the cup fluid; post-fix it must not.
    // ---------------------------------------------------------------------------------------------
    [Test]
    public void Beam_SubFloorCluster_ExcludedFromPbfSolve()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline(capacity: 8192);
        var sb = new StringBuilder();
        try
        {
            pipeline.SetCanvasPlaneY(-20f); // far below so the cluster stays a sub-floor beam during the test
            pipeline.SetCanvasCullingEnabled(true);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            // 3x3 beam cluster at r~0, well below the floor band (localY = -2).
            var particles = new List<FluidParticle>();
            AppendGrid(particles, center: new Vector3(0f, -2.0f, 0f), spacing: 0.04f);
            SpawnParticles(pipeline, particles.ToArray());

            sb.AppendLine($"[Beam:Reg1] {DateTime.Now:yyyy-MM-dd HH:mm:ss} sub-floor cluster r~0 localY=-2 floorTol={FloorTol}");
            sb.AppendLine("[Beam:Reg1] cols: frame idx | localY r | density lambda gradSq | participatesExpected");
            sb.AppendLine(new string('-', 96));

            float maxAbsDensity = 0f;
            float maxAbsLambda = 0f;

            for (int frame = 0; frame < 15; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);

                if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active) || active == 0)
                {
                    break;
                }

                FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
                for (int i = 0; i < p.Length; i++)
                {
                    var pos = new Vector3(p[i].Position.x, p[i].Position.y, p[i].Position.z);
                    Vector3 local = localToWorld.inverse.MultiplyPoint3x4(pos);
                    float r = new Vector2(local.x, local.z).magnitude;
                    bool subFloorInRadius = r <= Radius + RadiusEps && local.y < -FloorTol;
                    if (!subFloorInRadius)
                    {
                        continue;
                    }

                    float density = p[i].Density;
                    float lambda = ReadLambda(pipeline, i);
                    float gradSq = ReadGradSqSum(pipeline, i);
                    maxAbsDensity = Mathf.Max(maxAbsDensity, Mathf.Abs(density));
                    maxAbsLambda = Mathf.Max(maxAbsLambda, Mathf.Abs(lambda));

                    if (frame < 3 || i == 4)
                    {
                        sb.AppendLine(
                            $"f{frame,3} idx{i,3} | y={local.y,7:F3} r={r,5:F3} | d={density,9:F3} l={lambda,10:F5} g={gradSq,8:F3} | excluded");
                    }
                }
            }

            sb.AppendLine(new string('-', 96));
            sb.AppendLine($"[Beam:Reg1] maxAbsDensity={maxAbsDensity:E3} maxAbsLambda={maxAbsLambda:E3}");
            WriteTrace("beam_zonea_reg1_excluded.log", sb.ToString());
            Debug.Log(sb.ToString());

            Assert.Less(maxAbsDensity, PbfScalarEps,
                $"Sub-floor beam cluster still had non-zero PBF density ({maxAbsDensity:E3}) — beam exclusion failed.");
            Assert.Less(maxAbsLambda, PbfScalarEps,
                $"Sub-floor beam cluster still had non-zero PBF lambda ({maxAbsLambda:E3}) — beam exclusion failed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Regression 2: a beam particle (radially inside, below floor) is CLAIMED by canvas collision
    // within N frames and does not sink meaningfully past the canvas plane. Pre-fix nothing claimed
    // it and it free-fell without bound.
    // ---------------------------------------------------------------------------------------------
    [Test]
    public void Beam_Particle_IsClaimedByCanvasWithinFrames()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        // Canvas placed close to the start: beam particles descend under world drag (terminal velocity
        // ~0.24 m/s from the trace), so a distant plane would need thousands of frames. The regression
        // point is that the Option-A gate now claims the beam at all — proximity just bounds runtime.
        const float canvasY = -0.75f;
        const int maxFrames = 300;

        HarmonicPipelineController pipeline = CreatePipeline(capacity: 8192);
        var sb = new StringBuilder();
        try
        {
            pipeline.SetCanvasPlaneY(canvasY);
            pipeline.SetCanvasCullingEnabled(true);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            // Beam particle: radially inside (r=0), just below the floor band.
            SpawnParticles(pipeline, new[] { MakeParticle(new Vector3(0f, -0.25f, 0f), Vector3.zero) });

            sb.AppendLine($"[Beam:Reg2] {DateTime.Now:yyyy-MM-dd HH:mm:ss} beam r=0 startY=-0.25 canvasY={canvasY} maxFrames={maxFrames}");
            sb.AppendLine("[Beam:Reg2] cols: frame | worldY vy | claimed?");
            sb.AppendLine(new string('-', 72));

            bool claimed = false;
            int claimedFrame = -1;
            float minY = 0f;
            int lastActive = 1;

            for (int frame = 0; frame < maxFrames; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);

                if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active) || active == 0)
                {
                    // Removed from SoA at/after canvas contact counts as claimed.
                    claimed = true;
                    claimedFrame = frame;
                    lastActive = (int)active;
                    sb.AppendLine($"f{frame,4} | particle removed from SoA (active={active}) — claimed");
                    break;
                }
                lastActive = (int)active;

                FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
                var pos = new Vector3(p[0].Position.x, p[0].Position.y, p[0].Position.z);
                var vel = new Vector3(p[0].Velocity.x, p[0].Velocity.y, p[0].Velocity.z);
                minY = Mathf.Min(minY, pos.y);

                bool atPlane = Mathf.Abs(pos.y - canvasY) < 0.05f && Mathf.Abs(vel.y) < 0.5f;
                if (atPlane && !claimed)
                {
                    claimed = true;
                    claimedFrame = frame;
                }

                if (frame % 20 == 0 || atPlane)
                {
                    sb.AppendLine($"f{frame,4} | y={pos.y,9:F3} vy={vel.y,8:F2} | claimed={(claimed ? 1 : 0)}");
                }

                if (claimed && frame > claimedFrame + 15)
                {
                    break; // observed it rest at the plane for a while
                }
            }

            sb.AppendLine(new string('-', 72));
            sb.AppendLine($"[Beam:Reg2] claimed={claimed} claimedFrame={claimedFrame} minY={minY:F3} lastActive={lastActive}");
            WriteTrace("beam_zonea_reg2_canvasclaim.log", sb.ToString());
            Debug.Log(sb.ToString());

            Assert.IsTrue(claimed,
                $"Beam particle was NOT claimed by canvas within {maxFrames} frames (minY={minY:F3}). " +
                "Option-A canvas gate did not catch the sub-floor in-column particle.");
            Assert.GreaterOrEqual(minY, canvasY - 0.25f,
                $"Beam particle sank past the canvas plane (minY={minY:F3}, plane={canvasY}) — canvas did not arrest it.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Regression 3 (integration): a spinning lattice must not produce a PBF-COUPLED sub-floor beam.
    // For every frame, the deepest sub-floor, radially-inside particle must have ~zero PBF density
    // (i.e. it is excluded from the solve, not suspended in a coupled column). This is the lab-scale
    // reproduction from ScaleDivergenceDiagRunner, now asserted post-fix.
    // ---------------------------------------------------------------------------------------------
    [Test]
    public void Beam_SpinningLattice_NoCoupledSubFloorColumn()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        const int frames = 90;
        HarmonicPipelineController pipeline = CreatePipeline(capacity: 8192);
        var sb = new StringBuilder();
        try
        {
            pipeline.SetPbfIterations(2);
            SetField(pipeline, "cellSize", 0.1f); // config-B scale (3k lattice) — lighter than 20k
            pipeline.SetCanvasPlaneY(-6f);
            pipeline.SetCanvasCullingEnabled(true);

            Vector3 floorPivot = Vector3.zero;
            pipeline.SetUsePbf(true);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetContainerFluidOriented(floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
            int spawned = pipeline.TrySpawnContainerLatticeFill();
            Assert.Greater(spawned, 0, "Lattice spawn required.");

            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);

            sb.AppendLine($"[Beam:Reg3] {DateTime.Now:yyyy-MM-dd HH:mm:ss} spinning lattice spawned={spawned} frames={frames}");
            sb.AppendLine("[Beam:Reg3] cols: frame | deepestSubFloorInRadius: idx localY r density");
            sb.AppendLine(new string('-', 96));

            float worstCoupledDensity = 0f;
            float deepestSubFloorLocalY = 0f;

            for (int frame = 0; frame < frames; frame++)
            {
                float t = frame * SpinDegPerFrame;
                Quaternion rot = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
                Matrix4x4 w2l = currL2W.inverse;

                pipeline.SetContainerFluidOriented(floorPivot, rot, Radius, Height, 0.1f, 0.85f, 400f);
                pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
                SyncGpu();
                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
                prevL2W = currL2W;

                if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active) || active == 0)
                {
                    continue;
                }

                FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
                int deepestIdx = -1;
                float deepestY = float.MaxValue;
                float deepestR = 0f;
                for (int i = 0; i < p.Length; i++)
                {
                    var pos = new Vector3(p[i].Position.x, p[i].Position.y, p[i].Position.z);
                    Vector3 local = w2l.MultiplyPoint3x4(pos);
                    float r = new Vector2(local.x, local.z).magnitude;
                    if (r <= Radius + RadiusEps && local.y < -FloorTol && local.y < deepestY)
                    {
                        deepestY = local.y;
                        deepestIdx = i;
                        deepestR = r;
                    }
                }

                if (deepestIdx < 0)
                {
                    continue; // no sub-floor in-radius particle this frame — nothing to check
                }

                deepestSubFloorLocalY = Mathf.Min(deepestSubFloorLocalY, deepestY);
                // Read the density that is co-located with this particle in the compacted SoA (Block0.w,
                // written by Apply from _DensityCacheDensities). ApplyPositionsKernel now compacts
                // survivors via append, so readback-array index no longer maps 1:1 onto the pre-Apply
                // _DensityCacheDensities order — indexing that buffer by deepestIdx would read a
                // different (participating) particle. p[deepestIdx].Density is the same value the
                // density cache held for THIS particle, correctly carried through the compaction.
                float density = p[deepestIdx].Density;
                worstCoupledDensity = Mathf.Max(worstCoupledDensity, Mathf.Abs(density));

                if (frame % 15 == 0 || Mathf.Abs(density) > PbfScalarEps)
                {
                    sb.AppendLine($"f{frame,3} | idx{deepestIdx,5} localY={deepestY,7:F3} r={deepestR,5:F3} density={density,9:F3}");
                }
            }

            sb.AppendLine(new string('-', 96));
            sb.AppendLine($"[Beam:Reg3] deepestSubFloorLocalY={deepestSubFloorLocalY:F3} worstCoupledDensity={worstCoupledDensity:E3}");
            sb.AppendLine("[Beam:Reg3] NOTE: sub-floor particles may transiently exist (falling debris), but must have ~0 PBF density.");
            WriteTrace("beam_zonea_reg3_spinlattice.log", sb.ToString());
            Debug.Log(sb.ToString());

            Assert.Less(worstCoupledDensity, PbfScalarEps,
                $"A sub-floor in-radius particle had non-zero PBF density ({worstCoupledDensity:E3}) during the spin — " +
                "the coupled beam has returned (particles are being solved as cup fluid below the floor).");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    static void AppendGrid(List<FluidParticle> list, Vector3 center, float spacing)
    {
        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iz = -1; iz <= 1; iz++)
            {
                var pos = new Vector3(center.x + ix * spacing, center.y, center.z + iz * spacing);
                list.Add(MakeParticle(pos, Vector3.zero));
            }
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
    }

    static void SetupStationaryContainer(HarmonicPipelineController pipeline, out Matrix4x4 localToWorld)
    {
        Vector3 floorPivot = Vector3.zero;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        localToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
    }

    static void StepStationaryFrame(HarmonicPipelineController pipeline, Matrix4x4 localToWorld)
    {
        pipeline.SetContainerFluidOriented(Vector3.zero, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.ApplyContainerRigidRotation(localToWorld, localToWorld, DeltaTime);
        SyncGpu();
        pipeline.ExecutePipelineFrame(DeltaTime);
        SyncGpu();
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
            "PbfScratch", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return prop?.GetValue(pipeline) as PbfScratchBuffers;
    }

    static void SetField(HarmonicPipelineController pipeline, string fieldName, object value)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(pipeline, value);
    }

    static void WriteTrace(string fileName, string report)
    {
        string logPath = System.IO.Path.Combine(
            Application.dataPath, "..", "Logs", "HarmonicSimulation", fileName);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log($"[Beam] trace written to {logPath}");
    }

    static HarmonicPipelineController CreatePipeline(int capacity)
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

        var go = new GameObject("BeamZoneADiagnosticsPipeline");
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
            capacity: capacity,
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
