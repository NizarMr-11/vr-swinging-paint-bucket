using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Reproduces firm directional bucket hold (fluid piles on trailing wall, then punches
    /// through). Reports false escape latches, rim vs wall-band vs beyond-outer-shell exits.
    /// </summary>
    public sealed class V4WallEscapeFirmHoldInvestigationTests
    {
        private const float Dt = 1f / 60f;
        private const int RampFrames = 60;
        private const int HoldFrames = 240;
        private const int Frames = RampFrames + HoldFrames;
        private const float MaxLinearSpeed = 1.2f;
        private const float LinearAccel = 6f;
        private const float BucketWorldY = 0.4f;

        private const float BucketRadius = 0.3f;
        private const float BucketHeight = 0.6f;
        private const float WallThickness = 0.05f;
        private const float TopBandHeight = 0.12f;
        private const float RingSpacing = 0.06f;
        private const float GlobalDensity = 800000f;
        private const int PbfIterations = 4;
        private const float BoundaryGhostWeight = 0.149f;

        private static readonly (Vector3 pos, float radius, Color color)[] Lab2Zones =
        {
            (new Vector3(-0.1f, 0.15f, 0f), 0.1f, new Color(0.9f, 0.1f, 0.1f)),
            (new Vector3(0.1f, 0.22f, 0f), 0.1f, new Color(0.1f, 0.2f, 0.9f)),
            (new Vector3(0.094f, 0.511f, -0.018f), 0.1f, new Color(0f, 0f, 0f)),
            (new Vector3(0.16f, 0.511f, 0.051f), 0.1f, Color.white),
            (new Vector3(0.147f, 0.511f, -0.018f), 0.1f, Color.white),
            (new Vector3(0.147f, 0.541f, -0.018f), 0.1f, Color.white)
        };

        private struct FrameRecord
        {
            public int Frame;
            public int Live;
            public int OutsideLive;
            public int BeyondOuter;
            public int OverRim;
            public int WallBandOutside;
            public int NewEscape;
            public int FalseLatch;
            public int InsideToOutside;
            public int ContainMismatch;
            public float BucketSpeedZ;
        }

        private struct DetailEvent
        {
            public int Frame;
            public string Kind;
            public int Index;
            public string Detail;
        }

        private static V4TestRig.Config Lab2FirmHoldConfig()
        {
            return new V4TestRig.Config
            {
                BucketRadius = BucketRadius,
                BucketHeight = BucketHeight,
                WallThickness = WallThickness,
                TopBandHeight = TopBandHeight,
                RingSpacing = RingSpacing,
                GlobalDensity = GlobalDensity,
                CanvasY = -1f,
                CanvasSize = 3f,
                RestrictSpawnToBucketCavity = true,
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef { localPosition = new Vector3(0.1f, 0f, 0f), radius = 0f, outwardNormal = Vector3.down },
                    new V4HoleDef { localPosition = new Vector3(-0.15f, 0f, 0.15f), radius = 0f, outwardNormal = Vector3.down }
                },
                SpawnZones = Lab2Zones.Select(z => (z.pos, z.radius, z.color)).ToList(),
                ConfigureProfile = profile =>
                {
                    profile.settleEpsilon = 0f;
                    profile.impactAbsorbSpeed = float.MaxValue;
                },
                ConfigureRoot = root =>
                {
                    root.pbfIterations = PbfIterations;
                    root.boundaryGhostWeight = BoundaryGhostWeight;
                    root.carryRate = 10f;
                    root.pbfEpsilon = 50f;
                    root.debugLogBoundaryPressure = true;
                    root.debugLogContainInsideMismatch = true;
                    root.debugLogWallEscapeForensics = true;
                }
            };
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

        [Test]
        public void Investigate_FirmDirectionalHold_WallEscapeForensics()
        {
            using var rig = V4TestRig.Create(Lab2FirmHoldConfig());
            rig.Bucket.transform.SetPositionAndRotation(new Vector3(0f, BucketWorldY, 0f), Quaternion.identity);

            var bakedHoles = rig.Root.BakedHoles.ToArray();
            var frameLog = new List<FrameRecord>(Frames);
            var details = new List<DetailEvent>();
            var beyondOuterSamples = new List<string>();
            int spawned = rig.Root.ActiveParticleCount;

            Vector4[] prevPos = null;
            uint[] prevFlags = null;
            Vector3[] prevLocalPositions = null;
            float speedZ = 0f;

            for (int frame = 0; frame < Frames; frame++)
            {
                speedZ = Mathf.MoveTowards(speedZ, MaxLinearSpeed, LinearAccel * Dt);
                Vector3 pos = rig.Bucket.transform.position;
                pos.z += speedZ * Dt;
                rig.Bucket.transform.position = pos;

                rig.Root.Step(Dt);

                Vector4[] positions = rig.ReadPositions();
                uint[] flags = rig.ReadFlags();
                int live = rig.Root.ActiveParticleCount;
                Transform bucketTransform = rig.Bucket.transform;

                int outsideLive = 0;
                int beyondOuter = 0;
                int overRim = 0;
                int wallBandOutside = 0;
                int newEscape = 0;
                int falseLatch = 0;
                int insideToOutside = 0;
                int containMismatch = 0;
                float outerR = BucketRadius + WallThickness;

                var localPositions = new Vector3[live];
                for (int i = 0; i < live; i++)
                {
                    localPositions[i] = bucketTransform.InverseTransformPoint(positions[i]);
                    Vector3 local = localPositions[i];
                    uint f = flags[i];
                    V4WallEscapeExitClass exitClass = V4WallEscapeForensics.ClassifyLocal(
                        local, f, BucketRadius, BucketHeight, WallThickness);

                    if (!V4ParticleFlags.HasEscaped(f) && !V4ParticleFlags.IsInside(f))
                    {
                        outsideLive++;
                        if (exitClass == V4WallEscapeExitClass.BeyondOuterShell)
                        {
                            beyondOuter++;
                        }
                        else if (exitClass == V4WallEscapeExitClass.OverOpenRim)
                        {
                            overRim++;
                        }
                        else if (exitClass == V4WallEscapeExitClass.OutsideWallBand)
                        {
                            wallBandOutside++;
                        }
                    }
                }

                if (prevFlags != null && live == prevFlags.Length)
                {
                    for (int i = 0; i < live; i++)
                    {
                        if (!V4ParticleFlags.HasEscaped(prevFlags[i]) && V4ParticleFlags.HasEscaped(flags[i]))
                        {
                            newEscape++;
                            V4WallEscapeForensics.HoleProximity hole =
                                V4WallEscapeForensics.NearestHole(prevLocalPositions[i], bakedHoles);
                            bool isFalse = !hole.WithinD2;
                            if (isFalse)
                            {
                                falseLatch++;
                            }

                            if (details.Count < 64)
                            {
                                Vector3 p = prevLocalPositions[i];
                                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                                details.Add(new DetailEvent
                                {
                                    Frame = frame,
                                    Kind = isFalse ? "falseLatch" : "legitEscape",
                                    Index = i,
                                    Detail =
                                        $"hole={hole.NearestHole} dist={hole.Distance:F4} d2={hole.D2:F4} r={r:F4} y={p.y:F4}"
                                });
                            }
                        }

                        if (V4ParticleFlags.IsInside(prevFlags[i]) && !V4ParticleFlags.IsInside(flags[i]) &&
                            !V4ParticleFlags.HasEscaped(flags[i]))
                        {
                            insideToOutside++;
                            Vector3 local = localPositions[i];
                            float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                            V4WallEscapeExitClass exitClass = V4WallEscapeForensics.ClassifyLocal(
                                local, flags[i], BucketRadius, BucketHeight, WallThickness);

                            if (details.Count < 64)
                            {
                                details.Add(new DetailEvent
                                {
                                    Frame = frame,
                                    Kind = "insideToOutside",
                                    Index = i,
                                    Detail =
                                        $"{V4WallEscapeForensics.ExitClassLabel(exitClass)} r={r:F4} y={local.y:F4} outerR={outerR:F4}"
                                });
                            }
                        }

                        Vector3 localNow = localPositions[i];
                        bool geometricInside = V4BucketGeometry.ShouldContainInside(
                            localNow, BucketRadius, BucketHeight, WallThickness);
                        bool containInside = V4BucketGeometry.ComputeContainInside(
                            flags[i], localNow, prevLocalPositions[i], BucketRadius, BucketHeight, WallThickness);
                        if (containInside != geometricInside)
                        {
                            containMismatch++;
                        }
                    }
                }

                if (frame == 299)
                {
                    for (int i = 0; i < live && beyondOuterSamples.Count < 12; i++)
                    {
                        if (V4ParticleFlags.HasEscaped(flags[i]) || V4ParticleFlags.IsInside(flags[i]))
                        {
                            continue;
                        }

                        Vector3 local = localPositions[i];
                        if (V4WallEscapeForensics.ClassifyLocal(local, flags[i], BucketRadius, BucketHeight, WallThickness) !=
                            V4WallEscapeExitClass.BeyondOuterShell)
                        {
                            continue;
                        }

                        float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                        beyondOuterSamples.Add($"idx={i} r={r:F4} y={local.y:F4} z={local.z:F4}");
                    }
                }

                frameLog.Add(new FrameRecord
                {
                    Frame = frame,
                    Live = live,
                    OutsideLive = outsideLive,
                    BeyondOuter = beyondOuter,
                    OverRim = overRim,
                    WallBandOutside = wallBandOutside,
                    NewEscape = newEscape,
                    FalseLatch = falseLatch,
                    InsideToOutside = insideToOutside,
                    ContainMismatch = containMismatch,
                    BucketSpeedZ = speedZ
                });

                prevPos = positions;
                prevFlags = flags;
                prevLocalPositions = localPositions;
            }

            var sb = new StringBuilder();
            sb.AppendLine("V4 firm directional hold wall-escape forensics");
            sb.AppendLine($"spawned={spawned} frames={Frames} motion=+Z ramp {RampFrames}f then hold {MaxLinearSpeed}m/s");
            sb.AppendLine($"density={GlobalDensity} pbfIter={PbfIterations} ghostWeight={BoundaryGhostWeight} settle=off");
            sb.AppendLine();
            sb.AppendLine("frame  live  outside  beyondOuter  overRim  wallBand  newEsc  falseLatch  in->out  cif  speedZ");
            int[] sample = { 60, 90, 120, 132, 150, 180, 210, 240, 270, 299 };
            foreach (int f in sample)
            {
                if (f >= frameLog.Count)
                {
                    continue;
                }

                FrameRecord r = frameLog[f];
                sb.AppendLine(
                    $"{r.Frame,5} {r.Live,5} {r.OutsideLive,8} {r.BeyondOuter,11} {r.OverRim,7} {r.WallBandOutside,8} " +
                    $"{r.NewEscape,6} {r.FalseLatch,10} {r.InsideToOutside,7} {r.ContainMismatch,3} {r.BucketSpeedZ,6:F2}");
            }

            sb.AppendLine();
            sb.AppendLine("--- peak frames ---");
            FrameRecord peakBeyond = frameLog.OrderByDescending(r => r.BeyondOuter).First();
            FrameRecord peakInOut = frameLog.OrderByDescending(r => r.InsideToOutside).First();
            FrameRecord peakCif = frameLog.OrderByDescending(r => r.ContainMismatch).First();
            sb.AppendLine(
                $"peak beyondOuter={peakBeyond.BeyondOuter}@f{peakBeyond.Frame} insideToOutside={peakInOut.InsideToOutside}@f{peakInOut.Frame} cif={peakCif.ContainMismatch}@f{peakCif.Frame}");
            sb.AppendLine($"total falseLatch={frameLog.Sum(r => r.FalseLatch)} legitEscape={frameLog.Sum(r => r.NewEscape) - frameLog.Sum(r => r.FalseLatch)}");

            sb.AppendLine();
            sb.AppendLine("--- first 40 detail events (escapes + transitions) ---");
            foreach (DetailEvent e in details.Take(40))
            {
                sb.AppendLine($"f{e.Frame} {e.Kind} idx={e.Index} {e.Detail}");
            }

            sb.AppendLine();
            sb.AppendLine("--- beyondOuter below rim @ f299 (sample) ---");
            foreach (string line in beyondOuterSamples)
            {
                sb.AppendLine(line);
            }

            WriteReport("wall_escape_firm_hold.txt", sb.ToString());

            FrameRecord end = frameLog[frameLog.Count - 1];
            Assert.Less(end.BeyondOuter, 50,
                $"beyondOuter should stay near zero after outer-shell clamp (got {end.BeyondOuter} @ f{end.Frame})");
            Assert.Pass("Firm hold wall-escape forensics logged.");
        }

        private enum ShellState
        {
            Escaped,
            OverRim,
            BeyondOuter,
            WallBand,
            Inside,
            OutsideOther
        }

        private static ShellState ClassifyShell(Vector3 local, uint flags)
        {
            if (V4ParticleFlags.HasEscaped(flags))
            {
                return ShellState.Escaped;
            }

            if (local.y > BucketHeight)
            {
                return ShellState.OverRim;
            }

            float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float outerR = BucketRadius + WallThickness;
            if (!V4ParticleFlags.IsInside(flags))
            {
                if (r > outerR + 1e-5f)
                {
                    return ShellState.BeyondOuter;
                }

                if (r > BucketRadius + 1e-5f)
                {
                    return ShellState.WallBand;
                }

                return ShellState.OutsideOther;
            }

            return ShellState.Inside;
        }

        [Test]
        public void Investigate_BeyondOuterCrossingDeltas()
        {
            using var rig = V4TestRig.Create(Lab2FirmHoldConfig());
            rig.Bucket.transform.SetPositionAndRotation(new Vector3(0f, BucketWorldY, 0f), Quaternion.identity);

            float maxCorrection = rig.Root.MaxCorrectionDistance();
            int pbfIter = PbfIterations;
            var crossings = new List<string>();
            var prevLocal = (Vector3[])null;
            var prevFlags = (uint[])null;
            float speedZ = 0f;

            for (int frame = 0; frame < Frames; frame++)
            {
                speedZ = Mathf.MoveTowards(speedZ, MaxLinearSpeed, LinearAccel * Dt);
                Vector3 bucketPos = rig.Bucket.transform.position;
                bucketPos.z += speedZ * Dt;
                rig.Bucket.transform.position = bucketPos;

                rig.Root.Step(Dt);

                Vector4[] positions = rig.ReadPositions();
                uint[] flags = rig.ReadFlags();
                int live = rig.Root.ActiveParticleCount;
                Transform bucketTransform = rig.Bucket.transform;
                var localPositions = new Vector3[live];
                for (int i = 0; i < live; i++)
                {
                    localPositions[i] = bucketTransform.InverseTransformPoint(positions[i]);
                }

                if (prevLocal != null && prevFlags != null && live == prevFlags.Length)
                {
                    for (int i = 0; i < live && crossings.Count < 48; i++)
                    {
                        ShellState prev = ClassifyShell(prevLocal[i], prevFlags[i]);
                        ShellState cur = ClassifyShell(localPositions[i], flags[i]);
                        if (cur != ShellState.BeyondOuter || prev == ShellState.BeyondOuter)
                        {
                            continue;
                        }

                        Vector3 prevWorld = rig.Bucket.transform.TransformPoint(prevLocal[i]);
                        // Note: bucket moved; small error vs true prev-world — localDelta is authoritative.
                        Vector3 worldDelta = (Vector3)positions[i] - prevWorld;
                        Vector3 localDelta = localPositions[i] - prevLocal[i];
                        float rPrev = Mathf.Sqrt(prevLocal[i].x * prevLocal[i].x + prevLocal[i].z * prevLocal[i].z);
                        float rCur = Mathf.Sqrt(localPositions[i].x * localPositions[i].x + localPositions[i].z * localPositions[i].z);

                        crossings.Add(
                            $"f{frame} idx={i} from={prev} |delta|={localDelta.magnitude:F5} wallT={WallThickness:F3} " +
                            $"maxCorr={maxCorrection:F5} pbfIter={pbfIter} r {rPrev:F5}->{rCur:F5} y={localPositions[i].y:F4}");
                    }
                }

                prevLocal = localPositions;
                prevFlags = flags;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Beyond-outer first-crossing forensics (firm hold)");
            sb.AppendLine($"maxCorrection(0.2h)={maxCorrection:F6} wallThickness={WallThickness}");
            sb.AppendLine($"max single-step budget ~ {pbfIter + 1} * maxCorr = {(pbfIter + 1) * maxCorrection:F5}");
            sb.AppendLine();
            foreach (string line in crossings)
            {
                sb.AppendLine(line);
            }

            WriteReport("beyond_outer_crossing_forensics.txt", sb.ToString());
            Assert.Pass("Beyond-outer crossing forensics logged.");
        }
    }
}
