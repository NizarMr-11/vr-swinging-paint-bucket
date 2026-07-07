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
    /// Identity-stable leak probe for the Lab2 wall/floor leak. Settling and impact
    /// absorption are disabled so no particle is ever removed and indices stay stable
    /// for the whole run, giving exact per-particle crossing forensics (the escape
    /// statistics test loses identity once compaction starts).
    /// </summary>
    public sealed class V4Lab2LeakProbeTests
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 150;

        private const float BucketRadius = 0.3f;
        private const float BucketHeight = 0.6f;
        private const float WallThickness = 0.05f;

        private static readonly (Vector3 pos, float radius, Color color)[] Lab2Zones =
        {
            (new Vector3(-0.1f, 0.15f, 0f), 0.1f, new Color(0.9f, 0.1f, 0.1f)),
            (new Vector3(0.094f, 0.511f, -0.018f), 0.15f, new Color(0f, 0f, 0f)),
            (new Vector3(0.1f, 0.22f, 0f), 0.1f, new Color(0.1f, 0.2f, 0.9f))
        };

        private static V4TestRig.Config ProbeConfig()
        {
            return new V4TestRig.Config
            {
                BucketRadius = BucketRadius,
                BucketHeight = BucketHeight,
                WallThickness = WallThickness,
                TopBandHeight = 0.12f,
                RingSpacing = 0.06f,
                GlobalDensity = 1000000f,
                CanvasY = -1f,
                CanvasSize = 3f,
                RestrictSpawnToBucketCavity = true,
                Holes = new List<V4HoleDef>
                {
                    new V4HoleDef { localPosition = new Vector3(0.1f, 0f, 0f), radius = 0f, outwardNormal = Vector3.down },
                    new V4HoleDef { localPosition = new Vector3(-0.15f, 0f, 0.15f), radius = 0f, outwardNormal = Vector3.down }
                },
                SpawnZones = Lab2Zones.Select(z => (z.pos, z.radius, z.color)).ToList(),
                ConfigureProfile = p =>
                {
                    // No removals: keeps particle indices stable for the whole run.
                    p.settleEpsilon = 0f;
                    p.impactAbsorbSpeed = float.MaxValue;
                },
                ConfigureRoot = root =>
                {
                    root.pbfIterations = 2;
                    root.boundaryGhostWeight = 0f;
                    root.carryRate = 10f;
                    root.pbfEpsilon = 50f;
                }
            };
        }

        private struct Snapshot
        {
            public Vector3 Pos;
            public Vector3 Vel;
            public bool Inside;
            public bool Escaped;
        }

        private struct CrossEvent
        {
            public int Frame;
            public int Index;
            public string Channel;
            public Snapshot Prev2;
            public Snapshot Prev;
            public Snapshot Cur;
        }

        private static float RadiusOf(Vector3 p)
        {
            return Mathf.Sqrt(p.x * p.x + p.z * p.z);
        }

        private static float RadialVel(Vector3 pos, Vector3 vel)
        {
            float r = RadiusOf(pos);
            if (r < 1e-6f)
            {
                return 0f;
            }

            return (pos.x * vel.x + pos.z * vel.z) / r;
        }

        [Test]
        public void Investigate_Lab2FirstCrossingForensics_NoRemovals()
        {
            using var rig = V4TestRig.Create(ProbeConfig());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int spawned = rig.Root.ActiveParticleCount;
            var events = new List<CrossEvent>();
            var crossed = new HashSet<int>();

            Snapshot[] prev = null;
            Snapshot[] prev2 = null;
            int liveMismatchFrames = 0;

            for (int frame = 0; frame < Frames; frame++)
            {
                rig.Step(1, Dt);
                int live = rig.Root.ActiveParticleCount;
                if (live != spawned)
                {
                    liveMismatchFrames++;
                }

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();

                var cur = new Snapshot[live];
                for (int i = 0; i < live; i++)
                {
                    cur[i] = new Snapshot
                    {
                        Pos = positions[i],
                        Vel = velocities[i],
                        Inside = V4ParticleFlags.IsInside(flags[i]),
                        Escaped = V4ParticleFlags.HasEscaped(flags[i])
                    };
                }

                if (prev != null && live == spawned)
                {
                    for (int i = 0; i < live; i++)
                    {
                        if (cur[i].Escaped || crossed.Contains(i))
                        {
                            continue;
                        }

                        Vector3 p = cur[i].Pos;
                        float r = RadiusOf(p);
                        string channel = null;

                        if (p.y < -WallThickness - 0.001f && r <= BucketRadius)
                        {
                            channel = "BelowSlab";
                        }
                        else if (r > BucketRadius + WallThickness - 0.001f && p.y < BucketHeight)
                        {
                            channel = "OuterFaceOrBeyond";
                        }
                        else if (r > BucketRadius + 0.002f && p.y >= 0f && p.y < BucketHeight)
                        {
                            channel = "InWallBand";
                        }
                        else if (r > BucketRadius + 0.002f && p.y < 0f)
                        {
                            channel = "WallBandBelowFloor";
                        }

                        if (channel == null)
                        {
                            continue;
                        }

                        crossed.Add(i);
                        events.Add(new CrossEvent
                        {
                            Frame = frame,
                            Index = i,
                            Channel = channel,
                            Prev2 = prev2 != null ? prev2[i] : default,
                            Prev = prev[i],
                            Cur = cur[i]
                        });
                    }
                }

                prev2 = prev;
                prev = cur;
            }

            // ---- report ----
            var sb = new StringBuilder();
            sb.AppendLine("V4 Lab2 leak probe — identity-stable first crossings (settle disabled)");
            sb.AppendLine($"spawned={spawned} frames={Frames} liveMismatchFrames={liveMismatchFrames} (must be 0)");
            sb.AppendLine($"R={BucketRadius} outerR={BucketRadius + WallThickness} midWall={BucketRadius + WallThickness * 0.5f} slabBottom={-WallThickness}");
            sb.AppendLine();

            sb.AppendLine($"total first-crossings={events.Count}");
            foreach (var g in events.GroupBy(e => e.Channel).OrderByDescending(g => g.Count()))
            {
                sb.AppendLine($"  {g.Key}: {g.Count()}");
            }

            sb.AppendLine();
            sb.AppendLine("crossing frame histogram (10-frame bins):");
            for (int bin = 0; bin * 10 < Frames; bin++)
            {
                int lo = bin * 10;
                int count = events.Count(e => e.Frame >= lo && e.Frame < lo + 10);
                if (count > 0)
                {
                    sb.AppendLine($"  f{lo}-{lo + 10}: {count}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("prev-frame state of crossers:");
            int prevInside = events.Count(e => e.Prev.Inside);
            sb.AppendLine($"  prev flagged Inside: {prevInside}/{events.Count}   prev flagged Outside: {events.Count - prevInside}");
            var prevR = events.Select(e => RadiusOf(e.Prev.Pos)).ToList();
            var prevY = events.Select(e => e.Prev.Pos.y).ToList();
            if (events.Count > 0)
            {
                sb.AppendLine($"  prev r: mean={prevR.Average():F4} min={prevR.Min():F4} max={prevR.Max():F4}");
                sb.AppendLine($"  prev r bins: <R-0.01: {prevR.Count(v => v < BucketRadius - 0.01f)}, R-0.01..R: {prevR.Count(v => v >= BucketRadius - 0.01f && v <= BucketRadius)}, R..midWall: {prevR.Count(v => v > BucketRadius && v <= BucketRadius + 0.025f)}, >midWall: {prevR.Count(v => v > BucketRadius + 0.025f)}");
                sb.AppendLine($"  prev y: mean={prevY.Average():F4} min={prevY.Min():F4} max={prevY.Max():F4}");
                sb.AppendLine($"  prev y bins: y<-0.025: {prevY.Count(v => v < -0.025f)}, -0.025..0: {prevY.Count(v => v >= -0.025f && v < 0f)}, 0..0.02: {prevY.Count(v => v >= 0f && v < 0.02f)}, >0.02: {prevY.Count(v => v >= 0.02f)}");
                var prevVr = events.Select(e => RadialVel(e.Prev.Pos, e.Prev.Vel)).ToList();
                sb.AppendLine($"  prev radial vel: mean={prevVr.Average():F3} p95={prevVr.OrderBy(v => v).ElementAt((int)(prevVr.Count * 0.95f)):F3} max={prevVr.Max():F3}");
                var prevVy = events.Select(e => e.Prev.Vel.y).ToList();
                sb.AppendLine($"  prev vel.y: mean={prevVy.Average():F3} min={prevVy.Min():F3}");
                var hop = events.Select(e => Vector3.Distance(e.Prev.Pos, e.Cur.Pos)).ToList();
                sb.AppendLine($"  hop distance prev->cur: mean={hop.Average():F4} max={hop.Max():F4}");
            }

            sb.AppendLine();
            sb.AppendLine("=== first 25 crossing events (detail) ===");
            foreach (var e in events.Take(25))
            {
                sb.AppendLine($"f{e.Frame} idx={e.Index} ch={e.Channel}");
                if (e.Prev2.Pos != default(Vector3) || e.Frame >= 2)
                {
                    sb.AppendLine($"  prev2 in={(e.Prev2.Inside ? 1 : 0)} r={RadiusOf(e.Prev2.Pos):F4} y={e.Prev2.Pos.y:F4} vr={RadialVel(e.Prev2.Pos, e.Prev2.Vel):F3} vy={e.Prev2.Vel.y:F3}");
                }

                sb.AppendLine($"  prev  in={(e.Prev.Inside ? 1 : 0)} r={RadiusOf(e.Prev.Pos):F4} y={e.Prev.Pos.y:F4} vr={RadialVel(e.Prev.Pos, e.Prev.Vel):F3} vy={e.Prev.Vel.y:F3} |v|={e.Prev.Vel.magnitude:F3}");
                sb.AppendLine($"  cur   in={(e.Cur.Inside ? 1 : 0)} r={RadiusOf(e.Cur.Pos):F4} y={e.Cur.Pos.y:F4} vr={RadialVel(e.Cur.Pos, e.Cur.Vel):F3} vy={e.Cur.Vel.y:F3}");
            }

            string dir = Path.Combine(Application.dataPath, "HarmonicEngineV4/Tests/PlayMode/Results");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "lab2_leak_probe_first_crossings.txt"), sb.ToString());
            foreach (string line in sb.ToString().Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Debug.Log(line.TrimEnd('\r'));
                }
            }

            Assert.Pass("Leak probe logged.");
        }
    }
}
