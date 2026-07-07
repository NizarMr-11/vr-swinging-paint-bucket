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
    /// Measures ApplyDelta maxCorrection clamp hits on floor particles. Reports only.
    /// </summary>
    public sealed class V4ApplyDeltaClampInvestigationTests
    {
        private const float Dt = 1f / 60f;
        private const int Frames = 120;
        private const int TallLayers = 20;
        private const int PbfIterations = 8;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;

        private sealed class ClampSession
        {
            public readonly List<FrameClampRecord> Frames = new List<FrameClampRecord>();
            public readonly Dictionary<int, ParticleHistory> ParticleHistories = new Dictionary<int, ParticleHistory>();
            public readonly List<RimLaunchRecord> RimLaunches = new List<RimLaunchRecord>();
            public int[] TrackedIndices;
        }

        private struct IterClampStats
        {
            public int FloorCount;
            public int ClampHits;
            public float Fraction => FloorCount > 0 ? (float)ClampHits / FloorCount : 0f;
        }

        private struct FrameClampRecord
        {
            public int Frame;
            public int AboveRim;
            public int Settled;
            public IterClampStats[] PerIter;
        }

        private struct ParticleSample
        {
            public int Frame;
            public float Density;
            public float VelY;
            public float CumulativeDeltaY;
            public int ClampHitIters;
            public float LocalY;
        }

        private sealed class ParticleHistory
        {
            public readonly List<ParticleSample> Samples = new List<ParticleSample>();
            public readonly bool[][] ClampPerIterByFrame = new bool[Frames][];
            public float CumulativeDeltaY;
        }

        private struct RimLaunchRecord
        {
            public int Frame;
            public int ParticleIndex;
            public float VelY;
            public int PriorFrameClampIters;
            public float PriorFrameVelY;
            public float PriorFrameLocalY;
        }

        private static V4TestRig.Config TallConfig()
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
                ConfigureRoot = root =>
                {
                    root.pbfIterations = PbfIterations;
                    root.boundaryGhostWeight = 0f;
                    root.DebugDisableBoundaryGhosts = false;
                    root.DebugScorrApplyMode = 0;
                    root.DebugMaxCorrectionScale = 0f;
                }
            };
        }

        private static bool IsFloorParticle(V4TestRig rig, Vector3 worldPos, float floorBand)
        {
            Vector3 local = rig.Bucket.transform.InverseTransformPoint(worldPos);
            return local.y < floorBand && local.y >= 0f;
        }

        private static ClampSession RunInstrumentedSession(V4TestRig rig, int frames, bool lateralShake)
        {
            var session = new ClampSession();
            float floorBand = rig.Root.ParticleRadius * 2f;
            float maxCorrection = rig.Root.MaxCorrectionDistance();
            float bucketHeight = rig.Bucket.height;
            int iterCount = rig.Root.pbfIterations;

            var currentFrameIter = new IterClampStats[iterCount];
            var currentFrameClampByParticle = new Dictionary<int, bool[]>();
            var previousAboveRim = new HashSet<int>();
            var previousLocalY = new Dictionary<int, float>();
            var previousVelY = new Dictionary<int, float>();
            var previousClampIters = new Dictionary<int, int>();

            rig.Root.DebugBeforeApplyDelta = (iter, deltas, predicted) =>
            {
                if (iter >= currentFrameIter.Length)
                {
                    return;
                }

                for (int i = 0; i < rig.Root.ActiveParticleCount; i++)
                {
                    Vector3 world = predicted[i];
                    if (!IsFloorParticle(rig, world, floorBand))
                    {
                        continue;
                    }

                    currentFrameIter[iter].FloorCount++;
                    float deltaLen = deltas[i].magnitude;
                    bool clamped = deltaLen > maxCorrection + 1e-8f;
                    if (clamped)
                    {
                        currentFrameIter[iter].ClampHits++;
                    }

                    if (!currentFrameClampByParticle.TryGetValue(i, out bool[] perIter))
                    {
                        perIter = new bool[iterCount];
                        currentFrameClampByParticle[i] = perIter;
                    }

                    perIter[iter] = clamped;
                }
            };

            float time = 0f;
            for (int frame = 0; frame < frames; frame++)
            {
                if (lateralShake)
                {
                    time += Dt;
                    rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * 4f) * 0.25f, 0f, 0f);
                }

                for (int i = 0; i < iterCount; i++)
                {
                    currentFrameIter[i] = default;
                }

                currentFrameClampByParticle.Clear();
                rig.Step(1, Dt);

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                float[] densities = rig.ReadDensities();
                var aboveRimNow = new HashSet<int>();

                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                        new Vector3(positions[i].x, positions[i].y, positions[i].z));
                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                        if (!previousAboveRim.Contains(i))
                        {
                            previousLocalY.TryGetValue(i, out float priorY);
                            previousVelY.TryGetValue(i, out float priorVelY);
                            previousClampIters.TryGetValue(i, out int priorClamps);
                            session.RimLaunches.Add(new RimLaunchRecord
                            {
                                Frame = frame,
                                ParticleIndex = i,
                                VelY = velocities[i].y,
                                PriorFrameClampIters = priorClamps,
                                PriorFrameVelY = priorVelY,
                                PriorFrameLocalY = priorY
                            });
                        }
                    }

                    previousLocalY[i] = local.y;
                    previousVelY[i] = velocities[i].y;
                    int clampCount = 0;
                    if (currentFrameClampByParticle.TryGetValue(i, out bool[] perIter))
                    {
                        for (int it = 0; it < iterCount; it++)
                        {
                            if (perIter[it])
                            {
                                clampCount++;
                            }
                        }
                    }

                    previousClampIters[i] = clampCount;

                    if (session.TrackedIndices != null && session.ParticleHistories.ContainsKey(i))
                    {
                        ParticleHistory hist = session.ParticleHistories[i];
                        hist.ClampPerIterByFrame[frame] = currentFrameClampByParticle.TryGetValue(i, out bool[] ci)
                            ? (bool[])ci.Clone()
                            : new bool[iterCount];
                        if (frame > 0 && previousLocalY.ContainsKey(i))
                        {
                            hist.CumulativeDeltaY += local.y - previousLocalY[i];
                        }

                        hist.Samples.Add(new ParticleSample
                        {
                            Frame = frame,
                            Density = densities[i],
                            VelY = velocities[i].y,
                            CumulativeDeltaY = hist.CumulativeDeltaY,
                            ClampHitIters = clampCount,
                            LocalY = local.y
                        });
                    }
                }

                session.Frames.Add(new FrameClampRecord
                {
                    Frame = frame,
                    AboveRim = aboveRimNow.Count,
                    Settled = rig.Root.SettledTotal,
                    PerIter = (IterClampStats[])currentFrameIter.Clone()
                });

                previousAboveRim = aboveRimNow;
            }

            rig.Root.DebugBeforeApplyDelta = null;
            return session;
        }

        private static int[] PickTrackedFloorParticles(V4TestRig rig, int count)
        {
            Vector4[] positions = rig.ReadPositions();
            float floorBand = rig.Root.ParticleRadius * 2f;
            var candidates = new List<(int index, float r2)>();
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                    new Vector3(positions[i].x, positions[i].y, positions[i].z));
                if (local.y >= floorBand)
                {
                    continue;
                }

                float r2 = local.x * local.x + local.z * local.z;
                candidates.Add((i, r2));
            }

            return candidates.OrderBy(c => c.r2).Take(count).Select(c => c.index).ToArray();
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

        private static string FormatClampFrequencyReport(string title, ClampSession session, int sampleEvery)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("frame  settled  aboveRim  clampFrac(iter0..n)");
            foreach (FrameClampRecord f in session.Frames)
            {
                if (f.Frame % sampleEvery != 0 && f.Frame != session.Frames.Count - 1)
                {
                    continue;
                }

                var frac = string.Join(" ", f.PerIter.Select(p => $"{p.Fraction:P0}"));
                sb.AppendLine($"{f.Frame,5}  {f.Settled,7}  {f.AboveRim,8}  {frac}");
            }

            float meanFrac = 0f;
            int samples = 0;
            foreach (FrameClampRecord f in session.Frames)
            {
                foreach (IterClampStats s in f.PerIter)
                {
                    if (s.FloorCount > 0)
                    {
                        meanFrac += s.Fraction;
                        samples++;
                    }
                }
            }

            sb.AppendLine($"mean floor clamp fraction (all iters/frames): {(samples > 0 ? meanFrac / samples : 0f):P1}");
            return sb.ToString();
        }

        [Test]
        public void Investigate_ClampFrequency_RestAndShake()
        {
            using var rigRest = V4TestRig.Create(TallConfig());
            rigRest.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ClampSession rest = RunInstrumentedSession(rigRest, Frames, false);

            using var rigShake = V4TestRig.Create(TallConfig());
            rigShake.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ClampSession shake = RunInstrumentedSession(rigShake, Frames, true);

            var report = new StringBuilder();
            report.Append(FormatClampFrequencyReport(
                "=== REST tall column (iter=8, ghostW=0, final-iter s_corr) ===", rest, 10));
            report.Append(FormatClampFrequencyReport(
                "=== LATERAL SHAKE tall column ===", shake, 10));
            report.AppendLine($"rest final: settled={rest.Frames[^1].Settled} aboveRim={rest.Frames[^1].AboveRim}");
            report.AppendLine($"shake final: settled={shake.Frames[^1].Settled} aboveRim={shake.Frames[^1].AboveRim}");

            WriteReport("clamp_frequency_rest_shake.txt", report.ToString());
            Assert.Pass("Clamp frequency report logged.");
        }

        [Test]
        public void Investigate_TrackedFloorParticles_AndRimLaunches()
        {
            using var rig = V4TestRig.Create(TallConfig());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var session = new ClampSession();
            int[] tracked = PickTrackedFloorParticles(rig, 10);
            session.TrackedIndices = tracked;
            foreach (int idx in tracked)
            {
                session.ParticleHistories[idx] = new ParticleHistory();
            }

            float floorBand = rig.Root.ParticleRadius * 2f;
            float maxCorrection = rig.Root.MaxCorrectionDistance();
            int iterCount = rig.Root.pbfIterations;
            var currentFrameIter = new IterClampStats[iterCount];
            var currentFrameClampByParticle = new Dictionary<int, bool[]>();
            var previousAboveRim = new HashSet<int>();
            var previousLocalY = new Dictionary<int, float>();
            var previousVelY = new Dictionary<int, float>();
            var previousClampIters = new Dictionary<int, int>();
            float bucketHeight = rig.Bucket.height;

            rig.Root.DebugBeforeApplyDelta = (iter, deltas, predicted) =>
            {
                for (int i = 0; i < rig.Root.ActiveParticleCount; i++)
                {
                    if (!IsFloorParticle(rig, predicted[i], floorBand))
                    {
                        continue;
                    }

                    currentFrameIter[iter].FloorCount++;
                    bool clamped = deltas[i].magnitude > maxCorrection + 1e-8f;
                    if (clamped)
                    {
                        currentFrameIter[iter].ClampHits++;
                    }

                    if (!currentFrameClampByParticle.TryGetValue(i, out bool[] perIter))
                    {
                        perIter = new bool[iterCount];
                        currentFrameClampByParticle[i] = perIter;
                    }

                    perIter[iter] = clamped;
                }
            };

            for (int frame = 0; frame < Frames; frame++)
            {
                for (int i = 0; i < iterCount; i++)
                {
                    currentFrameIter[i] = default;
                }

                currentFrameClampByParticle.Clear();
                rig.Step(1, Dt);

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                float[] densities = rig.ReadDensities();
                var aboveRimNow = new HashSet<int>();

                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(
                        new Vector3(positions[i].x, positions[i].y, positions[i].z));
                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                        if (!previousAboveRim.Contains(i))
                        {
                            previousLocalY.TryGetValue(i, out float priorY);
                            previousVelY.TryGetValue(i, out float priorVelY);
                            previousClampIters.TryGetValue(i, out int priorClamps);
                            session.RimLaunches.Add(new RimLaunchRecord
                            {
                                Frame = frame,
                                ParticleIndex = i,
                                VelY = velocities[i].y,
                                PriorFrameClampIters = priorClamps,
                                PriorFrameVelY = priorVelY,
                                PriorFrameLocalY = priorY
                            });
                        }
                    }

                    previousLocalY[i] = local.y;
                    previousVelY[i] = velocities[i].y;
                    int clampCount = 0;
                    if (currentFrameClampByParticle.TryGetValue(i, out bool[] perIter))
                    {
                        for (int it = 0; it < iterCount; it++)
                        {
                            if (perIter[it])
                            {
                                clampCount++;
                            }
                        }
                    }

                    previousClampIters[i] = clampCount;

                    if (session.ParticleHistories.TryGetValue(i, out ParticleHistory hist))
                    {
                        hist.ClampPerIterByFrame[frame] = currentFrameClampByParticle.TryGetValue(i, out bool[] ci)
                            ? (bool[])ci.Clone()
                            : new bool[iterCount];
                        if (frame > 0)
                        {
                            hist.CumulativeDeltaY += local.y - previousLocalY[i];
                        }

                        hist.Samples.Add(new ParticleSample
                        {
                            Frame = frame,
                            Density = densities[i],
                            VelY = velocities[i].y,
                            CumulativeDeltaY = hist.CumulativeDeltaY,
                            ClampHitIters = clampCount,
                            LocalY = local.y
                        });
                    }
                }

                session.Frames.Add(new FrameClampRecord
                {
                    Frame = frame,
                    AboveRim = aboveRimNow.Count,
                    Settled = rig.Root.SettledTotal,
                    PerIter = (IterClampStats[])currentFrameIter.Clone()
                });
                previousAboveRim = aboveRimNow;
            }

            rig.Root.DebugBeforeApplyDelta = null;

            var report = new StringBuilder();
            report.AppendLine("V4 ApplyDelta clamp — tracked floor particles (rest, 120 frames)");
            report.AppendLine($"tracked indices: {string.Join(", ", tracked)}");
            foreach (int idx in tracked)
            {
                ParticleHistory hist = session.ParticleHistories[idx];
                report.AppendLine($"--- particle {idx} ---");
                report.AppendLine("frame  localY   density   velY    clampIters  cumDeltaY");
                foreach (ParticleSample s in hist.Samples.Where(s => s.Frame % 15 == 0 || s.Frame == Frames - 1))
                {
                    report.AppendLine(
                        $"{s.Frame,5}  {s.LocalY,6:F4}  {s.Density,8:F0}  {s.VelY,7:F3}  {s.ClampHitIters,10}  {s.CumulativeDeltaY,9:F4}");
                }
            }

            report.AppendLine("--- rim launch events (first above-rim crossing) ---");
            report.AppendLine($"count={session.RimLaunches.Count}");
            foreach (RimLaunchRecord r in session.RimLaunches.Take(20))
            {
                report.AppendLine(
                    $"frame={r.Frame} idx={r.ParticleIndex} velY={r.VelY:F3} priorVelY={r.PriorFrameVelY:F3} priorLocalY={r.PriorFrameLocalY:F4} priorClampIters={r.PriorFrameClampIters}");
            }

            WriteReport("clamp_tracked_particles_rim.txt", report.ToString());
            Assert.Pass("Tracked particle report logged.");
        }

        [Test]
        public void Investigate_MaxCorrectionSweep()
        {
            float[] scales = { 0.2f, 0.4f, 0.6f, -1f };
            string[] labels = { "0.2h (default)", "0.4h", "0.6h", "uncapped" };
            var report = new StringBuilder();
            report.AppendLine("V4 ApplyDelta maxCorrection sweep — rest tall column, 120 frames");

            for (int i = 0; i < scales.Length; i++)
            {
                using var rig = V4TestRig.Create(TallConfig());
                rig.Root.DebugMaxCorrectionScale = scales[i];
                rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                ClampSession session = RunInstrumentedSession(rig, Frames, false);

                float meanClamp = 0f;
                int n = 0;
                foreach (FrameClampRecord f in session.Frames)
                {
                    foreach (IterClampStats s in f.PerIter)
                    {
                        if (s.FloorCount > 0)
                        {
                            meanClamp += s.Fraction;
                            n++;
                        }
                    }
                }

                FrameClampRecord last = session.Frames[^1];
                report.AppendLine(
                    $"{labels[i]}: settled={last.Settled} aboveRim={last.AboveRim} meanClampFrac={((n > 0 ? meanClamp / n : 0f)):P1} rimLaunches={session.RimLaunches.Count}");
            }

            WriteReport("clamp_maxcorrection_sweep.txt", report.ToString());
            Assert.Pass("MaxCorrection sweep logged.");
        }
    }
}
