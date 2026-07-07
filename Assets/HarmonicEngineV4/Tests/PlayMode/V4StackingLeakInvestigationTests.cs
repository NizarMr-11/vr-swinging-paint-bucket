using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Profiles;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Investigates whether floor ghost over-compression launches particles over the open
    /// rim into canvas settling. Reports only — no simulation fixes.
    /// </summary>
    public sealed class V4StackingLeakInvestigationTests
    {
        private const float Dt = 1f / 60f;
        private const int SettleFrames = 320;
        private const int TallLayers = 20;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;
        private const int SampleEvery = 20;

        private struct FrameSample
        {
            public int Frame;
            public int Live;
            public int Escaped;
            public int Settled;
            public int AboveRim;
            public int BelowFloor;
            public int OutsideWall;
            public int CornerRegion;
            public int AboveRimNotEscaped;
        }

        private struct IsolationResult
        {
            public string Label;
            public bool GhostOn;
            public bool ScorrOn;
            public int Spawned;
            public int Live;
            public int Escaped;
            public int Settled;
            public float BottomLayerRatio;
            public int CornerCount;
            public float CornerAvgDensity;
            public float CornerAvgRatio;
            public int FloorOnlyCount;
            public float FloorOnlyAvgDensity;
            public float FloorOnlyAvgRatio;
        }

        private static V4TestRig.Config TallColumnConfig(bool ghostOn, bool scorrOn)
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
                ConfigureProfile = profile =>
                {
                    profile.pbfKCorr = scorrOn ? 0.1f : 0f;
                }
            };
        }

        private static void WriteReport(string fileName, string report)
        {
            string dir = Path.Combine(Application.dataPath, "HarmonicEngineV4/Tests/PlayMode/Results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, report);
            foreach (string line in report.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Debug.Log(line.TrimEnd('\r'));
                }
            }
        }

        private static FrameSample SampleFrame(V4TestRig rig, int frame)
        {
            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();
            float h = rig.Root.SmoothingRadius;
            float innerRadius = rig.Bucket.innerRadius;
            float bucketHeight = rig.Bucket.height;

            int aboveRim = 0;
            int belowFloor = 0;
            int outsideWall = 0;
            int corner = 0;
            int aboveRimNotEscaped = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                    new Vector3(positions[i].x, positions[i].y, positions[i].z));
                float radXZ = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                bool escaped = V4ParticleFlags.HasEscaped(flags[i]);

                if (local.y > bucketHeight)
                {
                    aboveRim++;
                    if (!escaped)
                    {
                        aboveRimNotEscaped++;
                    }
                }

                if (local.y < 0f)
                {
                    belowFloor++;
                }

                if (radXZ > innerRadius + 1e-3f)
                {
                    outsideWall++;
                }

                if (local.y < h && radXZ > innerRadius - h)
                {
                    corner++;
                }
            }

            return new FrameSample
            {
                Frame = frame,
                Live = rig.Root.ActiveParticleCount,
                Escaped = rig.Root.EscapedTotal,
                Settled = rig.Root.SettledTotal,
                AboveRim = aboveRim,
                BelowFloor = belowFloor,
                OutsideWall = outsideWall,
                CornerRegion = corner,
                AboveRimNotEscaped = aboveRimNotEscaped
            };
        }

        private static List<FrameSample> RunTimeline(V4TestRig rig, int frames, int pbfIterations, bool ghostOn)
        {
            rig.Root.pbfIterations = pbfIterations;
            rig.Root.DebugDisableBoundaryGhosts = !ghostOn;
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var samples = new List<FrameSample>();
            for (int frame = 0; frame < frames; frame++)
            {
                rig.Step(1, Dt);
                if (frame % SampleEvery == 0 || frame == frames - 1)
                {
                    samples.Add(SampleFrame(rig, frame));
                }
            }

            return samples;
        }

        private static string FormatTimeline(string title, List<FrameSample> samples)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("frame  live  escaped  settled  aboveRim  aboveRimLive  belowFloor  outsideWall  corner");
            foreach (FrameSample s in samples)
            {
                sb.AppendLine(
                    $"{s.Frame,5}  {s.Live,4}  {s.Escaped,7}  {s.Settled,7}  {s.AboveRim,8}  {s.AboveRimNotEscaped,12}  {s.BelowFloor,10}  {s.OutsideWall,11}  {s.CornerRegion,6}");
            }

            FrameSample first = samples[0];
            FrameSample last = samples[samples.Count - 1];
            sb.AppendLine(
                $"delta settled={last.Settled - first.Settled}  delta aboveRim(max)={MaxAboveRim(samples)}  spawned={last.Live + last.Settled}");
            return sb.ToString();
        }

        private static int MaxAboveRim(List<FrameSample> samples)
        {
            int max = 0;
            foreach (FrameSample s in samples)
            {
                if (s.AboveRim > max)
                {
                    max = s.AboveRim;
                }
            }

            return max;
        }

        private static float BottomLayerRatio(V4TestRig rig)
        {
            float restDensity = rig.Root.GpuRestDensity();
            float layerBand = rig.Root.ParticleRadius * 2f;
            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();
            float[] densities = rig.ReadDensities();

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                if (V4ParticleFlags.HasEscaped(flags[i]))
                {
                    continue;
                }

                Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                    new Vector3(positions[i].x, positions[i].y, positions[i].z));
                if (local.y < 0f || local.y > rig.Bucket.height)
                {
                    continue;
                }

                int layer = Mathf.FloorToInt(local.y / layerBand);
                if (layer != 0)
                {
                    continue;
                }

                sum += densities[i];
                count++;
            }

            return count > 0 ? (sum / count) / restDensity : 0f;
        }

        private static IsolationResult RunIsolationCase(string label, bool ghostOn, bool scorrOn)
        {
            using var rig = V4TestRig.Create(TallColumnConfig(ghostOn, scorrOn));
            int spawned = rig.Root.SpawnedTotal;
            RunTimeline(rig, SettleFrames, 8, ghostOn);

            float h = rig.Root.SmoothingRadius;
            float innerRadius = rig.Bucket.innerRadius;
            float restDensity = rig.Root.GpuRestDensity();
            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();
            float[] densities = rig.ReadDensities();

            float cornerSum = 0f;
            int cornerCount = 0;
            float floorSum = 0f;
            int floorCount = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                if (V4ParticleFlags.HasEscaped(flags[i]))
                {
                    continue;
                }

                Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                    new Vector3(positions[i].x, positions[i].y, positions[i].z));
                if (local.y < 0f || local.y > rig.Bucket.height)
                {
                    continue;
                }

                float radXZ = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                bool floorBand = local.y < h;
                bool wallBand = radXZ > innerRadius - h;
                if (floorBand && wallBand)
                {
                    cornerSum += densities[i];
                    cornerCount++;
                }
                else if (floorBand && !wallBand)
                {
                    floorSum += densities[i];
                    floorCount++;
                }
            }

            return new IsolationResult
            {
                Label = label,
                GhostOn = ghostOn,
                ScorrOn = scorrOn,
                Spawned = spawned,
                Live = rig.Root.ActiveParticleCount,
                Escaped = rig.Root.EscapedTotal,
                Settled = rig.Root.SettledTotal,
                BottomLayerRatio = BottomLayerRatio(rig),
                CornerCount = cornerCount,
                CornerAvgDensity = cornerCount > 0 ? cornerSum / cornerCount : 0f,
                CornerAvgRatio = cornerCount > 0 ? (cornerSum / cornerCount) / restDensity : 0f,
                FloorOnlyCount = floorCount,
                FloorOnlyAvgDensity = floorCount > 0 ? floorSum / floorCount : 0f,
                FloorOnlyAvgRatio = floorCount > 0 ? (floorSum / floorCount) / restDensity : 0f
            };
        }

        private static string FormatIsolation(IsolationResult r)
        {
            return
                $"{r.Label}: ghost={(r.GhostOn ? "ON" : "OFF")} scorr={(r.ScorrOn ? "ON" : "OFF")} " +
                $"spawned={r.Spawned} live={r.Live} escaped={r.Escaped} settled={r.Settled} " +
                $"bottomRhoRatio={r.BottomLayerRatio:F4} " +
                $"corner(n={r.CornerCount}) rho/rho0={r.CornerAvgRatio:F4} " +
                $"floorOnly(n={r.FloorOnlyCount}) rho/rho0={r.FloorOnlyAvgRatio:F4}";
        }

        [Test]
        public void Investigate_LeakPath_Timeline()
        {
            using var rig2 = V4TestRig.Create(TallColumnConfig(true, true));
            List<FrameSample> iter2 = RunTimeline(rig2, SettleFrames, 2, true);

            using var rig8 = V4TestRig.Create(TallColumnConfig(true, true));
            List<FrameSample> iter8 = RunTimeline(rig8, SettleFrames, 8, true);

            var report = new StringBuilder();
            report.AppendLine("V4 stacking leak investigation — sealed tall column timelines");
            report.Append(FormatTimeline("=== iter=2 (ghost ON, s_corr ON) ===", iter2));
            report.Append(FormatTimeline("=== iter=8 (ghost ON, s_corr ON) ===", iter8));
            report.AppendLine("--- interpretation ---");
            report.AppendLine(
                $"iter2 final: settled={iter2[iter2.Count - 1].Settled} escaped={iter2[iter2.Count - 1].Escaped} live={iter2[iter2.Count - 1].Live}");
            report.AppendLine(
                $"iter8 final: settled={iter8[iter8.Count - 1].Settled} escaped={iter8[iter8.Count - 1].Escaped} live={iter8[iter8.Count - 1].Live}");

            WriteReport("stacking_leak_timeline.txt", report.ToString());
            Assert.Pass("Leak timeline logged.");
        }

        [Test]
        public void Investigate_GhostScorr_Isolation()
        {
            IsolationResult bothOn = RunIsolationCase("a) ghost+s_corr", true, true);
            IsolationResult ghostOnly = RunIsolationCase("b) ghost only", true, false);
            IsolationResult scorrOnly = RunIsolationCase("c) s_corr only", false, true);

            var report = new StringBuilder();
            report.AppendLine("V4 stacking leak investigation — ghost/s_corr isolation (tall, iter=8, 320 frames)");
            report.AppendLine(FormatIsolation(bothOn));
            report.AppendLine(FormatIsolation(ghostOnly));
            report.AppendLine(FormatIsolation(scorrOnly));
            report.AppendLine("--- bottom rho/rho0 comparison ---");
            report.AppendLine($"both ON:  {bothOn.BottomLayerRatio:F4}");
            report.AppendLine($"ghost ON, s_corr OFF: {ghostOnly.BottomLayerRatio:F4}");
            report.AppendLine($"ghost OFF, s_corr ON: {scorrOnly.BottomLayerRatio:F4}");
            report.AppendLine("--- corner vs floor-only density (both ON) ---");
            report.AppendLine(
                $"corner rho/rho0={bothOn.CornerAvgRatio:F4} (n={bothOn.CornerCount}) vs floor-only={bothOn.FloorOnlyAvgRatio:F4} (n={bothOn.FloorOnlyCount}) ratio={bothOn.CornerAvgRatio / Mathf.Max(bothOn.FloorOnlyAvgRatio, 1e-6f):F2}x");

            WriteReport("stacking_leak_isolation.txt", report.ToString());
            Assert.Pass("Isolation study logged.");
        }
    }
}
