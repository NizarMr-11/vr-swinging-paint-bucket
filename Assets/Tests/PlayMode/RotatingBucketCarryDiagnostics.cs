using System;
using System.Reflection;
using System.Runtime.InteropServices;
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
/// Instrumentation-only diagnostics for two rotation-only symptoms reported in the visual pass:
///   (B) particles genuinely OUTSIDE the bucket move when the bucket rotates (as if rigid-carried), and
///   (A) a fixed "dead zone" under the bucket where particles get thrown / despawned.
///
/// These tests do NOT assert a fix. They trace GPU state to identify the mechanism:
///   - which transform (this-frame's new rotation vs last-frame's) the pre-carry classify uses,
///   - whether OtcFieldInsideRigidCarry is wrongly TRUE for an outside particle under rotation, or
///     TRUE-but-carry-moves-anyway,
///   - whether the "dead zone" is world-fixed or rotates with the bucket across swing angles.
///
/// Traces are written to Logs/HarmonicSimulation/ and echoed to the console.
/// </summary>
[Category("Diagnostic")]
[Category("Exploratory")]
public class RotatingBucketCarryDiagnostics
{
    const float DeltaTime = 1f / 60f;
    const float Radius = 0.55f;
    const float Height = 1.1f;

    // Mirror OtcParticleField.hlsl region bits.
    const uint OTC_REGION_PARTICIPATES_PBF = 1u << 0;
    const uint OTC_REGION_INSIDE_VOLUME = 1u << 1;
    const uint OTC_REGION_INSIDE_RIGID_CARRY = 1u << 2;
    const uint OTC_REGION_CANVAS_CLAIM = 1u << 3;
    const uint OTC_REGION_BEAM = 1u << 4;
    const uint OTC_REGION_THROUGH_HOLE = 1u << 5;

    // Matches struct OtcParticleClassification { float signedDist; uint regionFlags; float2 pad; }.
    [StructLayout(LayoutKind.Sequential)]
    struct ClassificationCpu
    {
        public float signedDist;
        public uint regionFlags;
        public float pad0;
        public float pad1;
    }

    // ------------------------------------------------------------------------------------------------
    // Q1: which transform does the pre-carry classify evaluate against?
    // ------------------------------------------------------------------------------------------------
    [Test]
    public void PreCarryClassify_WhichTransform_Trace()
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

            // Probe particle: fixed world position level with the cup band, radially clear of the
            // upright bucket but reachable if the bucket tilts toward +X.
            Vector3 probeWorld = new Vector3(Radius + 0.30f, 0.30f, 0f);
            SpawnParticles(pipeline, new[] { MakeParticle(probeWorld, Vector3.zero) });

            Quaternion upright = Quaternion.identity;
            Quaternion tilted = Quaternion.Euler(0f, 0f, 35f); // lean toward +X

            // Upright transform uploaded -> classify -> read bit.
            pipeline.SetContainerFluidOriented(Vector3.zero, upright, Radius, Height, 0.1f, 0.85f, 400f);
            ClassifyReadSet(pipeline, 1);
            ClassificationCpu uprightCls = ReadClassification(pipeline, 0);
            float rUpright = LocalRadius(probeWorld, Matrix4x4.Inverse(ContainerOrientedBounds.BuildLocalToWorld(Vector3.zero, upright)));

            // Tilted transform uploaded -> classify -> read bit (this is what the real frame does:
            // SetContainerFluidOriented(currentRotation) runs BEFORE the pre-carry classify).
            pipeline.SetContainerFluidOriented(Vector3.zero, tilted, Radius, Height, 0.1f, 0.85f, 400f);
            ClassifyReadSet(pipeline, 1);
            ClassificationCpu tiltedCls = ReadClassification(pipeline, 0);
            float rTilted = LocalRadius(probeWorld, Matrix4x4.Inverse(ContainerOrientedBounds.BuildLocalToWorld(Vector3.zero, tilted)));

