using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Measures kinetic energy and rim events during lateral shake to test whether
    /// wall collision injects energy. Reports only.
    /// </summary>
    public sealed class V4CollisionEnergyInvestigationTests
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 300;
        private const int TallLayers = 20;
        private const int PbfIterations = 8;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;
        private const float ShakeAmplitude = 0.25f;
        private const float ShakeOmega = 4f;

        private struct FrameEnergyRecord
        {
            public int Frame;
            public double KineticEnergy;
            public int InsideCount;
            public float MidColumnMeanVelY;
            public int AboveRim;
            public int Settled;
            public float BucketVelX;
        }

        private struct EnergySession
        {
            public string Label;
            public float SurfaceRestitution;
            public List<FrameEnergyRecord> Frames;
            public int FirstRimLaunchFrame;
            public int FirstSettleFrame;
            public int FirstMidVelYSpikeFrame;
        }

        private static V4TestRig.Config TallConfig(float restitution)
        {
            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float particleRadius = V4SpawnMath.ParticleRadiusFromDensity(GlobalDensity);
            float fluidHeight = TallLayers * spacing;
            float bucketHeight = fluidHeight + particleRadius * 4f + 0.02f;
            float halfHeight = bucketHeight * 0.5f;
            float spawnRadius = Mathf.Sqrt(BucketRadius * BucketRadius + halfHeight * halfHeight);

            return new V4TestRig.Config
            {
                BucketRadius = BucketRadius,
                BucketHeight = bucketHeight,
                WallThickness = WallThickness,
                GlobalDensity = GlobalDensity,
                Holes = new List<V4HoleDef>(),
                RestrictSpawnToBucketCavity = true,
                SpawnZones = new List<(Vector3, float, Color)>
                {
                    (new Vector3(0f, halfHeight, 0f), spawnRadius, Color.cyan)
                },
                ConfigureProfile = profile => profile.surfaceRestitution = restitution,
                ConfigureRoot = root =>
                {
                    root.pbfIterations = PbfIterations;
                    root.boundaryGhostWeight = 0f;
                }
            };
        }

        private static EnergySession RunShakeSession(string label, float restitution)
        {
            using var rig = V4TestRig.Create(TallConfig(restitution));
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float midMin = 7f * spacing;
            float midMax = 13f * spacing;
            float bucketHeight = rig.Bucket.height;

            var session = new EnergySession
            {
                Label = label,
                SurfaceRestitution = restitution,
                Frames = new List<FrameEnergyRecord>(Frames),
                FirstRimLaunchFrame = -1,
                FirstSettleFrame = -1,
                FirstMidVelYSpikeFrame = -1
            };

            var previousAboveRim = new HashSet<int>();
            float time = 0f;
            float midVelBaseline = 0f;

            for (int frame = 0; frame < Frames; frame++)
            {
                time += Dt;
                rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * ShakeOmega) * ShakeAmplitude, 0f, 0f);
                rig.Step(1, Dt);

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();

                double ke = 0.0;
                int insideCount = 0;
                float midVelSum = 0f;
                int midCount = 0;
                var aboveRimNow = new HashSet<int>();

                for (int i = 0; i < positions.Length; i++)
                {
                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    insideCount++;
                    Vector3 vel = new Vector3(velocities[i].x, velocities[i].y, velocities[i].z);
                    ke += 0.5 * vel.sqrMagnitude;

                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                        new Vector3(positions[i].x, positions[i].y, positions[i].z));
                    if (local.y >= midMin && local.y <= midMax)
                    {
                        midVelSum += velocities[i].y;
                        midCount++;
                    }

                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                    }
                }

                float midMeanVelY = midCount > 0 ? midVelSum / midCount : 0f;
                if (frame == 20)
                {
                    midVelBaseline = midMeanVelY;
                }

                if (session.FirstMidVelYSpikeFrame < 0 && frame > 20 && midMeanVelY > midVelBaseline + 0.15f)
                {
                    session.FirstMidVelYSpikeFrame = frame;
                }

                foreach (int idx in aboveRimNow)
                {
                    if (!previousAboveRim.Contains(idx) && session.FirstRimLaunchFrame < 0)
                    {
                        session.FirstRimLaunchFrame = frame;
                    }
                }

                if (session.FirstSettleFrame < 0 && rig.Root.SettledTotal > 0)
                {
                    session.FirstSettleFrame = frame;
                }

                session.Frames.Add(new FrameEnergyRecord
                {
                    Frame = frame,
                    KineticEnergy = ke,
                    InsideCount = insideCount,
                    MidColumnMeanVelY = midMeanVelY,
                    AboveRim = aboveRimNow.Count,
                    Settled = rig.Root.SettledTotal,
                    BucketVelX = rig.Bucket.LinearVelocity.x
                });

                previousAboveRim = aboveRimNow;
            }

            return session;
        }

        private static void WriteReport(string fileName, string report)
        {
            string dir = Path.Combine(Application.dataPath, "HarmonicEngineV4/Tests/PlayMode/Results");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, fileName), report);
            foreach (string line in report.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Debug.Log(line.TrimEnd('\r'));
                }
            }
        }

        private static string FormatEnergyTrend(EnergySession session)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {session.Label} (surfaceRestitution={session.SurfaceRestitution}) ===");
            sb.AppendLine(
                $"firstMidVelYSpike={session.FirstMidVelYSpikeFrame} firstRimLaunch={session.FirstRimLaunchFrame} firstSettle={session.FirstSettleFrame}");
            sb.AppendLine("frame  KE_inside  insideN  midVelY  aboveRim  settled  bucketVelX");

            int[] sampleFrames = { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 299 };
            foreach (int f in sampleFrames)
            {
                if (f >= session.Frames.Count)
                {
                    continue;
                }

                FrameEnergyRecord r = session.Frames[f];
                sb.AppendLine(
                    $"{r.Frame,5}  {r.KineticEnergy,10:F1}  {r.InsideCount,7}  {r.MidColumnMeanVelY,7:F3}  {r.AboveRim,8}  {r.Settled,7}  {r.BucketVelX,9:F3}");
            }

            FrameEnergyRecord first = session.Frames[0];
            FrameEnergyRecord last = session.Frames[^1];
            FrameEnergyRecord peak = session.Frames.OrderByDescending(f => f.KineticEnergy).First();
            double meanKe = session.Frames.Average(f => f.KineticEnergy);
            sb.AppendLine(
                $"KE: start={first.KineticEnergy:F1} end={last.KineticEnergy:F1} peak={peak.KineticEnergy:F1}@f{peak.Frame} mean={meanKe:F1} ratio_end/start={(last.KineticEnergy / Mathf.Max((float)first.KineticEnergy, 1e-6f)):F2}x");
            return sb.ToString();
        }

        [Test]
        public void Investigate_ShakeKineticEnergy_MovingFrameFix()
        {
            EnergySession session = RunShakeSession("moving-frame collision fix", 0.1f);

            var report = new StringBuilder();
            report.AppendLine("V4 collision energy — lateral shake 300 frames (moving-frame reflection fix)");
            report.AppendLine($"shake: x = {ShakeAmplitude} * sin({ShakeOmega} * t), dt={Dt}");
            report.AppendLine("KE = sum 0.5*|vel|^2 over Inside-flagged live particles");
            report.Append(FormatEnergyTrend(session));
            report.AppendLine($"final: settled={session.Frames[^1].Settled} aboveRim={session.Frames[^1].AboveRim}");
            report.AppendLine($"firstRimLaunch=f{session.FirstRimLaunchFrame} firstSettle=f{session.FirstSettleFrame}");

            WriteReport("collision_energy_shake_post_fix.txt", report.ToString());
            Assert.Pass("Post-fix collision energy report logged.");
        }

        [Test]
        public void Investigate_Frame3EarlyLeak()
        {
            using var rig = V4TestRig.Create(TallConfig(0.1f));
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float bucketHeight = rig.Bucket.height;
            float time = 0f;
            const int TrackFrames = 10;
            const int RimFrame = 3;

            var kinematicsLog = new StringBuilder();
            kinematicsLog.AppendLine("V4 frame-3 early leak investigation");
            kinematicsLog.AppendLine("SampleKinematics: finite difference (pos - _prevPos)/dt; _prevPos starts default zero, _hasPrev false on first call");
            kinematicsLog.AppendLine("frame  time  posX  bucketLinVel  analyticVelX  linVelError  angVelMag");
            for (int frame = 0; frame < TrackFrames; frame++)
            {
                time += Dt;
                float posX = Mathf.Sin(time * ShakeOmega) * ShakeAmplitude;
                rig.Bucket.transform.position = new Vector3(posX, 0f, 0f);
                rig.Step(1, Dt);

                float analyticVelX = ShakeAmplitude * ShakeOmega * Mathf.Cos(time * ShakeOmega);
                Vector3 linVel = rig.Bucket.LinearVelocity;
                Vector3 angVel = rig.Bucket.AngularVelocity;
                kinematicsLog.AppendLine(
                    $"{frame,5}  {time:F4}  {posX:F6}  ({linVel.x:F4},{linVel.y:F4},{linVel.z:F4})  {analyticVelX:F6}  {linVel.x - analyticVelX:F6}  {angVel.magnitude:F6}");
            }

            // Re-run with per-particle vel.y tracking through rim frame.
            rig.Dispose();
            using var rig2 = V4TestRig.Create(TallConfig(0.1f));
            rig2.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            time = 0f;
            int particleCount = rig2.Root.ActiveParticleCount;
            var velYHistory = new float[particleCount][];
            for (int i = 0; i < particleCount; i++)
            {
                velYHistory[i] = new float[RimFrame + 1];
            }

            var previousAboveRim = new HashSet<int>();
            var rimCrossersAt3 = new List<int>();

            for (int frame = 0; frame <= RimFrame; frame++)
            {
                time += Dt;
                rig2.Bucket.transform.position = new Vector3(Mathf.Sin(time * ShakeOmega) * ShakeAmplitude, 0f, 0f);
                rig2.Step(1, Dt);

                Vector4[] positions = rig2.ReadPositions();
                Vector4[] velocities = rig2.ReadVelocities();
                uint[] flags = rig2.ReadFlags();
                var aboveRimNow = new HashSet<int>();

                for (int i = 0; i < positions.Length; i++)
                {
                    velYHistory[i][frame] = velocities[i].y;

                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    Vector3 local = rig2.Bucket.transform.InverseTransformPoint(
                        new Vector3(positions[i].x, positions[i].y, positions[i].z));
                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                    }
                }

                if (frame == RimFrame)
                {
                    foreach (int idx in aboveRimNow)
                    {
                        if (!previousAboveRim.Contains(idx))
                        {
                            rimCrossersAt3.Add(idx);
                        }
                    }
                }

                previousAboveRim = aboveRimNow;
            }

            kinematicsLog.AppendLine();
            kinematicsLog.AppendLine($"rim crossers at frame {RimFrame}: count={rimCrossersAt3.Count}");
            const int maxReport = 8;
            for (int c = 0; c < Mathf.Min(rimCrossersAt3.Count, maxReport); c++)
            {
                int idx = rimCrossersAt3[c];
                kinematicsLog.Append($"  particle[{idx}] vel.y f0..f{RimFrame}:");
                for (int f = 0; f <= RimFrame; f++)
                {
                    kinematicsLog.Append($" {velYHistory[idx][f]:F4}");
                }

                kinematicsLog.AppendLine();
            }

            if (rimCrossersAt3.Count > maxReport)
            {
                kinematicsLog.AppendLine($"  ... and {rimCrossersAt3.Count - maxReport} more");
            }

            kinematicsLog.AppendLine();
            kinematicsLog.AppendLine("carry uses SampleKinematics LinearVelocity/AngularVelocity from the same Step call (before forces dispatch)");
            kinematicsLog.AppendLine($"frame-0 carry sees LinearVelocity=0 while bucket already at x={Mathf.Sin(Dt * ShakeOmega) * ShakeAmplitude:F6}");

            WriteReport("collision_frame3_early_leak.txt", kinematicsLog.ToString());
            Assert.Pass("Frame-3 early leak report logged.");
        }

        [Test]
        public void Investigate_ShakeKineticEnergy_DefaultAndZeroRestitution()
        {
            EnergySession defaultRest = RunShakeSession("default restitution", 0.1f);
            EnergySession zeroRest = RunShakeSession("zero restitution", 0.0f);

            var report = new StringBuilder();
            report.AppendLine("V4 collision energy investigation — lateral shake 300 frames");
            report.AppendLine($"shake: x = {ShakeAmplitude} * sin({ShakeOmega} * t), dt={Dt}");
            report.AppendLine("KE = sum 0.5*|vel|^2 over Inside-flagged live particles");
            report.AppendLine("active profile default surfaceRestitution=0.1 (V4Liquid_Default)");
            report.Append(FormatEnergyTrend(defaultRest));
            report.Append(FormatEnergyTrend(zeroRest));
            report.AppendLine("--- cross-reference ---");
            report.AppendLine(
                $"default: midVelY spike f{defaultRest.FirstMidVelYSpikeFrame} rim f{defaultRest.FirstRimLaunchFrame} settle f{defaultRest.FirstSettleFrame}");
            report.AppendLine(
                $"zeroRe:  midVelY spike f{zeroRest.FirstMidVelYSpikeFrame} rim f{zeroRest.FirstRimLaunchFrame} settle f{zeroRest.FirstSettleFrame}");

            FrameEnergyRecord defPeak = defaultRest.Frames.OrderByDescending(f => f.KineticEnergy).First();
            FrameEnergyRecord zeroPeak = zeroRest.Frames.OrderByDescending(f => f.KineticEnergy).First();
            if (defaultRest.FirstMidVelYSpikeFrame >= 0)
            {
                FrameEnergyRecord atSpike = defaultRest.Frames[defaultRest.FirstMidVelYSpikeFrame];
                report.AppendLine(
                    $"default at midVelY spike f{atSpike.Frame}: KE={atSpike.KineticEnergy:F1} aboveRim={atSpike.AboveRim}");
            }

            report.AppendLine($"default final: settled={defaultRest.Frames[^1].Settled} aboveRim={defaultRest.Frames[^1].AboveRim}");
            report.AppendLine($"zeroRe final:  settled={zeroRest.Frames[^1].Settled} aboveRim={zeroRest.Frames[^1].AboveRim}");

            WriteReport("collision_energy_shake.txt", report.ToString());
            Assert.Pass("Collision energy report logged.");
        }

        [Test]
        public void Investigate_ShakeMotion_IsSmoothSinusoidal()
        {
            float time = 0f;
            var positions = new List<float>(Frames);
            var velocities = new List<float>(Frames - 1);
            for (int frame = 0; frame < Frames; frame++)
            {
                time += Dt;
                float x = Mathf.Sin(time * ShakeOmega) * ShakeAmplitude;
                positions.Add(x);
                if (frame > 0)
                {
                    velocities.Add((positions[frame] - positions[frame - 1]) / Dt);
                }
            }

            float maxVel = velocities.Max(v => Mathf.Abs(v));
            float minVel = velocities.Min(v => Mathf.Abs(v));
            float expectedMax = ShakeAmplitude * ShakeOmega;
            int signChanges = 0;
            for (int i = 1; i < velocities.Count; i++)
            {
                if (Mathf.Sign(velocities[i]) != Mathf.Sign(velocities[i - 1]) && Mathf.Abs(velocities[i]) > 0.01f)
                {
                    signChanges++;
                }
            }

            var report = new StringBuilder();
            report.AppendLine("V4 shake motion profile (test harness, not V4BucketMotionController)");
            report.AppendLine($"x(t) = {ShakeAmplitude} * sin({ShakeOmega} * t)");
            report.AppendLine($"peak |bucketVelX| measured={maxVel:F3} theoretical={expectedMax:F3}");
            report.AppendLine($"velocity sign reversals in 300 frames: {signChanges} (~{signChanges / (Frames * Dt / (2f * Mathf.PI / ShakeOmega)):F1} per cycle)");
            report.AppendLine("V4BucketMotionController: keyboard-driven, integrates position += input*speed*dt each frame (continuous, not teleport steps)");
            report.AppendLine("Test shake: smooth sinusoidal position — no abrupt per-frame velocity reversals");

            WriteReport("collision_shake_motion_profile.txt", report.ToString());
            Assert.Pass("Shake motion profile logged.");
        }
    }
}
