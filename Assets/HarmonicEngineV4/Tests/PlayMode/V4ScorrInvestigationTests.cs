using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Measures s_corr magnitude / neighbor-distance distributions and sweeps kCorr and
    /// experimental scorr caps. Reports only.
    /// </summary>
    public sealed class V4ScorrInvestigationTests
    {
        private const float Dt = 1f / 60f;
        private const int SettleFrames = 320;
        private const int EarlyFrames = 40;
        private const int TallLayers = 20;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;
        private const float KCorrDefault = 0.1f;
        private const float DeltaQScale = 0.2f;
        private const float NCorr = 4f;

        private struct PairStats
        {
            public int PairCount;
            public float MinAbsScorr;
            public float MaxAbsScorr;
            public float MeanAbsScorr;
            public float MinR;
            public float MaxR;
            public float MeanR;
            public int BelowDeltaQ;
            public float BelowDeltaQFraction;
            public float RatioAboveOneFraction;
        }

        private struct FrameScorrSample
        {
            public int Frame;
            public int AboveRim;
            public int Settled;
            public PairStats Floor;
            public PairStats Mid;
        }

        private struct RunResult
        {
            public string Label;
            public int Settled;
            public int Live;
            public int Escaped;
            public float BottomRhoRatio;
        }

        private struct ScorrDebugConfig
        {
            public float KCorr;
            public float RatioMax;
            public bool SaturatePow;
            public int ApplyMode;
        }

        private static V4TestRig.Config TallConfig(float kCorr)
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
                ConfigureProfile = profile => profile.pbfKCorr = kCorr
            };
        }

        private static void ApplyScorrDebug(V4PipelineRoot root, ScorrDebugConfig cfg)
        {
            root.DebugDisableBoundaryGhosts = false;
            root.DebugScorrRatioMax = cfg.RatioMax;
            root.DebugScorrSaturatePow = cfg.SaturatePow;
            root.DebugScorrApplyMode = cfg.ApplyMode;
            root.pbfIterations = 8;
        }

        private static float Poly6(float r, float h)
        {
            if (r > h)
            {
                return 0f;
            }

            float h2 = h * h;
            float coeff = 315f / (64f * Mathf.PI * Mathf.Pow(h, 9f));
            float d = h2 - r * r;
            return d * d * d * coeff;
        }

        private static float AbsScorr(float r, float h, float kCorr)
        {
            float wCorrDenom = Poly6(DeltaQScale * h, h);
            float wCorrNum = Poly6(r, h);
            float wRatio = Mathf.Max(wCorrNum / Mathf.Max(wCorrDenom, 1e-6f), 0f);
            return Mathf.Abs(-kCorr * Mathf.Pow(wRatio, NCorr));
        }

        private static PairStats AnalyzeFloorScorr(
            Vector3[] positions, bool[] floorMask, float h, float kCorr, float deltaQ)
        {
            var grid = BuildGrid(positions, h);
            var absScorrs = new List<float>();
            var distances = new List<float>();
            int belowDeltaQ = 0;
            int ratioAboveOne = 0;
            float wCorrDenom = Poly6(DeltaQScale * h, h);

            for (int i = 0; i < positions.Length; i++)
            {
                if (!floorMask[i])
                {
                    continue;
                }

                foreach (int j in GridNeighbors(grid, positions, i, h))
                {
                    float r = Vector3.Distance(positions[i], positions[j]);
                    float wCorrNum = Poly6(r, h);
                    float wRatio = Mathf.Max(wCorrNum / Mathf.Max(wCorrDenom, 1e-6f), 0f);
                    absScorrs.Add(Mathf.Abs(-kCorr * Mathf.Pow(wRatio, NCorr)));
                    distances.Add(r);
                    if (r < deltaQ)
                    {
                        belowDeltaQ++;
                    }

                    if (wRatio > 1f)
                    {
                        ratioAboveOne++;
                    }
                }
            }

            if (absScorrs.Count == 0)
            {
                return default;
            }

            return new PairStats
            {
                PairCount = absScorrs.Count,
                MinAbsScorr = absScorrs.Min(),
                MaxAbsScorr = absScorrs.Max(),
                MeanAbsScorr = absScorrs.Sum() / absScorrs.Count,
                MinR = distances.Min(),
                MaxR = distances.Max(),
                MeanR = distances.Sum() / distances.Count,
                BelowDeltaQ = belowDeltaQ,
                BelowDeltaQFraction = (float)belowDeltaQ / distances.Count,
                RatioAboveOneFraction = (float)ratioAboveOne / distances.Count
            };
        }

        private static PairStats AnalyzeMidScorr(Vector3[] positions, bool[] midMask, float h, float kCorr, float deltaQ)
        {
            var grid = BuildGrid(positions, h);
            var absScorrs = new List<float>();
            var distances = new List<float>();
            float wCorrDenom = Poly6(DeltaQScale * h, h);

            for (int i = 0; i < positions.Length; i++)
            {
                if (!midMask[i])
                {
                    continue;
                }

                foreach (int j in GridNeighbors(grid, positions, i, h))
                {
                    float r = Vector3.Distance(positions[i], positions[j]);
                    float wCorrNum = Poly6(r, h);
                    float wRatio = Mathf.Max(wCorrNum / Mathf.Max(wCorrDenom, 1e-6f), 0f);
                    absScorrs.Add(Mathf.Abs(-kCorr * Mathf.Pow(wRatio, NCorr)));
                    distances.Add(r);
                }
            }

            if (absScorrs.Count == 0)
            {
                return default;
            }

            return new PairStats
            {
                PairCount = absScorrs.Count,
                MinAbsScorr = absScorrs.Min(),
                MaxAbsScorr = absScorrs.Max(),
                MeanAbsScorr = absScorrs.Sum() / absScorrs.Count,
                MinR = distances.Min(),
                MaxR = distances.Max(),
                MeanR = distances.Sum() / distances.Count
            };
        }

        private static FrameScorrSample SampleFrame(V4TestRig rig, int frame, float h, float kCorr, float deltaQ, float spacing)
        {
            Vector4[] pos4 = rig.ReadPositions();
            var positions = new Vector3[pos4.Length];
            var floorMask = new bool[pos4.Length];
            var midMask = new bool[pos4.Length];
            float floorBand = rig.Root.ParticleRadius * 2f;
            float midMin = 7f * spacing;
            float midMax = 13f * spacing;

            int aboveRim = 0;
            for (int i = 0; i < pos4.Length; i++)
            {
                positions[i] = new Vector3(pos4[i].x, pos4[i].y, pos4[i].z);
                Vector3 local = rig.Bucket.transform.InverseTransformPoint(positions[i]);
                if (local.y > rig.Bucket.height)
                {
                    aboveRim++;
                }

                floorMask[i] = local.y < floorBand;
                midMask[i] = local.y >= midMin && local.y <= midMax;
            }

            PairStats floor = AnalyzeFloorScorr(positions, floorMask, h, kCorr, deltaQ);
            PairStats mid = AnalyzeMidScorr(positions, midMask, h, kCorr, deltaQ);

            return new FrameScorrSample
            {
                Frame = frame,
                AboveRim = aboveRim,
                Settled = rig.Root.SettledTotal,
                Floor = floor,
                Mid = mid
            };
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
                if (local.y < 0f || local.y > rig.Bucket.height || Mathf.FloorToInt(local.y / layerBand) != 0)
                {
                    continue;
                }

                sum += densities[i];
                count++;
            }

            return count > 0 ? (sum / count) / restDensity : 0f;
        }

        private static RunResult RunCase(string label, ScorrDebugConfig cfg, int frames)
        {
            using var rig = V4TestRig.Create(TallConfig(cfg.KCorr));
            ApplyScorrDebug(rig.Root, cfg);
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.Step(frames, Dt);
            return new RunResult
            {
                Label = label,
                Settled = rig.Root.SettledTotal,
                Live = rig.Root.ActiveParticleCount,
                Escaped = rig.Root.EscapedTotal,
                BottomRhoRatio = BottomLayerRatio(rig)
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

        private static string FormatPairStats(string name, PairStats s, float deltaQ)
        {
            return
                $"{name}: pairs={s.PairCount} |s_corr| min={s.MinAbsScorr:E3} max={s.MaxAbsScorr:E3} mean={s.MeanAbsScorr:E3} " +
                $"r min={s.MinR:F5} max={s.MaxR:F5} mean={s.MeanR:F5} deltaQ={deltaQ:F5} " +
                $"frac(r<deltaQ)={s.BelowDeltaQFraction:P1} frac(wRatio>1)={s.RatioAboveOneFraction:P1}";
        }

        private struct GridReadback
        {
            public uint[] KeyValuePairs;
            public int[] CellRanges;
            public int GridResolution;
        }

        private static GridReadback BuildGrid(Vector3[] positions, float cellSize)
        {
            ComputeShader hashShader = V4ShaderLibrary.Load(V4ShaderLibrary.SpatialHash);
            ComputeShader radixShader = V4ShaderLibrary.Load(V4ShaderLibrary.RadixSort);
            int n = positions.Length;
            var grid = new V4SpatialHashGrid(hashShader, radixShader, n);
            var block0 = new ComputeBuffer(n, sizeof(float) * 4);
            try
            {
                var data = new Vector4[n];
                for (int i = 0; i < n; i++)
                {
                    data[i] = new Vector4(positions[i].x, positions[i].y, positions[i].z, 0.05f);
                }

                block0.SetData(data);
                grid.Build(block0, n, cellSize);
                var pairs = new uint[grid.PaddedGridSize * 2];
                grid.GridKeyValueBuffer.GetData(pairs);
                var ranges = new int[grid.PaddedGridSize * 2];
                grid.CellStartEndBuffer.GetData(ranges);
                return new GridReadback
                {
                    KeyValuePairs = pairs,
                    CellRanges = ranges,
                    GridResolution = grid.GridResolution
                };
            }
            finally
            {
                grid.Dispose();
                block0.Release();
            }
        }

        private static HashSet<int> GridNeighbors(GridReadback grid, Vector3[] positions, int queryIndex, float radius)
        {
            var result = new HashSet<int>();
            float cellSize = radius;
            Vector3Int baseCell = V4SpatialHashMath.CellFromPosition(positions[queryIndex], cellSize);
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                uint hash = V4SpatialHashMath.HashCell(baseCell + new Vector3Int(dx, dy, dz), (uint)grid.GridResolution);
                int start = grid.CellRanges[hash * 2];
                int end = grid.CellRanges[hash * 2 + 1];
                if (start < 0)
                {
                    continue;
                }

                for (int s = start; s <= end; s++)
                {
                    uint particleIndex = grid.KeyValuePairs[s * 2 + 1];
                    if (particleIndex == 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    int pi = (int)particleIndex;
                    if (pi == queryIndex)
                    {
                        continue;
                    }

                    if (Vector3.Distance(positions[queryIndex], positions[pi]) <= radius)
                    {
                        result.Add(pi);
                    }
                }
            }

            return result;
        }

        [Test]
        public void Investigate_ScorrDistribution_EarlyFrames()
        {
            using var rig = V4TestRig.Create(TallConfig(KCorrDefault));
            ApplyScorrDebug(rig.Root, new ScorrDebugConfig { KCorr = KCorrDefault });
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float h = rig.Root.SmoothingRadius;
            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float deltaQ = DeltaQScale * h;
            int[] sampleFrames = { 0, 5, 10, 20, 30, 39 };
            var samples = new List<FrameScorrSample>();

            for (int frame = 0; frame < EarlyFrames; frame++)
            {
                rig.Step(1, Dt);
                if (System.Array.IndexOf(sampleFrames, frame) >= 0)
                {
                    samples.Add(SampleFrame(rig, frame, h, KCorrDefault, deltaQ, spacing));
                }
            }

            var report = new StringBuilder();
            report.AppendLine("V4 s_corr investigation — early-frame distribution (ghost ON, kCorr=0.1, iter=8)");
            report.AppendLine($"deltaQ={deltaQ:F5} h={h:F5} spacing={spacing:F5}");
            report.AppendLine("CPU replay from post-step positions (approximates SolveDelta neighbor geometry)");
            foreach (FrameScorrSample s in samples)
            {
                report.AppendLine($"--- frame {s.Frame} aboveRim={s.AboveRim} settled={s.Settled} ---");
                report.AppendLine(FormatPairStats("floor pairs", s.Floor, deltaQ));
                report.AppendLine(FormatPairStats("mid pairs  ", s.Mid, deltaQ));
                report.AppendLine(
                    $"floor/mid mean |s_corr| ratio={s.Floor.MeanAbsScorr / Mathf.Max(s.Mid.MeanAbsScorr, 1e-12f):F1}x");
            }

            WriteReport("scorr_distribution_early.txt", report.ToString());
            Assert.Pass("Early-frame s_corr distribution logged.");
        }

        [Test]
        public void Investigate_KCorrSweep()
        {
            float[] kValues = { 0.1f, 0.03f, 0.01f, 0.003f, 0.001f };
            var report = new StringBuilder();
            report.AppendLine("V4 s_corr investigation — kCorr sweep (ghost ON, iter=8, 320 frames)");
            foreach (float k in kValues)
            {
                RunResult r = RunCase($"kCorr={k}", new ScorrDebugConfig { KCorr = k }, SettleFrames);
                report.AppendLine(
                    $"{r.Label}: settled={r.Settled} live={r.Live} escaped={r.Escaped} bottomRhoRatio={r.BottomRhoRatio:F4}");
            }

            WriteReport("scorr_kcorr_sweep.txt", report.ToString());
            Assert.Pass("kCorr sweep logged.");
        }

        [Test]
        public void Investigate_ScorrCapAndIterationLevers()
        {
            var cases = new List<(string label, ScorrDebugConfig cfg)>
            {
                ("baseline k=0.1", new ScorrDebugConfig { KCorr = KCorrDefault }),
                ("ratioMax=3", new ScorrDebugConfig { KCorr = KCorrDefault, RatioMax = 3f }),
                ("ratioMax=2", new ScorrDebugConfig { KCorr = KCorrDefault, RatioMax = 2f }),
                ("saturate(pow)", new ScorrDebugConfig { KCorr = KCorrDefault, SaturatePow = true }),
                ("scorr/iterCount", new ScorrDebugConfig { KCorr = KCorrDefault, ApplyMode = 1 }),
                ("finalIterOnly", new ScorrDebugConfig { KCorr = KCorrDefault, ApplyMode = 2 })
            };

            var report = new StringBuilder();
            report.AppendLine("V4 s_corr investigation — cap / iteration levers (kCorr=0.1, ghost ON, iter=8, 320 frames)");
            foreach ((string label, ScorrDebugConfig cfg) in cases)
            {
                RunResult r = RunCase(label, cfg, SettleFrames);
                report.AppendLine(
                    $"{r.Label}: settled={r.Settled} live={r.Live} bottomRhoRatio={r.BottomRhoRatio:F4}");
            }

            WriteReport("scorr_cap_iteration_levers.txt", report.ToString());
            Assert.Pass("Cap/iteration lever sweep logged.");
        }
    }
}