            sb.AppendLine("[RotCarry:WhichTransform] probe world-fixed; only the container rotation changed between the two classifies.");
            sb.AppendLine($"  probeWorld=({probeWorld.x:F3},{probeWorld.y:F3},{probeWorld.z:F3}) R={Radius}");
            sb.AppendLine($"  upright: localR={rUpright:F4} insideRigidCarry={(uprightCls.regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0} flags=0x{uprightCls.regionFlags:X}");
            sb.AppendLine($"  tilted : localR={rTilted:F4} insideRigidCarry={(tiltedCls.regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0} flags=0x{tiltedCls.regionFlags:X}");
            sb.AppendLine("  => classify tracks whichever transform was last uploaded. In production, SetContainerFluidOriented(CURRENT rotation)");
            sb.AppendLine("     runs before ApplyContainerRigidRotation's pre-carry classify, so the classify uses THIS frame's NEW rotation");
            sb.AppendLine("     evaluated against frame-start (un-moved) positions.");

            Report("rotating_bucket_which_transform.log", sb.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Q2 (symptom B): outside particle + rotating bucket. Isolate carry (no PBF) so any position
    // change is attributable to rigid carry alone. Per frame log the carry bit, the carry-only delta,
    // and the CPU relative-speed proxy that gates earning.
    // ------------------------------------------------------------------------------------------------
    [Test]
    public void OutsideParticle_RotatingBucket_CarryBitAndDelta_Trace()
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

            // Genuinely outside: radially clear of the upright bucket by 0.30, level with the cup band.
            Vector3 startWorld = new Vector3(Radius + 0.30f, 0.30f, 0f);
            SpawnParticles(pipeline, new[] { MakeParticle(startWorld, Vector3.zero) });

            sb.AppendLine("[RotCarry:OutsideTrace] carry isolated (no PBF). Bucket tilts about Z toward +X, pivot at origin.");
            sb.AppendLine("cols: frame angleDeg | worldPos | localR (curr) | carryBit | relSpeedProxy earned? | carryDx carryDy carryDz | |carryDelta|");
            sb.AppendLine(new string('-', 130));

            Quaternion prevRot = Quaternion.identity;
            pipeline.SetContainerFluidOriented(Vector3.zero, prevRot, Radius, Height, 0.1f, 0.85f, 400f);
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(Vector3.zero, prevRot);

            int falseCarryFrames = 0;
            int bitTrueWhileOutsideFrames = 0;

            for (int frame = 0; frame < 30; frame++)
            {
                float angle = (frame + 1) * 2f; // 2..60 deg
                Quaternion currRot = Quaternion.Euler(0f, 0f, angle);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(Vector3.zero, currRot);
                Matrix4x4 currW2L = currL2W.inverse;

                // Production order: update container transform (curr) BEFORE the pre-carry classify.
                pipeline.SetContainerFluidOriented(Vector3.zero, currRot, Radius, Height, 0.1f, 0.85f, 400f);

                Vector3 before = ReadState(pipeline, 0).Pos;

                // Reproduce exactly what carry reads: classify frame-start positions with curr transform.
                ClassifyReadSet(pipeline, 1);
                ClassificationCpu cls = ReadClassification(pipeline, 0);
                bool carryBit = (cls.regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0;

                Vector3 localCurr = currW2L.MultiplyPoint3x4(before);
                float localR = new Vector2(localCurr.x, localCurr.z).magnitude;
                bool radiallyOutside = localR > Radius + 1e-3f;

                // CPU relative-speed proxy (particle vel is ~0): |linear + cross(omega, arm)|.
                RigidVelocityProxy(prevL2W, currL2W, DeltaTime, before,
                    out Vector3 expectedVel, out float relSpeed);
                bool earnedProxy = carryBit && relSpeed < 0.25f;

                // Carry only (no ExecutePipelineFrame): any delta is rigid carry.
                pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
                SyncGpu();
                Vector3 after = ReadState(pipeline, 0).Pos;
                Vector3 carryDelta = after - before;

                if (radiallyOutside && carryBit)
                {
                    bitTrueWhileOutsideFrames++;
                }
                if (radiallyOutside && carryDelta.magnitude > 1e-4f)
                {
                    falseCarryFrames++;
                }

                sb.AppendLine(
                    $"f{frame,3} a={angle,5:F1} | ({after.x,6:F3},{after.y,6:F3},{after.z,6:F3}) | " +
                    $"lR={localR,6:F3} | bit={(carryBit ? 1 : 0)} | rel={relSpeed,7:F3} earn={(earnedProxy ? 1 : 0)} | " +
                    $"dx={carryDelta.x,7:F4} dy={carryDelta.y,7:F4} dz={carryDelta.z,7:F4} | |d|={carryDelta.magnitude,7:F4}" +
                    (radiallyOutside ? " [OUTSIDE]" : " [inside]"));

                prevRot = currRot;
                prevL2W = currL2W;
            }

            sb.AppendLine(new string('-', 130));
            sb.AppendLine($"[RotCarry:OutsideTrace] frames where particle radially OUTSIDE but carryBit TRUE = {bitTrueWhileOutsideFrames}");
            sb.AppendLine($"[RotCarry:OutsideTrace] frames where particle radially OUTSIDE but carry MOVED it   = {falseCarryFrames}");
            sb.AppendLine("Interpretation: carryBit TRUE while radially outside => classify (curr transform vs old position) over-captures;");
            sb.AppendLine("                carry MOVED while outside => rigid carry displaces a particle that should be on ordinary physics.");

            Report("rotating_bucket_outside_carry_trace.log", sb.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Q3 (symptom A): dead zone at several STATIC swing angles. Spawn a resting row of particles under
    // the bucket, run carry+PBF for a few frames at each angle, log which particles are thrown/removed
    // and their world X. If the anomaly's world X is constant across angles => world-fixed (transform
    // bug); if it moves with the tilt => rotates with the bucket (geometric).
    // ------------------------------------------------------------------------------------------------
    [Test]
    public void DeadZone_MultipleSwingAngles_WorldFixedOrRotates_Trace()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        float[] anglesDeg = { -30f, -15f, 0f, 15f, 30f };
        var sb = new StringBuilder();
        sb.AppendLine("[RotCarry:DeadZone] static tilt about Z. Row of particles resting near floor band, spread in X.");
        sb.AppendLine("cols: angle | idx startX | finalWorld | |v| | removed? | note");
        sb.AppendLine(new string('-', 120));

        foreach (float angleDeg in anglesDeg)
        {
            HarmonicPipelineController pipeline = CreatePipeline();
            try
            {
                pipeline.SetUsePbf(true);
                pipeline.SetContainerFluidEnabled(true);
                pipeline.SetSimulationActive(true);
                pipeline.SetCanvasPlaneY(-3f);
                pipeline.SetCanvasCullingEnabled(true);

                Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
                pipeline.SetContainerFluidOriented(Vector3.zero, rot, Radius, Height, 0.1f, 0.85f, 400f);
                Matrix4x4 l2w = ContainerOrientedBounds.BuildLocalToWorld(Vector3.zero, rot);
                Matrix4x4 w2l = l2w.inverse;

                float[] startX = { -0.4f, -0.2f, 0.0f, 0.2f, 0.4f };
                var particles = new FluidParticle[startX.Length];
                for (int k = 0; k < startX.Length; k++)
                {
                    particles[k] = MakeParticle(new Vector3(startX[k], 0.15f, 0f), Vector3.zero);
                }
                SpawnParticles(pipeline, particles);

                // Static angle => prev==curr => carry no-ops; isolates PBF + floor-clamp under a tilt.
                for (int frame = 0; frame < 12; frame++)
                {
                    pipeline.ApplyContainerRigidRotation(l2w, l2w, DeltaTime);
                    SyncGpu();
                    pipeline.ExecutePipelineFrame(DeltaTime);
                    SyncGpu();
                }

                ParticleState[] final = ReadStates(pipeline, startX.Length);
                for (int k = 0; k < startX.Length; k++)
                {
                    bool removed = final[k].Pos.sqrMagnitude < 1e-8f && final[k].Vel.sqrMagnitude < 1e-8f;
                    Vector3 localFinal = w2l.MultiplyPoint3x4(final[k].Pos);
                    string note = final[k].Vel.magnitude > 25f ? "THROWN" : (removed ? "removed" : "-");
                    sb.AppendLine(
                        $"a={angleDeg,6:F1} | idx{k} sx={startX[k],5:F2} | " +
                        $"({final[k].Pos.x,6:F3},{final[k].Pos.y,6:F3},{final[k].Pos.z,6:F3}) | " +
                        $"|v|={final[k].Vel.magnitude,8:F2} | rm={(removed ? 1 : 0)} | lY={localFinal.y,6:F3} {note}");
                }
                sb.AppendLine(new string('-', 120));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
            }
        }

        Report("rotating_bucket_dead_zone_trace.log", sb.ToString());
    }

    // ------------------------------------------------------------------------------------------------
    // Q2b/Q3b (realistic): pendulum swing (moving pivot + tilt) so the cup volume actually sweeps
    // through space. Attribute per-particle motion to carry vs PBF, flag outside-but-moved and
    // thrown/removed particles, and record world positions of anomalies for the world-fixed question.
    // ------------------------------------------------------------------------------------------------
    [Test]
    public void PendulumSweep_OutsideAndDeadZone_Trace()
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
            pipeline.SetCanvasPlaneY(-3f);
            pipeline.SetCanvasCullingEnabled(true);

            // Pendulum: pivot above, rope L, bucket hangs at pivot + (L sinθ, -L cosθ, 0), tilt Rz(θ).
            Vector3 pivot = new Vector3(0f, 2.5f, 0f);
            float L = 2.5f;
            float g = 9.81f;
            float damping = 0.05f;
            float theta = 35f * Mathf.Deg2Rad;
            float omega = 0f;

            Vector3 BucketPos(float th) => pivot + new Vector3(L * Mathf.Sin(th), -L * Mathf.Cos(th), 0f);
            Quaternion BucketRot(float th) => Quaternion.Euler(0f, 0f, th * Mathf.Rad2Deg);

            // Particles (world space):
            //  0: genuine fluid inside the cup at the start pose
            //  1: resting particle at the bottom of the arc the cup will sweep over  (outside at start)
            //  2: resting particle far away, should never be captured
            Vector3 startBucketPos = BucketPos(theta);
            Quaternion startRot = BucketRot(theta);
            Vector3 insideFluidWorld = startBucketPos + startRot * new Vector3(0.15f, 0.3f, 0f);
            Vector3 sweepTargetWorld = new Vector3(0.0f, 0.12f, 0f);
            Vector3 farWorld = new Vector3(3.0f, 0.12f, 0f);

            SpawnParticles(pipeline, new[]
            {
                MakeParticle(insideFluidWorld, Vector3.zero),
                MakeParticle(sweepTargetWorld, Vector3.zero),
                MakeParticle(farWorld, Vector3.zero),
            });
            int count = 3;

            sb.AppendLine("[RotCarry:Pendulum] pivot=(0,2.5,0) L=2.5 startTheta=35deg. Full frame = carry then PBF.");
            sb.AppendLine($"  p0 insideFluid world=({insideFluidWorld.x:F3},{insideFluidWorld.y:F3},{insideFluidWorld.z:F3})");
            sb.AppendLine($"  p1 sweepTarget world=({sweepTargetWorld.x:F3},{sweepTargetWorld.y:F3},{sweepTargetWorld.z:F3}) [outside at start]");
            sb.AppendLine($"  p2 far world=({farWorld.x:F3},{farWorld.y:F3},{farWorld.z:F3})");
            sb.AppendLine("cols: f theta | idx | localR localY inVol? carryBit prevEarn | relProxy | carryD pbfD |v| | note");
            sb.AppendLine(new string('-', 140));

            // Establish prev pose so the first ApplyContainerRigidRotation has a valid delta.
            pipeline.SetContainerFluidOriented(BucketPos(theta), BucketRot(theta), Radius, Height, 0.1f, 0.85f, 400f);
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(BucketPos(theta), BucketRot(theta));

            int outsideButCarried = 0;
            int outsideButThrownByPbf = 0;
            var anomalyWorld = new StringBuilder();

            for (int frame = 0; frame < 70; frame++)
            {
                // Advance pendulum (semi-implicit Euler; representative of the runtime rig).
                float alpha = -(g / L) * Mathf.Sin(theta) - damping * omega;
                omega += alpha * DeltaTime;
                theta += omega * DeltaTime;

                Vector3 bucketPos = BucketPos(theta);
                Quaternion rot = BucketRot(theta);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(bucketPos, rot);
                Matrix4x4 currW2L = currL2W.inverse;

                // Production order: push transform (curr) first.
                pipeline.SetContainerFluidOriented(bucketPos, rot, Radius, Height, 0.1f, 0.85f, 400f);

                ParticleState[] before = ReadStates(pipeline, count);

                // Pre-carry classify (same as carry pass): read bit per particle.
                ClassifyReadSet(pipeline, (uint)count);
                var cls = ReadClassifications(pipeline, count);

                pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
                SyncGpu();
                ParticleState[] afterCarry = ReadStates(pipeline, count);

                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
                ParticleState[] afterPbf = ReadStates(pipeline, count);

                for (int k = 0; k < count; k++)
                {
                    Vector3 localBefore = currW2L.MultiplyPoint3x4(before[k].Pos);
                    float localR = new Vector2(localBefore.x, localBefore.z).magnitude;
                    bool radiallyOutside = localR > Radius + 1e-3f;
                    bool inVol = (cls[k].regionFlags & OTC_REGION_INSIDE_VOLUME) != 0;
                    bool carryBit = (cls[k].regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0;
                    uint prevEarn = ReadPrevInside(pipeline, k);

                    RigidVelocityProxy(prevL2W, currL2W, DeltaTime, before[k].Pos, out _, out float relSpeed);

                    float carryD = (afterCarry[k].Pos - before[k].Pos).magnitude;
                    float pbfD = (afterPbf[k].Pos - afterCarry[k].Pos).magnitude;
                    float speed = afterPbf[k].Vel.magnitude;
                    bool removed = afterPbf[k].Pos.sqrMagnitude < 1e-8f && afterPbf[k].Vel.sqrMagnitude < 1e-8f;

                    string note = "-";
                    if (radiallyOutside && carryD > 1e-3f) { note = "OUTSIDE_CARRIED"; outsideButCarried++; anomalyWorld.AppendLine($"  f{frame} p{k} carry world=({afterCarry[k].Pos.x:F3},{afterCarry[k].Pos.y:F3},{afterCarry[k].Pos.z:F3})"); }
                    else if (radiallyOutside && speed > 25f) { note = "OUTSIDE_THROWN"; outsideButThrownByPbf++; anomalyWorld.AppendLine($"  f{frame} p{k} thrown world=({afterPbf[k].Pos.x:F3},{afterPbf[k].Pos.y:F3},{afterPbf[k].Pos.z:F3}) |v|={speed:F1}"); }
                    else if (removed) note = "removed";

                    // Log fluid (p0) always, and p1/p2 when interesting.
                    if (k == 0 || note != "-" || carryBit || !radiallyOutside)
                    {
                        sb.AppendLine(
                            $"f{frame,3} th={theta * Mathf.Rad2Deg,6:F1} | p{k} | " +
                            $"lR={localR,6:F3} lY={localBefore.y,6:F3} vol={(inVol ? 1 : 0)} bit={(carryBit ? 1 : 0)} pe={prevEarn} | " +
                            $"rel={relSpeed,6:F2} | cD={carryD,7:F4} pD={pbfD,7:F4} |v|={speed,7:F2} | {note}");
                    }
                }

                prevL2W = currL2W;
            }

            sb.AppendLine(new string('-', 140));
            sb.AppendLine($"[RotCarry:Pendulum] outsideButCarried(frames*particles)={outsideButCarried} outsideButThrownByPbf={outsideButThrownByPbf}");
            sb.AppendLine("Anomaly world positions (for world-fixed vs rotates check):");
            sb.Append(anomalyWorld.Length == 0 ? "  (none)\n" : anomalyWorld.ToString());

            Report("rotating_bucket_pendulum_trace.log", sb.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Q2c/Q3c: harsher/faster swing over a rake of resting particles. Summarize per particle: max
    // carry-only displacement (does carry EVER move it?), max PBF speed (throw), removed? (despawn),
    // and the world X where the peak throw occurred (world-fixed check).
    // ------------------------------------------------------------------------------------------------
    [Test]
    public void PendulumRake_ThrowAndDespawn_Trace()
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
            pipeline.SetCanvasPlaneY(-2f);
            pipeline.SetCanvasCullingEnabled(true);

            // Faster swing: shorter rope, larger amplitude.
            Vector3 pivot = new Vector3(0f, 1.6f, 0f);
            float L = 1.6f;
            float g = 9.81f;
            float damping = 0.02f;
            float theta = 65f * Mathf.Deg2Rad;
            float omega = 0f;

            Vector3 BucketPos(float th) => pivot + new Vector3(L * Mathf.Sin(th), -L * Mathf.Cos(th), 0f);
            Quaternion BucketRot(float th) => Quaternion.Euler(0f, 0f, th * Mathf.Rad2Deg);

            // Rake of resting particles across the arc bottom at the floor band.
            float[] rakeX = { -0.8f, -0.5f, -0.25f, 0f, 0.25f, 0.5f, 0.8f };
            int count = rakeX.Length;
            var spawn = new FluidParticle[count];
            for (int k = 0; k < count; k++)
            {
                spawn[k] = MakeParticle(new Vector3(rakeX[k], 0.05f, 0f), Vector3.zero);
            }
            SpawnParticles(pipeline, spawn);

            var maxCarryD = new float[count];
            var maxSpeed = new float[count];
            var carryEver = new bool[count];
            var everCarriedBitWhileOutside = new bool[count];
            var removed = new bool[count];
            var peakThrowWorldX = new float[count];

            pipeline.SetContainerFluidOriented(BucketPos(theta), BucketRot(theta), Radius, Height, 0.1f, 0.85f, 400f);
            Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(BucketPos(theta), BucketRot(theta));

            for (int frame = 0; frame < 160; frame++)
            {
                float alpha = -(g / L) * Mathf.Sin(theta) - damping * omega;
                omega += alpha * DeltaTime;
                theta += omega * DeltaTime;

                Vector3 bucketPos = BucketPos(theta);
                Quaternion rot = BucketRot(theta);
                Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(bucketPos, rot);
                Matrix4x4 currW2L = currL2W.inverse;

                pipeline.SetContainerFluidOriented(bucketPos, rot, Radius, Height, 0.1f, 0.85f, 400f);

                ParticleState[] before = ReadStates(pipeline, count);
                ClassifyReadSet(pipeline, (uint)count);
                var cls = ReadClassifications(pipeline, count);

                pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
                SyncGpu();
                ParticleState[] afterCarry = ReadStates(pipeline, count);

                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
                ParticleState[] afterPbf = ReadStates(pipeline, count);

                for (int k = 0; k < count; k++)
                {
                    Vector3 localBefore = currW2L.MultiplyPoint3x4(before[k].Pos);
                    float localR = new Vector2(localBefore.x, localBefore.z).magnitude;
                    bool radiallyOutside = localR > Radius + 1e-3f;
                    bool carryBit = (cls[k].regionFlags & OTC_REGION_INSIDE_RIGID_CARRY) != 0;

                    float carryD = (afterCarry[k].Pos - before[k].Pos).magnitude;
                    if (carryD > maxCarryD[k]) maxCarryD[k] = carryD;
                    if (carryD > 1e-3f) carryEver[k] = true;
                    if (radiallyOutside && carryBit) everCarriedBitWhileOutside[k] = true;

                    float speed = afterPbf[k].Vel.magnitude;
                    if (speed > maxSpeed[k]) { maxSpeed[k] = speed; peakThrowWorldX[k] = afterPbf[k].Pos.x; }

                    if (afterPbf[k].Pos.sqrMagnitude < 1e-8f && afterPbf[k].Vel.sqrMagnitude < 1e-8f) removed[k] = true;
                }

                prevL2W = currL2W;
            }

            sb.AppendLine("[RotCarry:Rake] fast swing (L=1.6, start 65deg) over resting rake at y=0.05.");
            sb.AppendLine("cols: idx startX | maxCarryDisp carryEver bitTrueWhileOutside | maxPbfSpeed peakThrowWorldX | removed");
            sb.AppendLine(new string('-', 110));
            for (int k = 0; k < count; k++)
            {
                sb.AppendLine(
                    $" idx{k} sx={rakeX[k],5:F2} | maxCarryD={maxCarryD[k],7:F4} carryEver={(carryEver[k] ? 1 : 0)} bitOutside={(everCarriedBitWhileOutside[k] ? 1 : 0)} | " +
                    $"maxV={maxSpeed[k],8:F2} throwX={peakThrowWorldX[k],6:F3} | removed={(removed[k] ? 1 : 0)}");
            }
            sb.AppendLine(new string('-', 110));
            sb.AppendLine("Read: carryEver=0 everywhere => rigid carry never displaces the rake (velocity gate holds).");
            sb.AppendLine("      maxV large with carryEver=0 => the throw is PBF (swept-volume floor/participation), not carry.");
            sb.AppendLine("      peakThrowWorldX clustering near a fixed world X => world-fixed zone (arc bottom), not a rotating one.");

            Report("rotating_bucket_rake_trace.log", sb.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------------------------------------
    static uint ReadPrevInside(HarmonicPipelineController pipeline, int idx)
    {
        ComputeBuffer buf = pipeline.PrevInsideForCarryBuffer;
        if (buf == null || idx >= buf.count)
        {
            return 0u;
        }
        var arr = new uint[idx + 1];
        buf.GetData(arr, 0, 0, idx + 1);
        return arr[idx];
    }

    static ClassificationCpu[] ReadClassifications(HarmonicPipelineController pipeline, int count)
    {
        ComputeBuffer buf = pipeline.PbfScratch.ParticleField;
        var arr = new ClassificationCpu[count];
        buf.GetData(arr, 0, 0, count);
        return arr;
    }

    static void RigidVelocityProxy(Matrix4x4 prevL2W, Matrix4x4 currL2W, float dt, Vector3 worldPos,
        out Vector3 expectedVel, out float relSpeed)
    {
        Quaternion prevRot = prevL2W.rotation;
        Quaternion currRot = currL2W.rotation;
        Quaternion deltaRot = currRot * Quaternion.Inverse(prevRot);
        deltaRot.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float safeDt = Mathf.Max(dt, 1e-5f);
        Vector3 omega = axis.sqrMagnitude > 1e-8f
            ? axis.normalized * (angleDeg * Mathf.Deg2Rad / safeDt)
            : Vector3.zero;
        Vector3 pivot = currL2W.GetColumn(3);
        Vector3 prevPivot = prevL2W.GetColumn(3);
        Vector3 linear = (pivot - prevPivot) / safeDt;
        Vector3 arm = worldPos - pivot;
        expectedVel = linear + Vector3.Cross(omega, arm);
        relSpeed = expectedVel.magnitude; // particle velocity ~ 0
    }

    static float LocalRadius(Vector3 worldPos, Matrix4x4 worldToLocal)
    {
        Vector3 l = worldToLocal.MultiplyPoint3x4(worldPos);
        return new Vector2(l.x, l.z).magnitude;
    }

    static void ClassifyReadSet(HarmonicPipelineController pipeline, uint count)
    {
        pipeline.ClassifyParticleFieldFrom(pipeline.PingPong.ReadSet.Block0, count);
        SyncGpu();
    }

    static ClassificationCpu ReadClassification(HarmonicPipelineController pipeline, int idx)
    {
        ComputeBuffer buf = pipeline.PbfScratch.ParticleField;
        var arr = new ClassificationCpu[idx + 1];
        buf.GetData(arr, 0, 0, idx + 1);
        return arr[idx];
    }

    struct ParticleState
    {
        public Vector3 Pos;
        public Vector3 Vel;
    }

    static ParticleState ReadState(HarmonicPipelineController pipeline, int idx) =>
        ReadStates(pipeline, idx + 1)[idx];

    static ParticleState[] ReadStates(HarmonicPipelineController pipeline, int count)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active));
        int n = Mathf.Min(count, (int)active);
        var states = new ParticleState[count];
        if (n <= 0)
        {
            return states;
        }
        FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
        for (int i = 0; i < count && i < n; i++)
        {
            states[i].Pos = new Vector3(p[i].Position.x, p[i].Position.y, p[i].Position.z);
            states[i].Vel = new Vector3(p[i].Velocity.x, p[i].Velocity.y, p[i].Velocity.z);
        }
        return states;
    }

    static FluidParticle MakeParticle(Vector3 position, Vector3 velocity) => new FluidParticle
    {
        Position = new float3(position.x, position.y, position.z),
        Velocity = new float3(velocity.x, velocity.y, velocity.z),
        Density = 1000f,
        Pressure = 0f,
        PackedColorRGBA = 0xFFFFFFFFu,
    };

    static void SpawnParticles(HarmonicPipelineController pipeline, FluidParticle[] particles)
    {
        int appended = pipeline.AppendParticles(particles, particles.Length);
        Assert.AreEqual(particles.Length, appended, "Failed to append diagnostic particles.");
    }

    static void Report(string fileName, string report)
    {
        string logPath = System.IO.Path.Combine(
            Application.dataPath, "..", "Logs", "HarmonicSimulation", fileName);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log(report);
        Debug.Log($"[RotCarry] trace written to {logPath}");
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

        var go = new GameObject("RotatingBucketCarryDiagnosticsPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        typeof(HarmonicPipelineController).GetField("containerRigidCarryShader", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, carryShader);
        typeof(HarmonicPipelineController).GetField("otcParticleFieldShader", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, otcFieldShader);
        typeof(HarmonicPipelineController).GetField("perfDiagnosticsMuted", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, true);

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
