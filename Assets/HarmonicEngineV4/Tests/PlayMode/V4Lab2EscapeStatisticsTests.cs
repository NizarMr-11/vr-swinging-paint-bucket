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
    /// Replicates the exact HarmonicEngineLab2 scene configuration (3 spawn zones,
    /// 2 floor holes, density 1e6, pbfIterations=2) with the bucket at rest and logs
    /// numeric statistics on every particle that leaves the bucket cavity: exit path
    /// (over rim / through wall / through floor / hole eject), exit position, velocity,
    /// timing, and spawn-zone attribution. Report only, no assertions on behavior.
    /// </summary>
    public sealed class V4Lab2EscapeStatisticsTests
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 600;

        // --- Lab2 scene values (HarmonicEngineLab2.unity) ---
        private const float BucketRadius = 0.3f;
        private const float BucketHeight = 0.6f;
        private const float WallThickness = 0.05f;
        private const float TopBandHeight = 0.12f;
        private const float RingSpacing = 0.06f;
        private const float GlobalDensity = 1000000f;
        private const int PbfIterations = 2;

        // Spawn zones in bucket-local coords (scene world pos minus bucket world y=0.4).
        private static readonly (Vector3 pos, float radius, Color color)[] Lab2Zones =
        {
            (new Vector3(-0.1f, 0.15f, 0f), 0.1f, new Color(0.9f, 0.1f, 0.1f)),   // red
            (new Vector3(0.094f, 0.511f, -0.018f), 0.15f, new Color(0f, 0f, 0f)), // black (clipped at rim)
            (new Vector3(0.1f, 0.22f, 0f), 0.1f, new Color(0.1f, 0.2f, 0.9f))     // blue
        };

        private enum ExitPath { OverRim, ThroughWall, ThroughFloor, HoleEject, Unknown }

        private struct ExitEvent
        {
            public int Frame;
            public int Index;
            public ExitPath Path;
            public Vector3 LocalPos;
            public Vector3 Velocity;
            public Vector3 PrevLocalPos;
            public Vector3 PrevVelocity;
            public int ColorZone; // nearest spawn color 0..2, -1 unknown
        }

        private static V4TestRig.Config Lab2Config()
        {
            return new V4TestRig.Config
            {
                BucketRadius = BucketRadius,
                BucketHeight = BucketHeight,
                WallThickness = WallThickness,
                TopBandHeight = TopBandHeight,
                RingSpacing = RingSpacing,
                GlobalDensity = GlobalDensity,
                CanvasY = -1f, // world -0.6 with bucket at world y=0.4
                CanvasSize = 3f,
                RestrictSpawnToBucketCavity = true,
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef { localPosition = new Vector3(0.1f, 0f, 0f), radius = 0f, outwardNormal = Vector3.down },
                    new V4HoleDef { localPosition = new Vector3(-0.15f, 0f, 0.15f), radius = 0f, outwardNormal = Vector3.down }
                },
                SpawnZones = Lab2Zones.Select(z => (z.pos, z.radius, z.color)).ToList(),
                ConfigureRoot = root =>
                {
                    root.pbfIterations = PbfIterations;
                    root.boundaryGhostWeight = 0f;
                    root.carryRate = 10f;
                    root.pbfEpsilon = 50f;
                }
            };
        }

        private static int NearestColorZone(uint packed)
        {
            float r = (packed & 0xFFu) / 255f;
            float g = ((packed >> 8) & 0xFFu) / 255f;
            float b = ((packed >> 16) & 0xFFu) / 255f;
            int best = -1;
            float bestDist = float.MaxValue;
            for (int z = 0; z < Lab2Zones.Length; z++)
            {
                Color c = Lab2Zones[z].color;
                float d = (r - c.r) * (r - c.r) + (g - c.g) * (g - c.g) + (b - c.b) * (b - c.b);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = z;
                }
            }

            return best;
        }

        private static ExitPath ClassifyExit(Vector3 local)
        {
            float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            if (local.y > BucketHeight)
            {
                return ExitPath.OverRim;
            }

            if (local.y < 0f)
            {
                return ExitPath.ThroughFloor;
            }

            if (r > BucketRadius)
            {
                return ExitPath.ThroughWall;
            }

            return ExitPath.Unknown;
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
        public void Investigate_Lab2RestEscapeStatistics()
        {
            using var rig = V4TestRig.Create(Lab2Config());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int spawned = rig.Root.ActiveParticleCount;
            var bakedHoles = rig.Root.BakedHoles;

            var exitEvents = new List<ExitEvent>();
            var holeEjectFrames = new List<(int frame, int count)>();
            var frameLog = new List<(int frame, int live, int inside, int outsideLive, int aboveRim, int escaped, int settled, float maxY)>();

            Vector4[] prevPos = null;
            Vector4[] prevVel = null;
            uint[] prevFlags = null;
            int prevLive = spawned;
            int prevEscapedTotal = 0;
            int skippedTransitionFrames = 0;
            int reentries = 0;

            for (int frame = 0; frame < Frames; frame++)
            {
                rig.Step(1, Dt);

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();
                uint[] colors = rig.ReadColors();
                int live = rig.Root.ActiveParticleCount;

                int inside = 0;
                int outsideLive = 0;
                int aboveRim = 0;
                float maxY = float.MinValue;

                for (int i = 0; i < live; i++)
                {
                    bool isIn = V4ParticleFlags.IsInside(flags[i]);
                    bool esc = V4ParticleFlags.HasEscaped(flags[i]);
                    Vector3 local = positions[i];
                    if (isIn)
                    {
                        inside++;
                        maxY = Mathf.Max(maxY, local.y);
                    }
                    else if (!esc)
                    {
                        outsideLive++;
                    }

                    if (local.y > BucketHeight && Mathf.Sqrt(local.x * local.x + local.z * local.z) <= BucketRadius + WallThickness)
                    {
                        aboveRim++;
                    }
                }

                int escapedTotal = rig.Root.EscapedTotal;
                if (escapedTotal > prevEscapedTotal)
                {
                    holeEjectFrames.Add((frame, escapedTotal - prevEscapedTotal));
                }

                // Transition detection only when indices are stable (no removals this frame).
                if (prevFlags != null && live == prevLive)
                {
                    for (int i = 0; i < live; i++)
                    {
                        bool wasIn = V4ParticleFlags.IsInside(prevFlags[i]);
                        bool isIn = V4ParticleFlags.IsInside(flags[i]);
                        bool escNow = V4ParticleFlags.HasEscaped(flags[i]);
                        bool escBefore = V4ParticleFlags.HasEscaped(prevFlags[i]);

                        if (wasIn && !isIn)
                        {
                            exitEvents.Add(new ExitEvent
                            {
                                Frame = frame,
                                Index = i,
                                Path = escNow ? ExitPath.HoleEject : ClassifyExit(positions[i]),
                                LocalPos = positions[i],
                                Velocity = velocities[i],
                                PrevLocalPos = prevPos[i],
                                PrevVelocity = prevVel[i],
                                ColorZone = NearestColorZone(colors[i])
                            });
                        }
                        else if (!wasIn && isIn && !escBefore)
                        {
                            reentries++;
                        }
                    }
                }
                else if (prevFlags != null)
                {
                    skippedTransitionFrames++;
                }

                if (frame % 30 == 0 || frame == Frames - 1)
                {
                    frameLog.Add((frame, live, inside, outsideLive, aboveRim, escapedTotal, rig.Root.SettledTotal, maxY));
                }

                prevPos = positions;
                prevVel = velocities;
                prevFlags = flags;
                prevLive = live;
                prevEscapedTotal = escapedTotal;
            }

            // ---- report ----
            var sb = new StringBuilder();
            sb.AppendLine("V4 Lab2 escape statistics — bucket at rest, exact scene config");
            sb.AppendLine($"config: R={BucketRadius} H={BucketHeight} wall={WallThickness} density={GlobalDensity} iters={PbfIterations} ghostW=0 carryRate=10 eps=50");
            sb.AppendLine($"spawned={spawned} particleRadius={rig.Root.ParticleRadius:F4} h={rig.Root.SmoothingRadius:F4}");
            sb.AppendLine("holes (baked):");
            foreach (var h in bakedHoles)
            {
                sb.AppendLine($"  local=({h.localPosition.x:F3},{h.localPosition.y:F3},{h.localPosition.z:F3}) r={h.radius:F3} d0={h.d0:F3} d1={h.d1:F3} d2={h.d2:F3}");
            }

            sb.AppendLine();
            sb.AppendLine("frame  live  inside  outsideLive  aboveRim  escapedTot  settledTot  maxInsideY");
            foreach (var f in frameLog)
            {
                sb.AppendLine($"{f.frame,5}  {f.live,5}  {f.inside,6}  {f.outsideLive,11}  {f.aboveRim,8}  {f.escaped,10}  {f.settled,10}  {f.maxY,9:F4}");
            }

            sb.AppendLine();
            sb.AppendLine($"=== exit events (Inside -> not Inside transitions, {Frames} frames) ===");
            sb.AppendLine($"total gross exits={exitEvents.Count} reentries={reentries} skippedTransitionFrames={skippedTransitionFrames}");

            foreach (var group in exitEvents.GroupBy(e => e.Path).OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"  {group.Key}: {group.Count()}");
            }

            sb.AppendLine();
            sb.AppendLine("exit timing histogram (60-frame bins):");
            for (int bin = 0; bin < Frames / 60; bin++)
            {
                int lo = bin * 60;
                int hi = lo + 60;
                var inBin = exitEvents.Where(e => e.Frame >= lo && e.Frame < hi).ToList();
                sb.AppendLine($"  f{lo}-{hi}: total={inBin.Count} rim={inBin.Count(e => e.Path == ExitPath.OverRim)} wall={inBin.Count(e => e.Path == ExitPath.ThroughWall)} floor={inBin.Count(e => e.Path == ExitPath.ThroughFloor)} hole={inBin.Count(e => e.Path == ExitPath.HoleEject)}");
            }

            var rimExits = exitEvents.Where(e => e.Path == ExitPath.OverRim).ToList();
            if (rimExits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== over-rim exit statistics ===");
                sb.AppendLine($"count={rimExits.Count} firstFrame={rimExits.Min(e => e.Frame)} lastFrame={rimExits.Max(e => e.Frame)}");
                sb.AppendLine($"exit vel.y: mean={rimExits.Average(e => e.Velocity.y):F3} min={rimExits.Min(e => e.Velocity.y):F3} max={rimExits.Max(e => e.Velocity.y):F3}");
                sb.AppendLine($"exit speed: mean={rimExits.Average(e => e.Velocity.magnitude):F3} max={rimExits.Max(e => e.Velocity.magnitude):F3}");
                sb.AppendLine($"exit local y: mean={rimExits.Average(e => e.LocalPos.y):F4} max={rimExits.Max(e => e.LocalPos.y):F4}");
                var rArr = rimExits.Select(e => Mathf.Sqrt(e.LocalPos.x * e.LocalPos.x + e.LocalPos.z * e.LocalPos.z) / BucketRadius).ToList();
                sb.AppendLine($"exit r/R: mean={rArr.Average():F3} min={rArr.Min():F3} max={rArr.Max():F3}  (1.0 = at wall)");
                sb.AppendLine("radial distribution of rim exits: " +
                    $"r/R<0.5: {rArr.Count(v => v < 0.5f)}, 0.5-0.8: {rArr.Count(v => v >= 0.5f && v < 0.8f)}, " +
                    $"0.8-1.0: {rArr.Count(v => v >= 0.8f && v < 1.0f)}, >1.0: {rArr.Count(v => v >= 1.0f)}");
                sb.AppendLine("spawn attribution (nearest color): " +
                    $"red={rimExits.Count(e => e.ColorZone == 0)} black={rimExits.Count(e => e.ColorZone == 1)} blue={rimExits.Count(e => e.ColorZone == 2)}");
            }

            var wallExits = exitEvents.Where(e => e.Path == ExitPath.ThroughWall).ToList();
            if (wallExits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== through-wall exit statistics (should be impossible) ===");
                sb.AppendLine($"count={wallExits.Count}");
                sb.AppendLine($"exit local y: mean={wallExits.Average(e => e.LocalPos.y):F4}");
                var rw = wallExits.Select(e => Mathf.Sqrt(e.LocalPos.x * e.LocalPos.x + e.LocalPos.z * e.LocalPos.z)).ToList();
                sb.AppendLine($"exit r: mean={rw.Average():F4} max={rw.Max():F4}  (innerR={BucketRadius}, outerR={BucketRadius + WallThickness})");
            }

            var floorExits = exitEvents.Where(e => e.Path == ExitPath.ThroughFloor).ToList();
            if (floorExits.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== through-floor exit statistics ===");
                sb.AppendLine($"count={floorExits.Count}");
                sb.AppendLine($"exit local y: mean={floorExits.Average(e => e.LocalPos.y):F4} min={floorExits.Min(e => e.LocalPos.y):F4}");
                int nearHole = floorExits.Count(e => bakedHoles.Any(h =>
                    Vector3.Distance(e.LocalPos, h.localPosition) <= h.d2));
                sb.AppendLine($"within d2 of a hole: {nearHole}/{floorExits.Count}");
            }

            sb.AppendLine();
            sb.AppendLine($"hole ejects (EscapedTotal increments): total={holeEjectFrames.Sum(h => h.count)} events={holeEjectFrames.Count}");
            if (holeEjectFrames.Count > 0)
            {
                sb.AppendLine($"  first at f{holeEjectFrames[0].frame}");
            }

            sb.AppendLine();
            sb.AppendLine("=== first 10 illegitimate exit examples (non-hole) ===");
            foreach (var e in exitEvents.Where(e => e.Path != ExitPath.HoleEject).Take(10))
            {
                sb.AppendLine(
                    $"f{e.Frame} idx={e.Index} path={e.Path} zone={(e.ColorZone == 0 ? "red" : e.ColorZone == 1 ? "black" : "blue")}");
                sb.AppendLine(
                    $"   prev local=({e.PrevLocalPos.x:F4},{e.PrevLocalPos.y:F4},{e.PrevLocalPos.z:F4}) vel=({e.PrevVelocity.x:F3},{e.PrevVelocity.y:F3},{e.PrevVelocity.z:F3})");
                sb.AppendLine(
                    $"   exit local=({e.LocalPos.x:F4},{e.LocalPos.y:F4},{e.LocalPos.z:F4}) vel=({e.Velocity.x:F3},{e.Velocity.y:F3},{e.Velocity.z:F3})");
            }

            WriteReport("lab2_escape_statistics.txt", sb.ToString());
            Assert.Pass("Lab2 escape statistics logged.");
        }

        [Test]
        public void Investigate_Lab2OutsideLiveAnatomy()
        {
            using var rig = V4TestRig.Create(Lab2Config());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var sb = new StringBuilder();
            sb.AppendLine("V4 Lab2 — anatomy of live particles by flag state and physical location");
            sb.AppendLine("bands: subFloorDeep y<-0.2 | subFloorLag -0.2..0 | cavity 0..H,r<=R | wallBand r in (R,R+t) | overRim y>H | outsideFar");
            sb.AppendLine();

            int[] snapshotFrames = { 30, 60, 120, 300, 599 };
            int next = 0;

            for (int frame = 0; frame < Frames; frame++)
            {
                rig.Step(1, Dt);
                if (next >= snapshotFrames.Length || frame != snapshotFrames[next])
                {
                    continue;
                }

                next++;
                Vector4[] positions = rig.ReadPositions();
                uint[] flags = rig.ReadFlags();
                int live = rig.Root.ActiveParticleCount;

                var counts = new Dictionary<string, int>();
                void Bump(string key)
                {
                    counts.TryGetValue(key, out int c);
                    counts[key] = c + 1;
                }

                for (int i = 0; i < live; i++)
                {
                    bool isIn = V4ParticleFlags.IsInside(flags[i]);
                    bool esc = V4ParticleFlags.HasEscaped(flags[i]);
                    string state = isIn ? "inside" : esc ? "escaped" : "outsideLive";

                    Vector3 p = positions[i];
                    float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    string band;
                    if (p.y < -0.2f)
                    {
                        band = "subFloorDeep";
                    }
                    else if (p.y < 0f)
                    {
                        band = r <= BucketRadius ? "subFloorLag" : "subFloorLagOutsideR";
                    }
                    else if (p.y > BucketHeight)
                    {
                        band = r <= BucketRadius + WallThickness ? "overRim" : "outsideFar";
                    }
                    else if (r <= BucketRadius)
                    {
                        band = "cavity";
                    }
                    else if (r < BucketRadius + WallThickness)
                    {
                        band = "wallBand";
                    }
                    else
                    {
                        band = "outsideFar";
                    }

                    Bump($"{state}/{band}");
                }

                sb.AppendLine($"--- frame {frame} live={live} escapedTot={rig.Root.EscapedTotal} settledTot={rig.Root.SettledTotal} ---");
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                {
                    sb.AppendLine($"  {kv.Key}: {kv.Value}");
                }

                // Detail: outsideLive particles in the cavity band — how close to floor/wall?
                var flickerY = new List<float>();
                var flickerWallDist = new List<float>();
                for (int i = 0; i < live; i++)
                {
                    if (V4ParticleFlags.IsInside(flags[i]) || V4ParticleFlags.HasEscaped(flags[i]))
                    {
                        continue;
                    }

                    Vector3 p = positions[i];
                    float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    if (p.y >= 0f && p.y <= BucketHeight && r <= BucketRadius)
                    {
                        flickerY.Add(p.y);
                        flickerWallDist.Add(BucketRadius - r);
                    }
                }

                if (flickerY.Count > 0)
                {
                    sb.AppendLine($"  outsideLive-in-cavity detail (n={flickerY.Count}):");
                    sb.AppendLine($"    y: mean={flickerY.Average():F4} p95={Percentile(flickerY, 0.95f):F4} max={flickerY.Max():F4}");
                    sb.AppendLine($"    distToWall: mean={flickerWallDist.Average():F4} min={flickerWallDist.Min():F6}");
                    sb.AppendLine($"    at floor (y<0.01): {flickerY.Count(y => y < 0.01f)}  at wall (dist<0.001): {flickerWallDist.Count(d => d < 0.001f)}");
                }

                sb.AppendLine();
            }

            WriteReport("lab2_outside_live_anatomy.txt", sb.ToString());
            Assert.Pass("Lab2 outsideLive anatomy logged.");
        }

        private static float Percentile(List<float> values, float p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int idx = Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }

        [Test]
        public void Investigate_Lab2FloorCrossingForensics()
        {
            using var rig = V4TestRig.Create(Lab2Config());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bakedHoles = rig.Root.BakedHoles;
            Vector2[] holeXZ = bakedHoles.Select(h => new Vector2(h.localPosition.x, h.localPosition.z)).ToArray();

            // Unlatched floor crossings: prev y >= 0, cur y < 0, no escape latch either frame.
            var crossings = new List<(int frame, float distToHole, float r, float y, Vector3 vel, bool latched)>();
            Vector4[] prevPos = null;
            uint[] prevFlags = null;
            int prevLive = 0;
            int stableFrames = 0;

            for (int frame = 0; frame < 300; frame++)
            {
                rig.Step(1, Dt);
                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();
                int live = rig.Root.ActiveParticleCount;

                if (prevPos != null && live == prevLive)
                {
                    stableFrames++;
                    for (int i = 0; i < live; i++)
                    {
                        if (prevPos[i].y < 0f || positions[i].y >= 0f)
                        {
                            continue;
                        }

                        float r = Mathf.Sqrt(positions[i].x * positions[i].x + positions[i].z * positions[i].z);
                        if (r > BucketRadius)
                        {
                            continue; // crossing outside footprint (wall-band spillover downstream)
                        }

                        var xz = new Vector2(positions[i].x, positions[i].z);
                        float dHole = holeXZ.Min(h => Vector2.Distance(xz, h));
                        bool latched = V4ParticleFlags.HasEscaped(flags[i]);
                        crossings.Add((frame, dHole, r, positions[i].y, (Vector3)(Vector4)velocities[i], latched));
                    }
                }

                prevPos = positions;
                prevFlags = flags;
                prevLive = live;
            }

            // Final-frame y histogram of unlatched sub-floor particles.
            Vector4[] finalPos = rig.ReadPositions();
            uint[] finalFlags = rig.ReadFlags();
            var subFloorY = new List<float>();
            for (int i = 0; i < rig.Root.ActiveParticleCount; i++)
            {
                if (!V4ParticleFlags.HasEscaped(finalFlags[i]) && !V4ParticleFlags.IsInside(finalFlags[i]) && finalPos[i].y < 0f)
                {
                    subFloorY.Add(finalPos[i].y);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("V4 Lab2 — floor crossing forensics (300 frames, rest)");
            sb.AppendLine($"holes at d0={bakedHoles[0].d0:F3} d1={bakedHoles[0].d1:F3} d2={bakedHoles[0].d2:F3}");
            sb.AppendLine($"index-stable frames used: {stableFrames}/299");
            sb.AppendLine();

            var unlatched = crossings.Where(c => !c.latched).ToList();
            var latchedC = crossings.Where(c => c.latched).ToList();
            sb.AppendLine($"floor crossings captured: total={crossings.Count} latched={latchedC.Count} UNLATCHED={unlatched.Count}");

            if (unlatched.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== UNLATCHED floor crossings (illegitimate leak) ===");
                sb.AppendLine($"first frame={unlatched.Min(c => c.frame)} last={unlatched.Max(c => c.frame)}");
                var d = unlatched.Select(c => c.distToHole).ToList();
                sb.AppendLine($"XZ dist to nearest hole: mean={d.Average():F4} p5={Percentile(d, 0.05f):F4} p50={Percentile(d, 0.5f):F4} p95={Percentile(d, 0.95f):F4} max={d.Max():F4}");
                sb.AppendLine("dist bins: " +
                    $"<d0(0.015): {d.Count(v => v < 0.015f)}, d0-d1(0.075): {d.Count(v => v >= 0.015f && v < 0.075f)}, " +
                    $"d1-d2(0.135): {d.Count(v => v >= 0.075f && v < 0.135f)}, >d2: {d.Count(v => v >= 0.135f)}");
                sb.AppendLine($"crossing vel.y: mean={unlatched.Average(c => c.vel.y):F3} min={unlatched.Min(c => c.vel.y):F3}");
                sb.AppendLine($"crossing depth y: mean={unlatched.Average(c => c.y):F4} min={unlatched.Min(c => c.y):F4}");
                var rr = unlatched.Select(c => c.r / BucketRadius).ToList();
                sb.AppendLine($"r/R at crossing: mean={rr.Average():F3} p95={Percentile(rr, 0.95f):F3}");
            }

            if (latchedC.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== latched (hole-eject) floor crossings for comparison ===");
                var d = latchedC.Select(c => c.distToHole).ToList();
                sb.AppendLine($"count={latchedC.Count} XZ dist to hole: mean={d.Average():F4} p95={Percentile(d, 0.95f):F4}");
            }

            sb.AppendLine();
            sb.AppendLine($"=== final frame unlatched sub-floor population: {subFloorY.Count} ===");
            if (subFloorY.Count > 0)
            {
                sb.AppendLine("y histogram: " +
                    $"0..-0.05: {subFloorY.Count(y => y > -0.05f)}, -0.05..-0.2: {subFloorY.Count(y => y <= -0.05f && y > -0.2f)}, " +
                    $"-0.2..-0.5: {subFloorY.Count(y => y <= -0.2f && y > -0.5f)}, -0.5..-0.9: {subFloorY.Count(y => y <= -0.5f && y > -0.9f)}, " +
                    $"canvas layer (<-0.9): {subFloorY.Count(y => y <= -0.9f)}");
            }

            sb.AppendLine();
            sb.AppendLine($"EscapedTotal={rig.Root.EscapedTotal} SettledTotal={rig.Root.SettledTotal} live={rig.Root.ActiveParticleCount}");

            WriteReport("lab2_floor_crossing_forensics.txt", sb.ToString());
            Assert.Pass("Floor crossing forensics logged.");
        }

        [Test]
        public void Investigate_Lab2LeakChannelGeometry()
        {
            using var rig = V4TestRig.Create(Lab2Config());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var bakedHoles = rig.Root.BakedHoles;
            Vector2[] holeXZ = bakedHoles.Select(h => new Vector2(h.localPosition.x, h.localPosition.z)).ToArray();
            float d2 = bakedHoles[0].d2;

            var sb = new StringBuilder();
            sb.AppendLine("V4 Lab2 — geometry of the unlatched sub-floor leak (where do they cross?)");
            sb.AppendLine($"R={BucketRadius} outerR={BucketRadius + WallThickness} floorLag(containment gives up below)=-{Mathf.Max(WallThickness * 4f, 0.02f):F2} holeD2={d2:F3}");
            sb.AppendLine();

            int[] snapshotFrames = { 40, 50, 60, 75, 90, 120 };
            int next = 0;

            for (int frame = 0; frame < 130; frame++)
            {
                rig.Step(1, Dt);
                if (next >= snapshotFrames.Length || frame != snapshotFrames[next])
                {
                    continue;
                }

                next++;
                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();
                int live = rig.Root.ActiveParticleCount;

                // Unlatched, not-inside, in the shallow crossing band (still near the floor).
                var shallowR = new List<float>();
                var shallowHoleDist = new List<float>();
                var shallowVelY = new List<float>();
                int deepCount = 0;

                for (int i = 0; i < live; i++)
                {
                    if (V4ParticleFlags.IsInside(flags[i]) || V4ParticleFlags.HasEscaped(flags[i]))
                    {
                        continue;
                    }

                    Vector3 p = positions[i];
                    if (p.y >= 0f)
                    {
                        continue;
                    }

                    if (p.y < -0.3f)
                    {
                        deepCount++;
                        continue;
                    }

                    float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                    shallowR.Add(r);
                    shallowHoleDist.Add(holeXZ.Min(h => Vector2.Distance(new Vector2(p.x, p.z), h)));
                    shallowVelY.Add(velocities[i].y);
                }

                sb.AppendLine($"--- frame {frame}: unlatched subfloor shallow(0..-0.3)={shallowR.Count} deep(<-0.3)={deepCount} escTot={rig.Root.EscapedTotal} setTot={rig.Root.SettledTotal} ---");
                if (shallowR.Count > 0)
                {
                    sb.AppendLine("  r bins: " +
                        $"r<0.15: {shallowR.Count(v => v < 0.15f)}, 0.15-0.25: {shallowR.Count(v => v >= 0.15f && v < 0.25f)}, " +
                        $"0.25-0.3(R): {shallowR.Count(v => v >= 0.25f && v < 0.3f)}, R-outerR: {shallowR.Count(v => v >= 0.3f && v < 0.35f)}, " +
                        $">outerR: {shallowR.Count(v => v >= 0.35f)}");
                    sb.AppendLine("  distToNearestHole bins: " +
                        $"<d2(0.135): {shallowHoleDist.Count(v => v < d2)}, d2-0.2: {shallowHoleDist.Count(v => v >= d2 && v < 0.2f)}, " +
                        $">0.2: {shallowHoleDist.Count(v => v >= 0.2f)}");
                    sb.AppendLine($"  vel.y: mean={shallowVelY.Average():F3} min={shallowVelY.Min():F3} max={shallowVelY.Max():F3}");
                }

                sb.AppendLine();
            }

            WriteReport("lab2_leak_channel_geometry.txt", sb.ToString());
            Assert.Pass("Leak channel geometry logged.");
        }
    }
}
