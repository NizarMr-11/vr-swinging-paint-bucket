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
    /// Sweeps boundaryGhostWeight after final-iteration-only s_corr fix. Reports only
    /// until a shipping constant is chosen from the sweep table.
    /// </summary>
    public sealed class V4GhostWeightSweepTests
    {
        private const float Dt = 1f / 60f;
        private const int SettleFrames = 320;
        private const int TallLayers = 20;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;
        private const int PbfIterations = 8;

        private struct SweepRow
        {
            public float GhostWeight;
            public int Settled;
            public int Live;
            public float BottomRhoRatio;
            public float MidRhoRatio;
            public float TopRhoRatio;
        }

        private static V4TestRig.Config TallConfig(float ghostWeight)
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
                    root.boundaryGhostWeight = ghostWeight;
                    root.DebugDisableBoundaryGhosts = false;
                    root.DebugScorrApplyMode = 0;
                }
            };
        }

        private static SweepRow RunSweep(float ghostWeight)
        {
            using var rig = V4TestRig.Create(TallConfig(ghostWeight));
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.Step(SettleFrames, Dt);

            float restDensity = rig.Root.GpuRestDensity();
            float layerBand = rig.Root.ParticleRadius * 2f;
            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float midMin = 7f * spacing;
            float midMax = 13f * spacing;

            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();
            float[] densities = rig.ReadDensities();

            var bottom = new List<float>();
            var mid = new List<float>();
            var top = new List<float>();
            int topLayer = -1;

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
                if (layer > topLayer)
                {
                    topLayer = layer;
                }

                float rhoRatio = densities[i] / restDensity;
                if (layer == 0)
                {
                    bottom.Add(rhoRatio);
                }

                if (local.y >= midMin && local.y <= midMax)
                {
                    mid.Add(rhoRatio);
                }
            }

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

                if (Mathf.FloorToInt(local.y / layerBand) == topLayer)
                {
                    top.Add(densities[i] / restDensity);
                }
            }

            return new SweepRow
            {
                GhostWeight = ghostWeight,
                Settled = rig.Root.SettledTotal,
                Live = rig.Root.ActiveParticleCount,
                BottomRhoRatio = bottom.Count > 0 ? bottom.Average() : 0f,
                MidRhoRatio = mid.Count > 0 ? mid.Average() : 0f,
                TopRhoRatio = top.Count > 0 ? top.Average() : 0f
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
        public void FinalIterScorr_SealedTallColumn_SettledStaysNearZero()
        {
            using var rig = V4TestRig.Create(TallConfig(1f));
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.Step(SettleFrames, Dt);

            Assert.AreEqual(0, rig.Root.EscapedTotal, "sealed bucket must not hole-escape");
            Assert.LessOrEqual(rig.Root.SettledTotal, 5,
                $"final-iter s_corr should prevent rim leak; settled={rig.Root.SettledTotal}");
            Assert.Greater(rig.Root.ActiveParticleCount, rig.Root.SpawnedTotal - 10,
                "most particles should remain live in the sealed bucket");
        }

        [Test]
        public void Sweep_GhostWeight_ReportTable()
        {
            float[] weights = { 1.0f, 0.7f, 0.5f, 0.3f, 0.15f, 0.0f };
            var rows = new List<SweepRow>();
            foreach (float w in weights)
            {
                rows.Add(RunSweep(w));
            }

            var report = new StringBuilder();
            report.AppendLine("V4 ghostWeight sweep — final-iter s_corr, kCorr=0.1, iter=8, tall sealed, 320 frames");
            report.AppendLine("ghostW  settled  live   bottom  mid     top");
            foreach (SweepRow r in rows)
            {
                report.AppendLine(
                    $"{r.GhostWeight,5:F2}  {r.Settled,7}  {r.Live,5}  {r.BottomRhoRatio,6:F3}  {r.MidRhoRatio,6:F3}  {r.TopRhoRatio,6:F3}");
            }

            SweepRow best = rows
                .OrderBy(r => Mathf.Abs(r.BottomRhoRatio - 1f))
                .ThenByDescending(r => r.GhostWeight)
                .First();
            report.AppendLine($"suggested ghostWeight (closest bottom to 1.0, leak<=5): {best.GhostWeight:F2}");

            WriteReport("ghost_weight_sweep.txt", report.ToString());
            Assert.Pass("Ghost weight sweep logged — see Results/ghost_weight_sweep.txt");
        }
    }
}
