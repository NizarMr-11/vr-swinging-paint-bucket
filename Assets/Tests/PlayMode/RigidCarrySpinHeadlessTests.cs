using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.Management.Gpu;
using HarmonicEngine.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Exploratory / diagnostic harness for rigid-carry spin tunneling — NOT part of the
/// standard regression gate until min-Y stays bounded under spin. See
/// OpenTopCylinderBoundaryTests for boundary regression coverage.
/// </summary>
[Category("Exploratory")]
[Category("Diagnostic")]
public class RigidCarrySpinHeadlessTests
{
    private const int FrameCount = 120;
    private const float DeltaTime = 1f / 60f;
    private const float SpinDegreesPerFrame = 8f;

    [Test]
    [Category("Exploratory")]
    public void RigidCarrySpin_MinLocalY_HeadlessCheck()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunSpinSimulation(logPerFrameDiagnostics: false, out float minLocalYInRadius);
        Debug.Log($"[RigidCarrySpin] frames={FrameCount} minLocalYInRadius={minLocalYInRadius:F6}");
    }

    /// <summary>
    /// Per-frame trace for one in-radius particle: post-carry local Y, |velocity|, arm length.
    /// </summary>
    [Test]
    [Category("Exploratory")]
    [Category("Diagnostic")]
    public void RigidCarrySpin_PerFrameCarryTrace_Diagnostics()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunSpinSimulation(logPerFrameDiagnostics: true, out float minLocalYInRadius);
        Debug.Log($"[RigidCarrySpin:Trace] summary minLocalYInRadius={minLocalYInRadius:F6}");
    }

    /// <summary>
    /// Post-H1-fix verification: bounded min-Y, plateauing carry |v|, preserved sloshing (relative vel).
    /// </summary>
    [Test]
    [Category("Exploratory")]
    [Category("Diagnostic")]
    public void RigidCarrySpin_H1Fix_Verification()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunH1FixVerification();
    }

    /// <summary>
    /// Residual min-Y tail and transient carry-|v| spike diagnostics (post H1 fix b).
    /// </summary>
    [Test]
    [Category("Exploratory")]
    [Category("Diagnostic")]
    public void RigidCarrySpin_ResidualTailDiagnostics()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunResidualTailDiagnostics();
    }

    /// <summary>
    /// PBF sub-stage trace for floor-wall corner explosions (idx 2651 / 670).
    /// Report-only diagnostic — does not modify solver shaders.
    /// </summary>
    [Test]
    [Category("Exploratory")]
    [Category("Diagnostic")]
    public void RigidCarrySpin_CornerPbfSubstage_Diagnostics()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunCornerPbfSubstageDiagnostics();
    }

    private static void RunResidualTailDiagnostics()
    {
        RunSinglePassArgminTrace(out FrameArgminSnapshot[] perFrameArgmin, out GlobalMinSummary globalMin, out CanvasHarnessInfo canvasInfo);

        var sb = new StringBuilder();
        sb.AppendLine("[RigidCarrySpin:Residual] single-pass summary:");
        sb.AppendLine($"  canvasPlaneY={canvasInfo.CanvasPlaneY:F4} (explicitlySetInTest={canvasInfo.ExplicitlySetInTest})");
        sb.AppendLine($"  canvasCullingEnabled={canvasInfo.CanvasCullingEnabled} canvasPaintAbsorbEnabled={canvasInfo.CanvasPaintAbsorbEnabled}");
        sb.AppendLine($"  globalMin postPbfLocalY={globalMin.PostPbfLocalY:F6} frame={globalMin.Frame} index={globalMin.Index}");
        sb.AppendLine($"  at globalMin: r={globalMin.PostPbfRadial:F4} outsideFootprint={globalMin.OutsideFootprint} preCarryEligible={globalMin.PreCarryEligible}");
        sb.AppendLine($"  at globalMin: postCarry|v|={globalMin.PostCarryVelMag:F4} postCarryLocalY={globalMin.PostCarryLocalY:F4} postPbfWorldY={globalMin.PostPbfWorldY:F4}");
        sb.AppendLine($"  matrixMismatch frames (mismatch>1e-4): {globalMin.MatrixMismatchCount}/{FrameCount}");
        if (globalMin.MatrixMismatchCount == 0)
        {
            sb.AppendLine("  matrixMismatch: ALL 120 frames match (carry W2L == frame W2L)");
        }

        sb.AppendLine("[RigidCarrySpin:Residual] per-frame argmin (in-radius post-PBF localY):");
        for (int frame = 0; frame < FrameCount; frame++)
        {
            FrameArgminSnapshot s = perFrameArgmin[frame];
            sb.AppendLine(
                $"  frame={frame} idx={s.Index} postPbfLocalY={s.PostPbfLocalY:F4} r={s.PostPbfRadial:F4} " +
                $"preCarryEligible={s.PreCarryEligible} postCarry|v|={s.PostCarryVelMag:F4} " +
                $"outsideFootprint={s.OutsideFootprint} postPbfWorldY={s.PostPbfWorldY:F4}");
        }

        int windowStart = Mathf.Max(0, globalMin.Frame - 5);
        sb.AppendLine($"[RigidCarrySpin:Residual] 5-frame lead-up to globalMin (frames {windowStart}..{globalMin.Frame}):");
        for (int frame = windowStart; frame <= globalMin.Frame; frame++)
        {
            FrameArgminSnapshot s = perFrameArgmin[frame];
            sb.AppendLine(
                $"  frame={frame} idx={s.Index} postPbfLocalY={s.PostPbfLocalY:F4} r={s.PostPbfRadial:F4} " +
                $"preCarryEligible={s.PreCarryEligible} postCarry|v|={s.PostCarryVelMag:F4} postCarryLocalY={s.PostCarryLocalY:F4}");
        }

        Debug.Log(sb.ToString());
    }

    private readonly struct CanvasHarnessInfo
    {
        public readonly float CanvasPlaneY;
        public readonly bool CanvasCullingEnabled;
        public readonly bool CanvasPaintAbsorbEnabled;
        public readonly bool ExplicitlySetInTest;

        public CanvasHarnessInfo(float canvasPlaneY, bool canvasCullingEnabled, bool canvasPaintAbsorbEnabled, bool explicitlySetInTest)
        {
            CanvasPlaneY = canvasPlaneY;
            CanvasCullingEnabled = canvasCullingEnabled;
            CanvasPaintAbsorbEnabled = canvasPaintAbsorbEnabled;
            ExplicitlySetInTest = explicitlySetInTest;
        }
    }

    private readonly struct FrameArgminSnapshot
    {
        public readonly int Frame;
        public readonly int Index;
        public readonly bool PreCarryEligible;
        public readonly float PostCarryVelMag;
        public readonly float PostCarryLocalY;
        public readonly float PostCarryRadial;
        public readonly float PostPbfLocalY;
        public readonly float PostPbfRadial;
        public readonly float PostPbfWorldY;
        public readonly bool OutsideFootprint;

        public FrameArgminSnapshot(
            int frame,
            int index,
            bool preCarryEligible,
            float postCarryVelMag,
            float postCarryLocalY,
            float postCarryRadial,
            float postPbfLocalY,
            float postPbfRadial,
            float postPbfWorldY,
            bool outsideFootprint)
        {
            Frame = frame;
            Index = index;
            PreCarryEligible = preCarryEligible;
            PostCarryVelMag = postCarryVelMag;
            PostCarryLocalY = postCarryLocalY;
            PostCarryRadial = postCarryRadial;
            PostPbfLocalY = postPbfLocalY;
            PostPbfRadial = postPbfRadial;
            PostPbfWorldY = postPbfWorldY;
            OutsideFootprint = outsideFootprint;
        }

        public static FrameArgminSnapshot Empty(int frame) =>
            new FrameArgminSnapshot(frame, -1, false, 0f, 0f, 0f, float.MaxValue, 0f, 0f, false);
    }

    private readonly struct GlobalMinSummary
    {
        public readonly int Frame;
        public readonly int Index;
        public readonly float PostPbfLocalY;
        public readonly float PostPbfRadial;
        public readonly float PostPbfWorldY;
        public readonly bool OutsideFootprint;
        public readonly bool PreCarryEligible;
        public readonly float PostCarryVelMag;
        public readonly float PostCarryLocalY;
        public readonly int MatrixMismatchCount;

        public GlobalMinSummary(FrameArgminSnapshot snapshot, int matrixMismatchCount)
        {
            Frame = snapshot.Frame;
            Index = snapshot.Index;
            PostPbfLocalY = snapshot.PostPbfLocalY;
            PostPbfRadial = snapshot.PostPbfRadial;
            PostPbfWorldY = snapshot.PostPbfWorldY;
            OutsideFootprint = snapshot.OutsideFootprint;
            PreCarryEligible = snapshot.PreCarryEligible;
            PostCarryVelMag = snapshot.PostCarryVelMag;
            PostCarryLocalY = snapshot.PostCarryLocalY;
            MatrixMismatchCount = matrixMismatchCount;
        }
    }

    private static void RunSinglePassArgminTrace(
        out FrameArgminSnapshot[] perFrameArgmin,
        out GlobalMinSummary globalMin,
        out CanvasHarnessInfo canvasInfo)
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        InitializeSpinPipeline(pipeline, out Vector3 floorPivot, out float radius, out float height, out Matrix4x4 prevLocalToWorld);
        PingPongSoaManager pingPong = GetPingPong(pipeline);

        canvasInfo = new CanvasHarnessInfo(
            pipeline.CanvasPlaneY,
            pipeline.CanvasCullingEnabled,
            GetSerializedBool(pipeline, "canvasPaintAbsorbEnabled", defaultValue: true),
            explicitlySetInTest: false);

        perFrameArgmin = new FrameArgminSnapshot[FrameCount];
        int matrixMismatchCount = 0;
        FrameArgminSnapshot best = FrameArgminSnapshot.Empty(0);

        for (int frame = 0; frame < FrameCount; frame++)
        {
            float t = frame * SpinDegreesPerFrame;
            Quaternion rotation = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rotation);
            Matrix4x4 worldToLocal = currLocalToWorld.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot,
                rotation,
                radius,
                height,
                restitution: 0.1f,
                friction: 0.85f,
                wallStiffness: 400f);

            float mismatch = MatrixMismatchMagnitude(GetContainerWorldToLocal(pipeline), worldToLocal);
            if (mismatch > 1e-4f)
            {
                matrixMismatchCount++;
            }

            perFrameArgmin[frame] = FrameArgminSnapshot.Empty(frame);

            if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers preSoa, out uint activeCount) || activeCount == 0)
            {
                prevLocalToWorld = currLocalToWorld;
                continue;
            }

            FluidParticle[] preParticles = GpuParticleReadbackUtility.ReadParticles(preSoa, (int)activeCount);

            pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
            SyncGpu();

            FluidParticle[] postCarryParticles = GpuParticleReadbackUtility.ReadParticles(pingPong.ReadSet, (int)activeCount);

            pipeline.ExecutePipelineFrame(DeltaTime);
            SyncGpu();
            prevLocalToWorld = currLocalToWorld;

            if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers postSoa, out activeCount) || activeCount == 0)
            {
                continue;
            }

            FluidParticle[] postPbfParticles = GpuParticleReadbackUtility.ReadParticles(postSoa, (int)activeCount);

            int argminIndex = -1;
            float argminLocalY = float.MaxValue;
            float argminRadial = 0f;
            float argminWorldY = 0f;

            for (int i = 0; i < (int)activeCount; i++)
            {
                Vector3 postPbfPos = ToVector3(postPbfParticles[i].Position);
                Vector3 postPbfLocal = worldToLocal.MultiplyPoint3x4(postPbfPos);
                float radial = new Vector2(postPbfLocal.x, postPbfLocal.z).magnitude;
                if (radial > radius + 1e-4f)
                {
                    continue;
                }

                if (postPbfLocal.y < argminLocalY)
                {
                    argminLocalY = postPbfLocal.y;
                    argminIndex = i;
                    argminRadial = radial;
                    argminWorldY = postPbfPos.y;
                }
            }

            if (argminIndex < 0)
            {
                continue;
            }

            Vector3 prePos = ToVector3(preParticles[argminIndex].Position);
            Vector3 postCarryPos = ToVector3(postCarryParticles[argminIndex].Position);
            Vector3 postCarryVel = ToVector3(postCarryParticles[argminIndex].Velocity);
            Vector3 postCarryLocal = worldToLocal.MultiplyPoint3x4(postCarryPos);

            var snapshot = new FrameArgminSnapshot(
                frame,
                argminIndex,
                IsInsideForRigidCarry(prePos, worldToLocal, height, radius),
                postCarryVel.magnitude,
                postCarryLocal.y,
                new Vector2(postCarryLocal.x, postCarryLocal.z).magnitude,
                argminLocalY,
                argminRadial,
                argminWorldY,
                IsOutsideFootprint(argminRadial, radius));

            perFrameArgmin[frame] = snapshot;

            if (argminLocalY < best.PostPbfLocalY)
            {
                best = snapshot;
            }
        }

        globalMin = new GlobalMinSummary(best, matrixMismatchCount);
        Object.DestroyImmediate(pipeline.gameObject);
    }

    private static Vector3 ToVector3(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z);

    private static bool IsOutsideFootprint(float radial, float radius) => radial > radius + 1e-4f;

    private static bool GetSerializedBool(HarmonicPipelineController pipeline, string fieldName, bool defaultValue)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (bool)field.GetValue(pipeline) : defaultValue;
    }

    private static bool IsInsideForRigidCarry(Vector3 worldPos, Matrix4x4 worldToLocal, float height, float radius)
    {
        Vector3 local = worldToLocal.MultiplyPoint3x4(worldPos);
        float radial = new Vector2(local.x, local.z).magnitude;
        return local.y >= 0f && local.y <= height && radial <= radius;
    }

    private static Vector3 ReadPrevCarryContribution(HarmonicPipelineController pipeline, int index)
    {
        ComputeBuffer buffer = GetPrevCarryContributionBuffer(pipeline);
        if (buffer == null || index < 0 || index >= buffer.count)
        {
            return Vector3.zero;
        }

        var scratch = new Vector3[1];
        buffer.GetData(scratch, 0, index, 1);
        return scratch[0];
    }

    private static ComputeBuffer GetPrevCarryContributionBuffer(HarmonicPipelineController pipeline)
    {
        PropertyInfo prop = typeof(HarmonicPipelineController).GetProperty(
            "PrevCarryContributionBuffer",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return prop != null ? (ComputeBuffer)prop.GetValue(pipeline) : null;
    }

    private static uint MatrixChecksum(Matrix4x4 m)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < 16; i++)
            {
                hash ^= (uint)System.BitConverter.SingleToInt32Bits(m[i]);
                hash *= 16777619u;
            }

            return hash;
        }
    }

    private static void RunH1FixVerification()
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        Vector3 floorPivot = Vector3.zero;
        float radius = 0.55f;
        float height = 1.1f;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(
            floorPivot,
            Quaternion.identity,
            radius,
            height,
            restitution: 0.1f,
            friction: 0.85f,
            wallStiffness: 400f);

        int spawned = pipeline.TrySpawnContainerLatticeFill();
        Assert.Greater(spawned, 0);

        Matrix4x4 prevLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
        float minLocalYInRadius = float.MaxValue;
        PingPongSoaManager pingPong = GetPingPong(pipeline);

        int highestVelIndex = 0;
        float highestVelEver = 0f;
        var perFrameHighestVelMag = new float[FrameCount];
        var perFrameHighestVelIndex = new int[FrameCount];
        int sloshingSampleIndex = SelectTrackedParticleIndex(pipeline, prevLocalToWorld, radius);

        for (int frame = 0; frame < FrameCount; frame++)
        {
            float t = frame * SpinDegreesPerFrame;
            Quaternion rotation = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rotation);
            Matrix4x4 worldToLocal = currLocalToWorld.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot,
                rotation,
                radius,
                height,
                restitution: 0.1f,
                friction: 0.85f,
                wallStiffness: 400f);

            Vector3 angularVelocity = ComputeAngularVelocityWorld(prevLocalToWorld, currLocalToWorld, DeltaTime);
            pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
            SyncGpu();

            if (pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) && activeCount > 0)
            {
                FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
                float frameHighestVel = 0f;
                int frameHighestIdx = 0;
                for (int i = 0; i < particles.Length; i++)
                {
                    Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
                    float radial = new Vector2(local.x, local.z).magnitude;
                    if (radial > radius + 1e-4f)
                    {
                        continue;
                    }

                    float velMag = new Vector3(particles[i].Velocity.x, particles[i].Velocity.y, particles[i].Velocity.z).magnitude;
                    if (velMag > frameHighestVel)
                    {
                        frameHighestVel = velMag;
                        frameHighestIdx = i;
                    }
                }

                perFrameHighestVelMag[frame] = frameHighestVel;
                perFrameHighestVelIndex[frame] = frameHighestIdx;
                if (frameHighestVel > highestVelEver)
                {
                    highestVelEver = frameHighestVel;
                    highestVelIndex = frameHighestIdx;
                }

                if (frame == 30 || frame == 60 || frame == 90)
                {
                    Vector3 pos = new Vector3(
                        particles[sloshingSampleIndex].Position.x,
                        particles[sloshingSampleIndex].Position.y,
                        particles[sloshingSampleIndex].Position.z);
                    Vector3 vel = new Vector3(
                        particles[sloshingSampleIndex].Velocity.x,
                        particles[sloshingSampleIndex].Velocity.y,
                        particles[sloshingSampleIndex].Velocity.z);
                    Vector3 arm = pos - floorPivot;
                    Vector3 rotContribution = Vector3.Cross(angularVelocity, arm);
                    Vector3 relativeVel = vel - rotContribution;
                    Debug.Log(
                        $"[RigidCarrySpin:H1Fix] sloshing frame={frame} sampleIdx={sloshingSampleIndex} " +
                        $"|relativeVel|={relativeVel.magnitude:F4} relativeVel=({relativeVel.x:F3},{relativeVel.y:F3},{relativeVel.z:F3}) " +
                        $"|worldVel|={vel.magnitude:F4} |rotContrib|={rotContribution.magnitude:F4}");
                }
            }

            pipeline.ExecutePipelineFrame(DeltaTime);
            SyncGpu();
            prevLocalToWorld = currLocalToWorld;

            if (pipeline.TryGetInternalParticleSoa(out soa, out activeCount) && activeCount > 0)
            {
                FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
                for (int i = 0; i < particles.Length; i++)
                {
                    Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
                    float radial = new Vector2(local.x, local.z).magnitude;
                    if (radial <= radius + 1e-4f)
                    {
                        minLocalYInRadius = Mathf.Min(minLocalYInRadius, local.y);
                    }
                }
            }
        }

        int monotonicIncreases = 0;
        for (int i = 1; i < FrameCount; i++)
        {
            if (perFrameHighestVelMag[i] > perFrameHighestVelMag[i - 1] + 1e-3f)
            {
                monotonicIncreases++;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("[RigidCarrySpin:H1Fix] verification summary:");
        sb.AppendLine($"  minLocalYInRadius={minLocalYInRadius:F6} (baseline pre-fix: -405.64)");
        sb.AppendLine($"  peak post-carry |v| across run: idx={highestVelIndex} |v|={highestVelEver:F4} (baseline pre-fix: ~642+)");
        sb.AppendLine($"  per-frame highest-|v| monotonic increases: {monotonicIncreases}/{FrameCount - 1}");
        sb.AppendLine($"  frame0 highest|v|={perFrameHighestVelMag[0]:F4} frame119 highest|v|={perFrameHighestVelMag[FrameCount - 1]:F4}");
        sb.AppendLine($"  outlier index at frame119: {perFrameHighestVelIndex[FrameCount - 1]}");
        Debug.Log(sb.ToString());

        Object.DestroyImmediate(pipeline.gameObject);
    }

    /// <summary>
    /// Population correlation (post-carry |v| vs post-PBF Y) and PBF sub-stage amplification trace.
    /// </summary>
    [Test]
    [Category("Exploratory")]
    [Category("Diagnostic")]
    public void RigidCarrySpin_PopulationCorrelationAndPbfAmplification_Diagnostics()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        RunPopulationCorrelationDiagnostics();
    }

    private static void RunPopulationCorrelationDiagnostics()
    {
        const float explosionThreshold = -10f;

        int explosionFrame = RunSpinFindExplosionFrame(explosionThreshold, out float finalMinY);
        if (explosionFrame < 0)
        {
            Debug.Log(
                $"[RigidCarrySpin:PopCorr] minLocalYInRadius never dropped below {explosionThreshold:F1} " +
                $"(finalMinY={finalMinY:F4}). Skipping population correlation.");
            return;
        }

        Debug.Log($"[RigidCarrySpin:PopCorr] first minLocalYInRadius<{explosionThreshold:F1} at frame={explosionFrame}");

        ReplaySpinThroughExplosionFrame(
            explosionFrame,
            out PopulationFrameSnapshot snapshot,
            out List<PbfStageSample> pbfTrace);

        ReportPopulationCorrelation(snapshot, explosionFrame);
        ReportPbfAmplification(pbfTrace, snapshot.HighestCarryVelIndex, snapshot.WorldToLocal);
    }

    private static int RunSpinFindExplosionFrame(float threshold, out float finalMinY)
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        InitializeSpinPipeline(pipeline, out Vector3 floorPivot, out float radius, out float height, out Matrix4x4 prevLocalToWorld);

        finalMinY = float.MaxValue;
        int explosionFrame = -1;

        for (int frame = 0; frame < FrameCount; frame++)
        {
            AdvanceSpinFrame(pipeline, ref prevLocalToWorld, floorPivot, radius, height, frame, runPbf: true, out _);
            float minY = ScanMinLocalYInRadius(pipeline, prevLocalToWorld.inverse, radius);
            finalMinY = Mathf.Min(finalMinY, minY);
            if (explosionFrame < 0 && minY < threshold)
            {
                explosionFrame = frame;
            }
        }

        Object.DestroyImmediate(pipeline.gameObject);
        return explosionFrame;
    }

    private static void ReplaySpinThroughExplosionFrame(
        int explosionFrame,
        out PopulationFrameSnapshot snapshot,
        out List<PbfStageSample> pbfTrace)
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        InitializeSpinPipeline(pipeline, out Vector3 floorPivot, out float radius, out float height, out Matrix4x4 prevLocalToWorld);

        snapshot = default;
        pbfTrace = new List<PbfStageSample>();

        for (int frame = 0; frame <= explosionFrame; frame++)
        {
            AdvanceSpinFrame(
                pipeline,
                ref prevLocalToWorld,
                floorPivot,
                radius,
                height,
                frame,
                runPbf: true,
                out PopulationFrameSnapshot frameSnapshot);

            if (frame == explosionFrame)
            {
                snapshot = frameSnapshot;
                snapshot.PostPbfLocalY = ScanPostPbfLocalYForIndices(pipeline, frameSnapshot.WorldToLocal, snapshot.Indices);
                pbfTrace = ReplayExplosionFramePbfTrace(explosionFrame, floorPivot, radius, height);
            }
        }

        Object.DestroyImmediate(pipeline.gameObject);
    }

    private static List<PbfStageSample> ReplayExplosionFramePbfTrace(
        int explosionFrame,
        Vector3 floorPivot,
        float radius,
        float height)
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        InitializeSpinPipeline(pipeline, out _, out _, out _, out Matrix4x4 prevLocalToWorld);

        PopulationFrameSnapshot snapshot = default;
        for (int frame = 0; frame <= explosionFrame; frame++)
        {
            AdvanceSpinFrame(
                pipeline,
                ref prevLocalToWorld,
                floorPivot,
                radius,
                height,
                frame,
                runPbf: frame < explosionFrame,
                out PopulationFrameSnapshot frameSnapshot);

            if (frame == explosionFrame)
            {
                snapshot = frameSnapshot;
            }
        }

        List<PbfStageSample> trace = ExecuteInstrumentedPbfFrame(
            pipeline,
            snapshot.HighestCarryVelIndex,
            snapshot.WorldToLocal);
        Object.DestroyImmediate(pipeline.gameObject);
        return trace;
    }

    private static void InitializeSpinPipeline(
        HarmonicPipelineController pipeline,
        out Vector3 floorPivot,
        out float radius,
        out float height,
        out Matrix4x4 prevLocalToWorld)
    {
        floorPivot = Vector3.zero;
        radius = 0.55f;
        height = 1.1f;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(
            floorPivot,
            Quaternion.identity,
            radius,
            height,
            restitution: 0.1f,
            friction: 0.85f,
            wallStiffness: 400f);

        int spawned = pipeline.TrySpawnContainerLatticeFill();
        Assert.Greater(spawned, 0, "Expected lattice particles near the container floor.");
        prevLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
    }

    private static void AdvanceSpinFrame(
        HarmonicPipelineController pipeline,
        ref Matrix4x4 prevLocalToWorld,
        Vector3 floorPivot,
        float radius,
        float height,
        int frame,
        bool runPbf,
        out PopulationFrameSnapshot snapshot)
    {
        float t = frame * SpinDegreesPerFrame;
        Quaternion rotation = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
        Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rotation);
        Matrix4x4 worldToLocal = currLocalToWorld.inverse;

        pipeline.SetContainerFluidOriented(
            floorPivot,
            rotation,
            radius,
            height,
            restitution: 0.1f,
            friction: 0.85f,
            wallStiffness: 400f);

        pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
        SyncGpu();

        snapshot = CollectPostCarryInRadiusSnapshot(pipeline, worldToLocal, radius);

        if (runPbf)
        {
            pipeline.ExecutePipelineFrame(DeltaTime);
            SyncGpu();
        }

        prevLocalToWorld = currLocalToWorld;
    }

    private static Dictionary<int, float> ScanPostPbfLocalYForIndices(
        HarmonicPipelineController pipeline,
        Matrix4x4 worldToLocal,
        List<int> indices)
    {
        var map = new Dictionary<int, float>(indices.Count);
        if (indices == null || indices.Count == 0
            || !pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount)
            || activeCount == 0)
        {
            return map;
        }

        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticlesAtIndices(
            soa,
            indices.ToArray());
        for (int i = 0; i < indices.Count; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
            map[indices[i]] = local.y;
        }

        return map;
    }

    private static PopulationFrameSnapshot CollectPostCarryInRadiusSnapshot(
        HarmonicPipelineController pipeline,
        Matrix4x4 worldToLocal,
        float radius)
    {
        var snapshot = new PopulationFrameSnapshot { WorldToLocal = worldToLocal };

        if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) || activeCount == 0)
        {
            return snapshot;
        }

        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
        snapshot.Indices = new List<int>(particles.Length);
        snapshot.CarryVelMagnitudes = new List<float>(particles.Length);

        float highestVel = 0f;
        int highestIndex = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
            float radial = new Vector2(local.x, local.z).magnitude;
            if (radial > radius + 1e-4f)
            {
                continue;
            }

            float velMag = new Vector3(particles[i].Velocity.x, particles[i].Velocity.y, particles[i].Velocity.z).magnitude;
            snapshot.Indices.Add(i);
            snapshot.CarryVelMagnitudes.Add(velMag);
            if (velMag > highestVel)
            {
                highestVel = velMag;
                highestIndex = i;
            }
        }

        snapshot.HighestCarryVelIndex = highestIndex;
        snapshot.HighestCarryVelMagnitude = highestVel;
        return snapshot;
    }

    private static float ScanMinLocalYInRadius(HarmonicPipelineController pipeline, Matrix4x4 worldToLocal, float radius)
    {
        float minY = float.MaxValue;
        if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) || activeCount == 0)
        {
            return minY;
        }

        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
            float radial = new Vector2(local.x, local.z).magnitude;
            if (radial <= radius + 1e-4f)
            {
                minY = Mathf.Min(minY, local.y);
            }
        }

        return minY;
    }

    private static Dictionary<int, float> ScanPerParticleLocalY(
        HarmonicPipelineController pipeline,
        Matrix4x4 worldToLocal,
        float radius)
    {
        var map = new Dictionary<int, float>();
        if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) || activeCount == 0)
        {
            return map;
        }

        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
            float radial = new Vector2(local.x, local.z).magnitude;
            if (radial <= radius + 1e-4f)
            {
                map[i] = local.y;
            }
        }

        return map;
    }

    private const int CornerTraceIdx2651 = 2651;
    private const int CornerTraceIdx670 = 670;
    private const int CornerTraceFrame2651Start = 105;
    private const int CornerTraceFrame2651End = 116;
    private const int CornerTraceFrame670Start = 113;
    private const int CornerTraceFrame670End = 117;
    private const int CornerTraceRunThroughFrame = 117;

    private static void RunCornerPbfSubstageDiagnostics()
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        InitializeSpinPipeline(pipeline, out Vector3 floorPivot, out float radius, out float height, out Matrix4x4 prevLocalToWorld);

        var traces2651 = new Dictionary<int, List<CornerPbfSubstageSample>>();
        var traces670 = new Dictionary<int, List<CornerPbfSubstageSample>>();

        for (int frame = 0; frame <= CornerTraceRunThroughFrame; frame++)
        {
            float t = frame * SpinDegreesPerFrame;
            Quaternion rotation = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rotation);
            Matrix4x4 worldToLocal = currLocalToWorld.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot,
                rotation,
                radius,
                height,
                restitution: 0.1f,
                friction: 0.85f,
                wallStiffness: 400f);

            pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
            SyncGpu();

            PingPongSoaManager pingPong = GetPingPong(pipeline);
            float postCarryVel2651 = 0f;
            float postCarryLocalY2651 = 0f;
            float postCarryVel670 = 0f;
            float postCarryLocalY670 = 0f;
            if (pipeline.TryGetInternalParticleSoa(out _, out uint activeForCarry) && activeForCarry > 0)
            {
                if (CornerTraceIdx2651 < activeForCarry)
                {
                    Vector3 v2651 = ReadParticleVelocity(pingPong.ReadSet, CornerTraceIdx2651);
                    postCarryVel2651 = v2651.magnitude;
                    postCarryLocalY2651 = worldToLocal.MultiplyPoint3x4(ReadParticlePosition(pingPong.ReadSet, CornerTraceIdx2651)).y;
                }

                if (CornerTraceIdx670 < activeForCarry)
                {
                    Vector3 v670 = ReadParticleVelocity(pingPong.ReadSet, CornerTraceIdx670);
                    postCarryVel670 = v670.magnitude;
                    postCarryLocalY670 = worldToLocal.MultiplyPoint3x4(ReadParticlePosition(pingPong.ReadSet, CornerTraceIdx670)).y;
                }
            }

            bool trace2651 = frame >= CornerTraceFrame2651Start && frame <= CornerTraceFrame2651End;
            bool trace670 = frame >= CornerTraceFrame670Start && frame <= CornerTraceFrame670End;

            if (trace2651 || trace670)
            {
                var indices = new List<int>(2);
                if (trace2651)
                {
                    indices.Add(CornerTraceIdx2651);
                }

                if (trace670)
                {
                    indices.Add(CornerTraceIdx670);
                }

                var boundary = new OtcBoundaryParams(
                    radius,
                    height,
                    restitution: 0.1f,
                    friction: 0.85f,
                    worldToLocal,
                    currLocalToWorld);

                Dictionary<int, List<CornerPbfSubstageSample>> frameTrace =
                    ExecuteCornerPbfSubstageTrace(pipeline, indices, boundary);

                if (trace2651 && frameTrace.TryGetValue(CornerTraceIdx2651, out List<CornerPbfSubstageSample> t2651))
                {
                    traces2651[frame] = t2651;
                    t2651.Insert(0, new CornerPbfSubstageSample(
                        "postCarry",
                        ReadParticlePosition(pingPong.ReadSet, CornerTraceIdx2651),
                        boundary,
                        ReadParticleVelocity(pingPong.ReadSet, CornerTraceIdx2651),
                        density: float.NaN,
                        lambda: float.NaN,
                        gradSqSum: float.NaN,
                        densityLambdaEarlyOut: false,
                        posDeltaFromPrev: Vector3.zero,
                        floorClampFired: false,
                        wallClampFired: false,
                        clampNote: $"postCarry|v|={postCarryVel2651:F4} localY={postCarryLocalY2651:F6}"));
                }

                if (trace670 && frameTrace.TryGetValue(CornerTraceIdx670, out List<CornerPbfSubstageSample> t670))
                {
                    traces670[frame] = t670;
                    t670.Insert(0, new CornerPbfSubstageSample(
                        "postCarry",
                        ReadParticlePosition(pingPong.ReadSet, CornerTraceIdx670),
                        boundary,
                        ReadParticleVelocity(pingPong.ReadSet, CornerTraceIdx670),
                        density: float.NaN,
                        lambda: float.NaN,
                        gradSqSum: float.NaN,
                        densityLambdaEarlyOut: false,
                        posDeltaFromPrev: Vector3.zero,
                        floorClampFired: false,
                        wallClampFired: false,
                        clampNote: $"postCarry|v|={postCarryVel670:F4} localY={postCarryLocalY670:F6}"));
                }
            }
            else
            {
                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
            }

            prevLocalToWorld = currLocalToWorld;
        }

        Object.DestroyImmediate(pipeline.gameObject);

        var sb = new StringBuilder();
        sb.AppendLine("[RigidCarrySpin:CornerPbf] floor-wall corner PBF sub-stage trace (single-pass, post-carry instrumented PBF):");
        sb.AppendLine($"  idx={CornerTraceIdx2651} frames {CornerTraceFrame2651Start}-{CornerTraceFrame2651End}");
        sb.AppendLine($"  idx={CornerTraceIdx670} frames {CornerTraceFrame670Start}-{CornerTraceFrame670End}");

        foreach (KeyValuePair<int, List<CornerPbfSubstageSample>> kv in traces2651.OrderBy(k => k.Key))
        {
            sb.AppendLine(FormatCornerFrameTrace(CornerTraceIdx2651, kv.Key, kv.Value));
        }

        foreach (KeyValuePair<int, List<CornerPbfSubstageSample>> kv in traces670.OrderBy(k => k.Key))
        {
            sb.AppendLine(FormatCornerFrameTrace(CornerTraceIdx670, kv.Key, kv.Value));
        }

        sb.AppendLine(AnalyzeCornerTraces(CornerTraceIdx2651, traces2651));
        sb.AppendLine(AnalyzeCornerTraces(CornerTraceIdx670, traces670));
        sb.AppendLine(CompareCornerMechanisms(traces2651, traces670));

        string report = sb.ToString();
        string logPath = System.IO.Path.Combine(
            Application.dataPath,
            "..",
            "Logs",
            "HarmonicSimulation",
            "corner_pbf_substage_trace.log");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log(report);
        Debug.Log($"[RigidCarrySpin:CornerPbf] full trace written to {logPath}");
    }

    private static Dictionary<int, List<CornerPbfSubstageSample>> ExecuteCornerPbfSubstageTrace(
        HarmonicPipelineController pipeline,
        IReadOnlyList<int> traceIndices,
        OtcBoundaryParams boundary)
    {
        var result = new Dictionary<int, List<CornerPbfSubstageSample>>();
        foreach (int idx in traceIndices)
        {
            result[idx] = new List<CornerPbfSubstageSample>();
        }

        PingPongSoaManager pingPong = GetPingPong(pipeline);
        InvokeBeginPipelineFrame(pipeline);
        uint activeCount = InvokeSanitizeAndRepairActiveCount(pipeline);
        pipeline.SetCachedInternalCount(activeCount);
        if (activeCount == 0)
        {
            return result;
        }

        float deltaTime = DeltaTime;
        int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / pipeline.ContainerFluidMaxTimeStep), 1, 2);
        float subDt = deltaTime / steps;

        for (int step = 0; step < steps; step++)
        {
            pipeline.ComputeFrameSortSize(activeCount);
            pipeline.ClassifyParticleFieldFrom(pingPong.ReadSet.Block0, activeCount);
            InvokeSpatialHashBuild(pipeline, pingPong.ReadSet, activeCount);

            foreach (int idx in traceIndices)
            {
                SampleCornerStage(
                    result[idx],
                    "prePredict",
                    pingPong.ReadSet.Block0,
                    pingPong.ReadSet,
                    idx,
                    boundary,
                    readVelFromSoa: true);
            }

            float smoothingRadius = pipeline.SphSmoothingRadius;
            pipeline.ApplyPbfUniforms(smoothingRadius, subDt);

            DispatchPbfPredict(pipeline, activeCount);
            SyncGpu();

            InvokeSpatialHashBuild(pipeline, pipeline.PbfScratch.PredictedBlock0, activeCount);

            foreach (int idx in traceIndices)
            {
                SampleCornerStage(
                    result[idx],
                    "postPredict",
                    pipeline.PbfScratch.PredictedBlock0,
                    pingPong.ReadSet,
                    idx,
                    boundary,
                    readVelFromSoa: false,
                    velSource: pingPong.ReadSet,
                    includePredictClampAudit: true);
            }

            int pbfIterations = pipeline.PbfIterations;
            for (int iter = 0; iter < pbfIterations; iter++)
            {
                pipeline.ClassifyParticleFieldFrom(pipeline.PbfScratch.PredictedBlock0, activeCount);

                foreach (int idx in traceIndices)
                {
                    Vector3 preSolvePos = ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx);
                    result[idx].Add(new CornerPbfSubstageSample(
                        $"preSolveIter{iter}",
                        preSolvePos,
                        boundary,
                        Vector3.zero,
                        density: float.NaN,
                        lambda: float.NaN,
                        gradSqSum: float.NaN,
                        densityLambdaEarlyOut: false,
                        posDeltaFromPrev: Vector3.zero,
                        floorClampFired: false,
                        wallClampFired: false,
                        clampNote: string.Empty));
                }

                DispatchPbfDensity(pipeline, activeCount);
                SyncGpu();

                foreach (int idx in traceIndices)
                {
                    float density = ReadFloatBufferSample(GetDensityBuffer(pipeline), idx);
                    ClassifyOtc(ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx), boundary,
                        out _, out _, out bool participates);
                    bool earlyOut = !participates && Mathf.Abs(density) < 1e-8f;
                    result[idx].Add(new CornerPbfSubstageSample(
                        $"postDensityIter{iter}",
                        ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx),
                        boundary,
                        Vector3.zero,
                        density,
                        lambda: float.NaN,
                        gradSqSum: float.NaN,
                        earlyOut,
                        posDeltaFromPrev: Vector3.zero,
                        floorClampFired: false,
                        wallClampFired: false,
                        clampNote: earlyOut ? "earlyOut(!participates)" : "fullDensity"));
                }

                DispatchPbfLambda(pipeline, activeCount);
                SyncGpu();

                foreach (int idx in traceIndices)
                {
                    float lambda = ReadFloatBufferSample(pipeline.PbfScratch.Lambdas, idx);
                    float gradSq = ReadFloatBufferSample(pipeline.PbfScratch.GradSqSum, idx);
                    ClassifyOtc(ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx), boundary,
                        out _, out _, out bool participates);
                    bool earlyOut = !participates && Mathf.Abs(lambda) < 1e-8f && Mathf.Abs(gradSq) < 1e-8f;
                    result[idx].Add(new CornerPbfSubstageSample(
                        $"postLambdaIter{iter}",
                        ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx),
                        boundary,
                        Vector3.zero,
                        ReadFloatBufferSample(GetDensityBuffer(pipeline), idx),
                        lambda,
                        gradSq,
                        earlyOut,
                        posDeltaFromPrev: Vector3.zero,
                        floorClampFired: false,
                        wallClampFired: false,
                        clampNote: earlyOut ? "earlyOut(!participates)" : "fullLambda"));
                }

                DispatchPbfSolve(pipeline, activeCount);
                SyncGpu();

                foreach (int idx in traceIndices)
                {
                    CornerPbfSubstageSample preSolve = result[idx].Last(s => s.Stage == $"preSolveIter{iter}");
                    Vector3 postSolvePos = ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx);
                    Vector3 move = postSolvePos - preSolve.WorldPosition;
                    InferSolveClamp(preSolve, postSolvePos, boundary,
                        out bool floorFired,
                        out bool wallFired,
                        out string clampNote);
                    result[idx].Add(new CornerPbfSubstageSample(
                        $"postSolveIter{iter}",
                        postSolvePos,
                        boundary,
                        Vector3.zero,
                        density: float.NaN,
                        lambda: float.NaN,
                        gradSqSum: float.NaN,
                        densityLambdaEarlyOut: false,
                        posDeltaFromPrev: move,
                        floorFired,
                        wallFired,
                        clampNote));
                }
            }

            foreach (int idx in traceIndices)
            {
                Vector3 oldPos = ReadBufferPosition(pipeline.PbfScratch.OldBlock0, idx);
                Vector3 predictedPos = ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx);
                result[idx].Add(new CornerPbfSubstageSample(
                    "preApply",
                    predictedPos,
                    boundary,
                    (predictedPos - oldPos) / Mathf.Max(subDt, 1e-6f),
                    density: ReadFloatBufferSample(GetDensityBuffer(pipeline), idx),
                    lambda: float.NaN,
                    gradSqSum: float.NaN,
                    densityLambdaEarlyOut: false,
                    posDeltaFromPrev: Vector3.zero,
                    floorClampFired: false,
                    wallClampFired: false,
                    clampNote: $"oldY={boundary.WorldToLocal.MultiplyPoint3x4(oldPos).y:F6}"));
            }

            DispatchPbfApply(pipeline, pingPong, activeCount);
            SyncGpu();

            foreach (int idx in traceIndices)
            {
                Vector3 predictedPos = ReadBufferPosition(pipeline.PbfScratch.PredictedBlock0, idx);
                Vector3 oldPos = ReadBufferPosition(pipeline.PbfScratch.OldBlock0, idx);
                Vector3 velDerived = (predictedPos - oldPos) / Mathf.Max(subDt, 1e-6f);
                velDerived *= GetPbfVelocityDamping(pipeline);

                Vector3 posBeforeClamp = predictedPos;
                Vector3 velBeforeClamp = velDerived;
                SimulateOtcClampToContainer(ref posBeforeClamp, ref velBeforeClamp, boundary,
                    out bool floorSim, out bool wallSim);

                Vector3 finalPos = ReadBufferPosition(pingPong.WriteSet.Block0, idx);
                Vector3 finalVel = ReadParticleVelocity(pingPong.WriteSet, idx);

                string applyNote =
                    $"applyClamp floorSim={floorSim} wallSim={wallSim} " +
                    $"posDelta=({finalPos.x - predictedPos.x:F6},{finalPos.y - predictedPos.y:F6},{finalPos.z - predictedPos.z:F6}) " +
                    $"velDerivedMag={velDerived.magnitude:F4} finalVelMag={finalVel.magnitude:F4}";

                SampleCornerStage(
                    result[idx],
                    "postApply",
                    pingPong.WriteSet.Block0,
                    pingPong.WriteSet,
                    idx,
                    boundary,
                    readVelFromSoa: true,
                    applyClampNote: applyNote,
                    floorClampFired: floorSim,
                    wallClampFired: wallSim);
            }

            // Apply appended survivors and set the counter itself (mirrors production); do not overwrite.
            pingPong.Swap();
            activeCount = InvokeRepairParticleCount(pipeline, pingPong.ReadSet);
        }

        pipeline.SetCachedInternalCount(activeCount);
        return result;
    }

    private static ComputeBuffer GetDensityBuffer(HarmonicPipelineController pipeline)
    {
        return pipeline.TryGetDensityCacheBuffers(out ComputeBuffer densities, out _, out _)
            ? densities
            : null;
    }

    private static float GetPbfVelocityDamping(HarmonicPipelineController pipeline)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            "pbfVelocityDamping",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (float)field.GetValue(pipeline) : 1f;
    }

    private static float ReadFloatBufferSample(ComputeBuffer buffer, int index)
    {
        if (buffer == null || index < 0 || index >= buffer.count)
        {
            return float.NaN;
        }

        var scratch = new float[1];
        buffer.GetData(scratch, 0, index, 1);
        return scratch[0];
    }

    private static Vector3 ReadBufferPosition(ComputeBuffer block0, int index)
    {
        var block = new Vector4[1];
        block0.GetData(block, 0, index, 1);
        return new Vector3(block[0].x, block[0].y, block[0].z);
    }

    private static void SampleCornerStage(
        List<CornerPbfSubstageSample> trace,
        string stage,
        ComputeBuffer positionBuffer,
        ParticleSoaBuffers velSoa,
        int index,
        OtcBoundaryParams boundary,
        bool readVelFromSoa,
        ParticleSoaBuffers velSource = null,
        bool includePredictClampAudit = false,
        string applyClampNote = null,
        bool floorClampFired = false,
        bool wallClampFired = false)
    {
        Vector3 worldPos = ReadBufferPosition(positionBuffer, index);
        Vector3 vel = Vector3.zero;
        if (readVelFromSoa && velSoa != null)
        {
            vel = ReadParticleVelocity(velSoa, index);
        }

        string note = applyClampNote ?? string.Empty;
        if (includePredictClampAudit && velSource != null)
        {
            Vector3 prePos = ReadBufferPosition(velSource.Block0, index);
            Vector3 preVel = ReadParticleVelocity(velSource, index);
            Vector3 simPos = prePos;
            Vector3 simVel = preVel;
            SimulateOtcClampToContainer(ref simPos, ref simVel, boundary, out bool floorSim, out bool wallSim);
            note = $"predictClampAudit floorSim={floorSim} wallSim={wallSim} preLocalY={boundary.WorldToLocal.MultiplyPoint3x4(prePos).y:F6}";
            floorClampFired = floorSim;
            wallClampFired = wallSim;
        }

        Vector3 prevPos = trace.Count > 0 ? trace[trace.Count - 1].WorldPosition : worldPos;
        trace.Add(new CornerPbfSubstageSample(
            stage,
            worldPos,
            boundary,
            vel,
            density: float.NaN,
            lambda: float.NaN,
            gradSqSum: float.NaN,
            densityLambdaEarlyOut: false,
            posDeltaFromPrev: worldPos - prevPos,
            floorClampFired,
            wallClampFired,
            note));
    }

    private static void InferSolveClamp(
        CornerPbfSubstageSample preSolve,
        Vector3 postSolvePos,
        OtcBoundaryParams boundary,
        out bool floorClampFired,
        out bool wallClampFired,
        out string clampNote)
    {
        Vector3 preLocal = preSolve.LocalPosition;
        Vector3 postLocal = boundary.WorldToLocal.MultiplyPoint3x4(postSolvePos);
        float preR = preSolve.Radial;
        float postR = new Vector2(postLocal.x, postLocal.z).magnitude;

        floorClampFired = preLocal.y < -1e-6f && postLocal.y >= -1e-6f;
        wallClampFired = preR > boundary.Radius + 1e-6f && Mathf.Abs(postR - boundary.Radius) <= 1e-5f;

        Vector3 move = postSolvePos - preSolve.WorldPosition;
        clampNote = floorClampFired && wallClampFired
            ? "DUAL floor+wall clamp inferred"
            : floorClampFired
                ? "floor clamp inferred"
                : wallClampFired
                    ? "wall clamp inferred"
                    : "no clamp inferred";

        if (floorClampFired || wallClampFired)
        {
            clampNote += $" preLocal=({preLocal.x:F6},{preLocal.y:F6},{preLocal.z:F6}) postLocal=({postLocal.x:F6},{postLocal.y:F6},{postLocal.z:F6}) move=({move.x:F6},{move.y:F6},{move.z:F6})";
        }
    }

    private static void ClassifyOtc(
        Vector3 worldPos,
        OtcBoundaryParams boundary,
        out Vector3 local,
        out float radial,
        out bool participatesInPbf)
    {
        local = boundary.WorldToLocal.MultiplyPoint3x4(worldPos);
        radial = new Vector2(local.x, local.z).magnitude;
        participatesInPbf = radial <= boundary.Radius;
    }

    private static void SimulateOtcClampToContainer(
        ref Vector3 pos,
        ref Vector3 vel,
        OtcBoundaryParams boundary,
        out bool floorFired,
        out bool wallFired)
    {
        floorFired = false;
        wallFired = false;

        Vector3 localPos = boundary.WorldToLocal.MultiplyPoint3x4(pos);
        Vector3 localVel = boundary.WorldToLocal.MultiplyVector(vel);
        float radialDist = new Vector2(localPos.x, localPos.z).magnitude;
        Vector3 localBefore = localPos;

        if (localPos.y < 0f && radialDist <= boundary.Radius)
        {
            if (localPos.y < -1e-7f || localVel.y < -1e-6f)
            {
                floorFired = true;
            }

            localPos.y = 0f;
            if (localVel.y < 0f)
            {
                localVel.y = -localVel.y * boundary.Restitution;
            }

            localVel.x *= boundary.Friction;
            localVel.z *= boundary.Friction;
        }

        if (!(localPos.y > boundary.Height || radialDist <= boundary.Radius || radialDist < 1e-5f))
        {
            wallFired = true;
            Vector2 n = new Vector2(localPos.x, localPos.z) / radialDist;
            localPos.x = n.x * boundary.Radius;
            localPos.z = n.y * boundary.Radius;

            float vn = Vector2.Dot(new Vector2(localVel.x, localVel.z), n);
            if (Mathf.Abs(vn) > 1e-6f)
            {
                Vector2 vNormal = vn * n;
                Vector2 vTangent = new Vector2(localVel.x, localVel.z) - vNormal;
                localVel.x = vTangent.x * boundary.Friction - vNormal.x * boundary.Restitution;
                localVel.z = vTangent.y * boundary.Friction - vNormal.y * boundary.Restitution;
            }
        }

        if (!floorFired && !wallFired && (localPos - localBefore).sqrMagnitude > 1e-12f)
        {
            floorFired = localBefore.y < 0f && localPos.y >= 0f;
            wallFired = !floorFired;
        }

        pos = boundary.LocalToWorld.MultiplyPoint3x4(localPos);
        vel = boundary.LocalToWorld.MultiplyVector(localVel);
    }

    private static string FormatCornerFrameTrace(int particleIndex, int frame, List<CornerPbfSubstageSample> samples)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- particle={particleIndex} frame={frame} ({samples.Count} sub-stages) ---");
        foreach (CornerPbfSubstageSample s in samples)
        {
            sb.AppendLine(
                $"  {s.Stage}: pos=({s.WorldPosition.x:F6},{s.WorldPosition.y:F6},{s.WorldPosition.z:F6}) " +
                $"localY={s.LocalPosition.y:F6} r={s.Radial:F8} |v|={s.Velocity.magnitude:F4} " +
                $"outsideFp={s.OutsideFootprint} participatesPbf={s.ParticipatesInPbf} " +
                (float.IsNaN(s.Density) ? string.Empty : $"density={s.Density:F4} ") +
                (float.IsNaN(s.Lambda) ? string.Empty : $"lambda={s.Lambda:F6} gradSq={s.GradSqSum:F6} ") +
                (s.DensityLambdaEarlyOut ? "EARLY_OUT " : string.Empty) +
                $"dPos=({s.PosDeltaFromPrev.x:F6},{s.PosDeltaFromPrev.y:F6},{s.PosDeltaFromPrev.z:F6}) " +
                $"floorClamp={s.FloorClampFired} wallClamp={s.WallClampFired} {s.ClampNote}");
        }

        return sb.ToString();
    }

    private static string AnalyzeCornerTraces(int particleIndex, Dictionary<int, List<CornerPbfSubstageSample>> traces)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[RigidCarrySpin:CornerPbf:Analysis] particle={particleIndex}:");

        int classificationFlips = 0;
        int participatesFlips = 0;
        int dualClampEvents = 0;
        int solveOscillationFrames = 0;
        float worstLocalY = float.MaxValue;
        int worstFrame = -1;

        foreach (KeyValuePair<int, List<CornerPbfSubstageSample>> kv in traces.OrderBy(k => k.Key))
        {
            int frame = kv.Key;
            List<CornerPbfSubstageSample> samples = kv.Value;
            bool? prevOutside = null;
            bool? prevParticipates = null;

            foreach (CornerPbfSubstageSample s in samples)
            {
                if (s.LocalPosition.y < worstLocalY)
                {
                    worstLocalY = s.LocalPosition.y;
                    worstFrame = frame;
                }

                if (prevOutside.HasValue && prevOutside.Value != s.OutsideFootprint)
                {
                    classificationFlips++;
                }

                if (prevParticipates.HasValue && prevParticipates.Value != s.ParticipatesInPbf)
                {
                    participatesFlips++;
                }

                prevOutside = s.OutsideFootprint;
                prevParticipates = s.ParticipatesInPbf;

                if (s.FloorClampFired && s.WallClampFired)
                {
                    dualClampEvents++;
                }
            }

            var solveStages = samples.Where(s => s.Stage.StartsWith("postSolveIter")).ToList();
            if (solveStages.Count >= 2)
            {
                bool oscillating = false;
                for (int i = 1; i < solveStages.Count; i++)
                {
                    float prevDy = solveStages[i - 1].PosDeltaFromPrev.y;
                    float currDy = solveStages[i].PosDeltaFromPrev.y;
                    float prevDr = solveStages[i - 1].PosDeltaFromPrev.magnitude;
                    float currDr = solveStages[i].PosDeltaFromPrev.magnitude;
                    if (Mathf.Abs(prevDy) > 1e-5f && Mathf.Abs(currDy) > 1e-5f && Mathf.Sign(prevDy) != Mathf.Sign(currDy))
                    {
                        oscillating = true;
                    }

                    if (Mathf.Abs(solveStages[i].Radial - 0.55f) <= 1e-4f
                        && Mathf.Abs(prevDr) > 1e-5f
                        && Mathf.Abs(currDr) > 1e-5f
                        && Mathf.Sign(prevDr) != Mathf.Sign(currDr))
                    {
                        oscillating = true;
                    }
                }

                if (oscillating)
                {
                    solveOscillationFrames++;
                }
            }
        }

        sb.AppendLine($"  worstLocalY={worstLocalY:F6} at frame={worstFrame}");
        sb.AppendLine($"  outsideFootprint flips (sub-stage to sub-stage): {classificationFlips}");
        sb.AppendLine($"  participatesPbf flips (sub-stage to sub-stage): {participatesFlips}");
        sb.AppendLine($"  dual floor+wall clamp events (inferred): {dualClampEvents}");
        sb.AppendLine($"  frames with solve-iter position sign alternation: {solveOscillationFrames}/{traces.Count}");

        int atWallSamples = traces.SelectMany(t => t.Value).Count(s => Mathf.Abs(s.Radial - 0.55f) <= 1e-4f);
        int atWallOutside = traces.SelectMany(t => t.Value).Count(s => Mathf.Abs(s.Radial - 0.55f) <= 1e-4f && s.OutsideFootprint);
        sb.AppendLine($"  samples at r≈0.55: {atWallSamples}, of which outsideFootprint=true: {atWallOutside}");
        sb.AppendLine($"  OtcIsOutsideFootprint vs OtcParticipatesInPbf logical disagreement: impossible by definition (!outside); flips indicate FP boundary at r=radius");

        return sb.ToString();
    }

    private static string CompareCornerMechanisms(
        Dictionary<int, List<CornerPbfSubstageSample>> traces2651,
        Dictionary<int, List<CornerPbfSubstageSample>> traces670)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[RigidCarrySpin:CornerPbf:Compare] idx=2651 vs idx=670 (overlap frames 113-116):");

        for (int frame = CornerTraceFrame670Start; frame <= CornerTraceFrame2651End; frame++)
        {
            if (!traces2651.TryGetValue(frame, out List<CornerPbfSubstageSample> s2651)
                || !traces670.TryGetValue(frame, out List<CornerPbfSubstageSample> s670))
            {
                continue;
            }

            CornerPbfSubstageSample p2651 = s2651.Last(s => s.Stage == "postApply");
            CornerPbfSubstageSample p670 = s670.Last(s => s.Stage == "postApply");
            sb.AppendLine(
                $"  frame={frame}: 2651 postApply localY={p2651.LocalPosition.y:F4} r={p2651.Radial:F4} |v|={p2651.Velocity.magnitude:F1} " +
                $"670 postApply localY={p670.LocalPosition.y:F4} r={p670.Radial:F4} |v|={p670.Velocity.magnitude:F1}");
        }

        return sb.ToString();
    }

    private readonly struct OtcBoundaryParams
    {
        public readonly float Radius;
        public readonly float Height;
        public readonly float Restitution;
        public readonly float Friction;
        public readonly Matrix4x4 WorldToLocal;
        public readonly Matrix4x4 LocalToWorld;

        public OtcBoundaryParams(
            float radius,
            float height,
            float restitution,
            float friction,
            Matrix4x4 worldToLocal,
            Matrix4x4 localToWorld)
        {
            Radius = radius;
            Height = height;
            Restitution = restitution;
            Friction = friction;
            WorldToLocal = worldToLocal;
            LocalToWorld = localToWorld;
        }
    }

    private readonly struct CornerPbfSubstageSample
    {
        public readonly string Stage;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 LocalPosition;
        public readonly float Radial;
        public readonly Vector3 Velocity;
        public readonly bool OutsideFootprint;
        public readonly bool ParticipatesInPbf;
        public readonly float Density;
        public readonly float Lambda;
        public readonly float GradSqSum;
        public readonly bool DensityLambdaEarlyOut;
        public readonly Vector3 PosDeltaFromPrev;
        public readonly bool FloorClampFired;
        public readonly bool WallClampFired;
        public readonly string ClampNote;

        public CornerPbfSubstageSample(
            string stage,
            Vector3 worldPosition,
            OtcBoundaryParams boundary,
            Vector3 velocity,
            float density,
            float lambda,
            float gradSqSum,
            bool densityLambdaEarlyOut,
            Vector3 posDeltaFromPrev,
            bool floorClampFired,
            bool wallClampFired,
            string clampNote)
        {
            Stage = stage;
            WorldPosition = worldPosition;
            LocalPosition = boundary.WorldToLocal.MultiplyPoint3x4(worldPosition);
            Radial = new Vector2(LocalPosition.x, LocalPosition.z).magnitude;
            Velocity = velocity;
            OutsideFootprint = Radial > boundary.Radius;
            ParticipatesInPbf = !OutsideFootprint;
            Density = density;
            Lambda = lambda;
            GradSqSum = gradSqSum;
            DensityLambdaEarlyOut = densityLambdaEarlyOut;
            PosDeltaFromPrev = posDeltaFromPrev;
            FloorClampFired = floorClampFired;
            WallClampFired = wallClampFired;
            ClampNote = clampNote ?? string.Empty;
        }
    }

    private static List<PbfStageSample> ExecuteInstrumentedPbfFrame(
        HarmonicPipelineController pipeline,
        int traceIndex,
        Matrix4x4 worldToLocal)
    {
        var trace = new List<PbfStageSample>();
        PingPongSoaManager pingPong = GetPingPong(pipeline);

        InvokeBeginPipelineFrame(pipeline);
        uint activeCount = InvokeSanitizeAndRepairActiveCount(pipeline);
        pipeline.SetCachedInternalCount(activeCount);
        if (activeCount == 0)
        {
            return trace;
        }

        float deltaTime = DeltaTime;
        int steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / pipeline.ContainerFluidMaxTimeStep), 1, 2);
        float subDt = deltaTime / steps;

        for (int step = 0; step < steps; step++)
        {
            pipeline.ComputeFrameSortSize(activeCount);
            pipeline.ClassifyParticleFieldFrom(pingPong.ReadSet.Block0, activeCount);
            InvokeSpatialHashBuild(pipeline, pingPong.ReadSet, activeCount);

            SamplePbfTraceStage(trace, "prePredict", pingPong.ReadSet, traceIndex, worldToLocal, readVelFromSoa: true);

            float smoothingRadius = pipeline.SphSmoothingRadius;
            pipeline.ApplyPbfUniforms(smoothingRadius, subDt);

            DispatchPbfPredict(pipeline, activeCount);
            SyncGpu();
            SamplePbfTraceStage(trace, "postPredict", pipeline.PbfScratch.PredictedBlock0, traceIndex, worldToLocal, readVelFromSoa: false, velSoa: pingPong.ReadSet);

            InvokeSpatialHashBuild(pipeline, pipeline.PbfScratch.PredictedBlock0, activeCount);

            int pbfIterations = pipeline.PbfIterations;
            for (int iter = 0; iter < pbfIterations; iter++)
            {
                pipeline.ClassifyParticleFieldFrom(pipeline.PbfScratch.PredictedBlock0, activeCount);
                DispatchPbfDensity(pipeline, activeCount);
                SyncGpu();
                DispatchPbfLambda(pipeline, activeCount);
                SyncGpu();
                DispatchPbfSolve(pipeline, activeCount);
                SyncGpu();
                SamplePbfTraceStage(trace, $"postSolveIter{iter}", pipeline.PbfScratch.PredictedBlock0, traceIndex, worldToLocal, readVelFromSoa: false, velSoa: pingPong.ReadSet);
            }

            DispatchPbfApply(pipeline, pingPong, activeCount);
            SyncGpu();
            SamplePbfTraceStage(trace, "postApply", pingPong.WriteSet, traceIndex, worldToLocal, readVelFromSoa: true);

            // Apply appended survivors and set the counter itself (mirrors production); do not overwrite.
            pingPong.Swap();
            activeCount = InvokeRepairParticleCount(pipeline, pingPong.ReadSet);
        }

        pipeline.SetCachedInternalCount(activeCount);
        return trace;
    }

    private static void SamplePbfTraceStage(
        List<PbfStageSample> trace,
        string stage,
        ParticleSoaBuffers soa,
        int index,
        Matrix4x4 worldToLocal,
        bool readVelFromSoa)
    {
        SamplePbfTraceStage(trace, stage, soa.Block0, index, worldToLocal, readVelFromSoa, readVelFromSoa ? soa : null);
    }

    private static void SamplePbfTraceStage(
        List<PbfStageSample> trace,
        string stage,
        ComputeBuffer positionBuffer,
        int index,
        Matrix4x4 worldToLocal,
        bool readVelFromSoa,
        ParticleSoaBuffers velSoa = null)
    {
        var block0 = new Vector4[1];
        positionBuffer.GetData(block0, 0, index, 1);
        Vector3 worldPos = new Vector3(block0[0].x, block0[0].y, block0[0].z);
        Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);
        Vector3 vel = Vector3.zero;
        if (readVelFromSoa && velSoa != null)
        {
            vel = ReadParticleVelocity(velSoa, index);
        }

        trace.Add(new PbfStageSample(stage, worldPos, localPos, vel));
    }

    private static void DispatchPbfPredict(HarmonicPipelineController host, uint activeCount)
    {
        host.PassBindReadSoa(host.PbfSolverShader, host.KernelPbfPredict, host.PingPong.ReadSet);
        host.BindParticleFieldRead(host.PbfSolverShader, host.KernelPbfPredict);
        host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.OldBlock0, host.PbfScratch.OldBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
        host.PbfSolverShader.SetBuffer(host.KernelPbfPredict, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
        host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
        host.PbfSolverShader.DispatchIndirect(host.KernelPbfPredict, host.IndirectArgsBuffer, 0);
    }

    private static void DispatchPbfDensity(HarmonicPipelineController host, uint activeCount)
    {
        host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
        host.PbfSolverShader.SetBuffer(host.KernelPbfDensity, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
        host.BindParticleFieldRead(host.PbfSolverShader, host.KernelPbfDensity);
        host.PassBindDensityCacheRw(host.PbfSolverShader, host.KernelPbfDensity);
        host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
        host.PbfSolverShader.DispatchIndirect(host.KernelPbfDensity, host.IndirectArgsBuffer, 0);
    }

    private static void DispatchPbfLambda(HarmonicPipelineController host, uint activeCount)
    {
        host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
        host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
        host.BindParticleFieldRead(host.PbfSolverShader, host.KernelPbfLambda);
        host.PassBindDensityCacheRead(host.PbfSolverShader, host.KernelPbfLambda);
        host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.Lambdas, host.PbfScratch.Lambdas);
        host.PbfSolverShader.SetBuffer(host.KernelPbfLambda, HarmonicShaderPropertyIds.GradSqSum, host.PbfScratch.GradSqSum);
        host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
        host.PbfSolverShader.DispatchIndirect(host.KernelPbfLambda, host.IndirectArgsBuffer, 0);
    }

    private static void DispatchPbfSolve(HarmonicPipelineController host, uint activeCount)
    {
        host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.SortedGridKeyValueBuffer, host.GridKeyValueBuffer);
        host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
        host.PbfSolverShader.SetBuffer(host.KernelPbfSolve, HarmonicShaderPropertyIds.Lambdas, host.PbfScratch.Lambdas);
        host.BindParticleFieldRead(host.PbfSolverShader, host.KernelPbfSolve);
        host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
        host.PbfSolverShader.DispatchIndirect(host.KernelPbfSolve, host.IndirectArgsBuffer, 0);
    }

    // ApplyPositionsKernel now compacts survivors via append (SphAppendInternal) instead of indexed
    // writes, so it must be driven with the append binding + a zeroed WriteSet counter, exactly like the
    // production OpenTopCylinderPbfSimulationPass. NOTE (exploratory harness only): because append
    // compaction assigns output slots in nondeterministic completion order, WriteSet index i after this
    // dispatch is no longer guaranteed to be the same particle as ReadSet index i. The per-index
    // "postApply" traces below therefore sample whatever survivor landed in that slot, not a stable
    // particle identity — acceptable here since this class is out-of-gate diagnostics.
    private static void DispatchPbfApply(HarmonicPipelineController host, PingPongSoaManager pingPong, uint activeCount)
    {
        pingPong.WriteSet.SetCounterValue(0);
        host.PassBindReadSoa(host.PbfSolverShader, host.KernelPbfApply, pingPong.ReadSet);
        host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.OldBlock0, host.PbfScratch.OldBlock0);
        host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.PredictedBlock0, host.PbfScratch.PredictedBlock0);
        host.PassBindDensityCacheDensitiesOnly(host.PbfSolverShader, host.KernelPbfApply);
        host.PassBindInternalAppendSoa(host.PbfSolverShader, host.KernelPbfApply, pingPong.WriteSet);
        host.PbfSolverShader.SetBuffer(host.KernelPbfApply, HarmonicShaderPropertyIds.CanvasHitAppend, host.CanvasHitsBuffer);
        host.PbfSolverShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
        host.PbfSolverShader.DispatchIndirect(host.KernelPbfApply, host.IndirectArgsBuffer, 0);
    }

    private static void InvokeBeginPipelineFrame(HarmonicPipelineController pipeline)
    {
        MethodInfo method = typeof(HarmonicPipelineController).GetMethod(
            "BeginPipelineFrame",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        method.Invoke(pipeline, null);
    }

    private static uint InvokeSanitizeAndRepairActiveCount(HarmonicPipelineController pipeline)
    {
        MethodInfo method = typeof(HarmonicPipelineController).GetMethod(
            "SanitizeAndRepairActiveCount",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return (uint)method.Invoke(pipeline, null);
    }

    private static uint InvokeRepairParticleCount(HarmonicPipelineController pipeline, ParticleSoaBuffers soa)
    {
        MethodInfo method = typeof(HarmonicPipelineController).GetMethod(
            "RepairParticleCount",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return (uint)method.Invoke(pipeline, new object[] { soa });
    }

    private static void InvokeSpatialHashBuild(HarmonicPipelineController pipeline, ParticleSoaBuffers read, uint activeCount)
    {
        pipeline.SpatialHashPass.Build(pipeline, read, activeCount);
    }

    private static void InvokeSpatialHashBuild(HarmonicPipelineController pipeline, ComputeBuffer positionBlock0, uint activeCount)
    {
        pipeline.SpatialHashPass.Build(pipeline, positionBlock0, activeCount);
    }

    private static void ReportPopulationCorrelation(PopulationFrameSnapshot snapshot, int frame)
    {
        if (snapshot.Indices == null || snapshot.Indices.Count == 0)
        {
            Debug.Log("[RigidCarrySpin:PopCorr] No in-radius particles at explosion frame.");
            return;
        }

        var carryByIndex = new Dictionary<int, float>();
        for (int i = 0; i < snapshot.Indices.Count; i++)
        {
            carryByIndex[snapshot.Indices[i]] = snapshot.CarryVelMagnitudes[i];
        }

        var topCarry = carryByIndex
            .OrderByDescending(kv => kv.Value)
            .Take(5)
            .ToList();

        int worstYIndex = -1;
        float worstY = float.MaxValue;
        foreach (int idx in snapshot.Indices)
        {
            if (!snapshot.PostPbfLocalY.TryGetValue(idx, out float localY))
            {
                continue;
            }

            if (localY < worstY)
            {
                worstY = localY;
                worstYIndex = idx;
            }
        }

        var topPostPbfY = snapshot.PostPbfLocalY
            .Where(kv => snapshot.Indices.Contains(kv.Key))
            .OrderBy(kv => kv.Value)
            .Take(5)
            .ToList();

        float pearson = ComputePearson(snapshot.Indices, snapshot.CarryVelMagnitudes, snapshot.PostPbfLocalY);

        int cohortBelowMinus10 = snapshot.PostPbfLocalY.Count(kv => kv.Value < -10f);

        var sb = new StringBuilder();
        sb.AppendLine($"[RigidCarrySpin:PopCorr] frame={frame} inRadiusCount={snapshot.Indices.Count}");
        sb.AppendLine($"  highestPostCarryVel: index={snapshot.HighestCarryVelIndex} |v|={snapshot.HighestCarryVelMagnitude:F4}");
        sb.AppendLine("  top5 post-carry |v|:");
        foreach (KeyValuePair<int, float> kv in topCarry)
        {
            float postPbfY = snapshot.PostPbfLocalY.TryGetValue(kv.Key, out float y) ? y : float.NaN;
            sb.AppendLine($"    idx={kv.Key} carry|v|={kv.Value:F4} postPbfLocalY={postPbfY:F4}");
        }

        sb.AppendLine($"  worst post-PBF localY: index={worstYIndex} y={worstY:F4}");
        sb.AppendLine("  top5 worst post-PBF localY:");
        foreach (KeyValuePair<int, float> kv in topPostPbfY)
        {
            float carryV = carryByIndex.TryGetValue(kv.Key, out float v) ? v : float.NaN;
            sb.AppendLine($"    idx={kv.Key} postPbfLocalY={kv.Value:F4} postCarry|v|={carryV:F4}");
        }

        bool highestVelIsWorstY = worstYIndex == snapshot.HighestCarryVelIndex;
        bool overlapTop5 = topCarry.Any(kv => topPostPbfY.Any(w => w.Key == kv.Key));
        sb.AppendLine($"  highestCarryVelIndex==worstPostPbfYIndex: {highestVelIsWorstY}");
        sb.AppendLine($"  top5-carry intersect top5-worstY: {overlapTop5}");
        sb.AppendLine($"  Pearson r(postCarry|v|, postPbfLocalY) over post-carry cohort: {pearson:F4}");
        sb.AppendLine($"  post-carry cohort with postPbfLocalY<-10: {cohortBelowMinus10}/{snapshot.Indices.Count}");
        Debug.Log(sb.ToString());
    }

    private static float ComputePearson(
        List<int> indices,
        List<float> carryVelMagnitudes,
        Dictionary<int, float> postPbfLocalY)
    {
        var xs = new List<float>();
        var ys = new List<float>();
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (!postPbfLocalY.TryGetValue(idx, out float localY))
            {
                continue;
            }

            xs.Add(carryVelMagnitudes[i]);
            ys.Add(localY);
        }

        if (xs.Count < 2)
        {
            return float.NaN;
        }

        float meanX = xs.Average();
        float meanY = ys.Average();
        float num = 0f;
        float denX = 0f;
        float denY = 0f;
        for (int i = 0; i < xs.Count; i++)
        {
            float dx = xs[i] - meanX;
            float dy = ys[i] - meanY;
            num += dx * dy;
            denX += dx * dx;
            denY += dy * dy;
        }

        float den = Mathf.Sqrt(denX * denY);
        return den > 1e-8f ? num / den : float.NaN;
    }

    private static void ReportPbfAmplification(List<PbfStageSample> trace, int traceIndex, Matrix4x4 worldToLocal)
    {
        if (trace == null || trace.Count == 0)
        {
            Debug.Log("[RigidCarrySpin:PbfAmp] No PBF sub-stage samples.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[RigidCarrySpin:PbfAmp] particle={traceIndex} sub-stage trace ({trace.Count} samples):");
        float largestJump = 0f;
        string largestJumpStage = "none";
        for (int i = 0; i < trace.Count; i++)
        {
            PbfStageSample s = trace[i];
            float deltaLocalY = i > 0 ? s.LocalPosition.y - trace[i - 1].LocalPosition.y : 0f;
            if (i > 0 && Mathf.Abs(deltaLocalY) > Mathf.Abs(largestJump))
            {
                largestJump = deltaLocalY;
                largestJumpStage = $"{trace[i - 1].Stage} -> {s.Stage}";
            }

            sb.AppendLine(
                $"  {s.Stage}: world=({s.WorldPosition.x:F3},{s.WorldPosition.y:F3},{s.WorldPosition.z:F3}) " +
                $"localY={s.LocalPosition.y:F4} |v|={s.Velocity.magnitude:F4}" +
                (i > 0 ? $" dLocalY={deltaLocalY:F4}" : string.Empty));
        }

        sb.AppendLine($"  largest |dLocalY| step: {largestJumpStage} delta={largestJump:F4}");
        Debug.Log(sb.ToString());
    }

    private struct PopulationFrameSnapshot
    {
        public Matrix4x4 WorldToLocal;
        public List<int> Indices;
        public List<float> CarryVelMagnitudes;
        public int HighestCarryVelIndex;
        public float HighestCarryVelMagnitude;
        public Dictionary<int, float> PostPbfLocalY;
    }

    private readonly struct PbfStageSample
    {
        public readonly string Stage;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 Velocity;

        public PbfStageSample(string stage, Vector3 worldPosition, Vector3 localPosition, Vector3 velocity)
        {
            Stage = stage;
            WorldPosition = worldPosition;
            LocalPosition = localPosition;
            Velocity = velocity;
        }
    }

    private static void RunSpinSimulation(bool logPerFrameDiagnostics, out float minLocalYInRadius)
    {
        HarmonicPipelineController pipeline = CreatePipelineWithRigidCarry();
        Vector3 floorPivot = Vector3.zero;
        float radius = 0.55f;
        float height = 1.1f;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(
            floorPivot,
            Quaternion.identity,
            radius,
            height,
            restitution: 0.1f,
            friction: 0.85f,
            wallStiffness: 400f);

        int spawned = pipeline.TrySpawnContainerLatticeFill();
        Assert.Greater(spawned, 0, "Expected lattice particles near the container floor.");

        Matrix4x4 prevLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
        minLocalYInRadius = float.MaxValue;

        int trackedIndex = SelectTrackedParticleIndex(pipeline, prevLocalToWorld, radius);
        Debug.Log($"[RigidCarrySpin] trackedParticleIndex={trackedIndex} spawned={spawned}");

        int firstBelowMinusOneFrame = -1;
        float prevCarryLocalY = 0f;
        float prevCarryVelMag = 0f;
        float prevCarryArmLen = 0f;
        var perFrameVelMag = new float[FrameCount];
        var perFrameArmLen = new float[FrameCount];

        PingPongSoaManager pingPong = GetPingPong(pipeline);

        for (int frame = 0; frame < FrameCount; frame++)
        {
            float t = frame * SpinDegreesPerFrame;
            Quaternion rotation = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currLocalToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rotation);
            Matrix4x4 worldToLocal = currLocalToWorld.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot,
                rotation,
                radius,
                height,
                restitution: 0.1f,
                friction: 0.85f,
                wallStiffness: 400f);

            Matrix4x4 carryClampWorldToLocal = GetContainerWorldToLocal(pipeline);

            Vector3 posBeforeCarry = ReadParticlePosition(pingPong.ReadSet, trackedIndex);
            Vector3 velBeforeCarry = ReadParticleVelocity(pingPong.ReadSet, trackedIndex);
            float armLenBeforeCarry = (posBeforeCarry - floorPivot).magnitude;
            Vector3 angularVelocity = ComputeAngularVelocityWorld(prevLocalToWorld, currLocalToWorld, DeltaTime);
            float predictedCarryVelMag = (velBeforeCarry + Vector3.Cross(angularVelocity, posBeforeCarry - floorPivot)).magnitude;

            pipeline.ApplyContainerRigidRotation(prevLocalToWorld, currLocalToWorld, DeltaTime);
            SyncGpu();

            Vector3 posAfterCarry = ReadParticlePosition(pingPong.ReadSet, trackedIndex);
            Vector3 velAfterCarry = ReadParticleVelocity(pingPong.ReadSet, trackedIndex);
            float carryLocalY = worldToLocal.MultiplyPoint3x4(posAfterCarry).y;
            float carryVelMag = velAfterCarry.magnitude;
            float carryArmLen = (posBeforeCarry - floorPivot).magnitude;

            perFrameVelMag[frame] = carryVelMag;
            perFrameArmLen[frame] = carryArmLen;

            if (logPerFrameDiagnostics)
            {
                Debug.Log(
                    $"[RigidCarrySpin:Trace] frame={frame} " +
                    $"carryLocalY={carryLocalY:F4} carryVelMag={carryVelMag:F4} armLen={carryArmLen:F4} " +
                    $"velBeforeCarry={velBeforeCarry.magnitude:F4} predictedCarryVelMag={predictedCarryVelMag:F4} " +
                    $"matrixMismatch={MatrixMismatchMagnitude(carryClampWorldToLocal, worldToLocal):F6}");
            }

            if (firstBelowMinusOneFrame < 0 && carryLocalY < -1f)
            {
                firstBelowMinusOneFrame = frame;
                Debug.Log(
                    $"[RigidCarrySpin:Trace] FIRST carryLocalY<-1 at frame={frame} " +
                    $"carryLocalY={carryLocalY:F4} carryVelMag={carryVelMag:F4} armLen={carryArmLen:F4}");
                Debug.Log(
                    $"[RigidCarrySpin:Trace] frameBefore={frame - 1} " +
                    $"carryLocalY={prevCarryLocalY:F4} carryVelMag={prevCarryVelMag:F4} armLen={prevCarryArmLen:F4}");
            }

            prevCarryLocalY = carryLocalY;
            prevCarryVelMag = carryVelMag;
            prevCarryArmLen = carryArmLen;

            pipeline.ExecutePipelineFrame(DeltaTime);
            SyncGpu();

            prevLocalToWorld = currLocalToWorld;

            if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount)
                || activeCount == 0)
            {
                continue;
            }

            bool readSetMatchesPingPong = ReferenceEquals(soa, pingPong.ReadSet);
            if (logPerFrameDiagnostics && frame < 3)
            {
                Debug.Log(
                    $"[RigidCarrySpin:Trace] postPbf frame={frame} " +
                    $"tryGetReadSetMatchesPingPongReadSet={readSetMatchesPingPong}");
            }

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
            for (int i = 0; i < particles.Length; i++)
            {
                Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
                float radial = new Vector2(local.x, local.z).magnitude;
                if (radial <= radius + 1e-4f)
                {
                    minLocalYInRadius = Mathf.Min(minLocalYInRadius, local.y);
                }
            }
        }

        ReportVelocityArmTrends(perFrameVelMag, perFrameArmLen);

        if (firstBelowMinusOneFrame < 0)
        {
            Debug.Log("[RigidCarrySpin:Trace] carryLocalY never dropped below -1.0 during trace window.");
        }

        Object.DestroyImmediate(pipeline.gameObject);
    }

    private static void ReportVelocityArmTrends(float[] perFrameVelMag, float[] perFrameArmLen)
    {
        if (perFrameVelMag.Length < 2)
        {
            return;
        }

        float velFrame0 = perFrameVelMag[0];
        float velFrameLast = perFrameVelMag[perFrameVelMag.Length - 1];
        float armFrame0 = perFrameArmLen[0];
        float armFrameLast = perFrameArmLen[perFrameArmLen.Length - 1];

        int monotonicVelIncreases = 0;
        float sumVelDelta = 0f;
        for (int i = 1; i < perFrameVelMag.Length; i++)
        {
            float d = perFrameVelMag[i] - perFrameVelMag[i - 1];
            sumVelDelta += d;
            if (d > 1e-4f)
            {
                monotonicVelIncreases++;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("[RigidCarrySpin:Trace] velocity/arm trend (post-carry, tracked particle):");
        sb.AppendLine($"  carryVelMag frame0={velFrame0:F4} frame119={velFrameLast:F4} netDelta={velFrameLast - velFrame0:F4}");
        sb.AppendLine($"  avgPerFrameVelDelta={sumVelDelta / (perFrameVelMag.Length - 1):F4} monotonicIncreases={monotonicVelIncreases}/{perFrameVelMag.Length - 1}");
        sb.AppendLine($"  armLen frame0={armFrame0:F4} frame119={armFrameLast:F4} netDelta={armFrameLast - armFrame0:F4}");
        Debug.Log(sb.ToString());
    }

    private static int SelectTrackedParticleIndex(
        HarmonicPipelineController pipeline,
        Matrix4x4 localToWorld,
        float radius)
    {
        if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount)
            || activeCount == 0)
        {
            return 0;
        }

        SyncGpu();
        Matrix4x4 worldToLocal = localToWorld.inverse;
        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);

        int bestIndex = 0;
        float bestScore = float.MaxValue;
        for (int i = 0; i < particles.Length; i++)
        {
            Vector3 local = worldToLocal.MultiplyPoint3x4(particles[i].Position);
            float radial = new Vector2(local.x, local.z).magnitude;
            if (radial > radius + 1e-4f)
            {
                continue;
            }

            float score = local.y;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static Vector3 ComputeAngularVelocityWorld(
        Matrix4x4 prevLocalToWorld,
        Matrix4x4 currLocalToWorld,
        float deltaTime)
    {
        Quaternion deltaRot = currLocalToWorld.rotation * Quaternion.Inverse(prevLocalToWorld.rotation);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f)
        {
            angleDeg -= 360f;
        }

        float safeDt = Mathf.Max(deltaTime, 1e-5f);
        return axis.sqrMagnitude > 1e-8f
            ? axis.normalized * (angleDeg * Mathf.Deg2Rad / safeDt)
            : Vector3.zero;
    }

    private static float MatrixMismatchMagnitude(Matrix4x4 a, Matrix4x4 b)
    {
        float sum = 0f;
        for (int i = 0; i < 16; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }

        return Mathf.Sqrt(sum);
    }

    private static Matrix4x4 GetContainerWorldToLocal(HarmonicPipelineController pipeline)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            "_containerWorldToLocal",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (Matrix4x4)field.GetValue(pipeline);
    }

    private static PingPongSoaManager GetPingPong(HarmonicPipelineController pipeline)
    {
        PropertyInfo prop = typeof(HarmonicPipelineController).GetProperty(
            "PingPong",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (PingPongSoaManager)prop.GetValue(pipeline);
    }

    private static Vector3 ReadParticlePosition(ParticleSoaBuffers soa, int index)
    {
        var block0 = new Vector4[1];
        soa.Block0.GetData(block0, 0, index, 1);
        return new Vector3(block0[0].x, block0[0].y, block0[0].z);
    }

    private static Vector3 ReadParticleVelocity(ParticleSoaBuffers soa, int index)
    {
        var block1 = new Vector4[1];
        soa.Block1.GetData(block1, 0, index, 1);
        return new Vector3(block1[0].x, block1[0].y, block1[0].z);
    }

    private static void SyncGpu()
    {
        // Force completion of pending compute before synchronous GetData readback.
        var fence = Graphics.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }

    private static HarmonicPipelineController CreatePipelineWithRigidCarry()
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

        var go = new GameObject("RigidCarrySpinTestPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        FieldInfo carryField = typeof(HarmonicPipelineController).GetField(
            "containerRigidCarryShader",
            BindingFlags.Instance | BindingFlags.NonPublic);
        carryField.SetValue(pipeline, carryShader);

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
}
