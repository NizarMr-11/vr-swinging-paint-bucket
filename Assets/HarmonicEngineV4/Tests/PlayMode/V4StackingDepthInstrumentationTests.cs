using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Instruments steady-state Poly6 density by bucket-local y-layer to test whether
    /// stacking quality degrades with fluid depth at fixed pbfIterations (Jacobi propagation).
    /// Reports only — does not assert simulation correctness.
    /// </summary>
    public sealed class V4StackingDepthInstrumentationTests
    {
        private const float Dt = 1f / 60f;
        private const int SettleFrames = 320;
        private const float GlobalDensity = 120000f;
        private const float BucketRadius = 0.3f;
        private const float WallThickness = 0.05f;

        private struct LayerRecord
        {
            public int LayerIndex;
            public float LocalYCenter;
            public int ParticleCount;
            public float AvgDensity;
            public float DensityRatio;
        }

        private struct DepthCaseResult
        {
            public string Label;
            public int TargetLayers;
            public int PbfIterations;
            public float BucketHeight;
            public int ParticleCount;
            public float RestDensity;
            public float LayerBand;
            public int MeasuredLayers;
            public List<LayerRecord> Layers;
            public float BottomLayerRatio;
            public float TopLayerRatio;
            public double AvgStepMs;
        }

        private static V4TestRig.Config ColumnConfig(int targetLayers)
        {
            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float particleRadius = V4SpawnMath.ParticleRadiusFromDensity(GlobalDensity);
            float fluidHeight = targetLayers * spacing;
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
                }
            };
        }

        private static DepthCaseResult RunCase(string label, int targetLayers, int pbfIterations, bool measureTiming)
        {
            using var rig = V4TestRig.Create(ColumnConfig(targetLayers));
            rig.Root.pbfIterations = pbfIterations;
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            double stepMs = 0.0;
            if (measureTiming)
            {
                const int warmup = 30;
                const int timed = 120;
                rig.Step(warmup, Dt);
                var sw = Stopwatch.StartNew();
                rig.Step(timed, Dt);
                sw.Stop();
                stepMs = sw.Elapsed.TotalMilliseconds / timed;
                rig.Step(SettleFrames - warmup - timed, Dt);
            }
            else
            {
                rig.Step(SettleFrames, Dt);
            }

            float restDensity = rig.Root.GpuRestDensity();
            float layerBand = rig.Root.ParticleRadius * 2f;
            Vector4[] positions = rig.ReadPositions();
            uint[] flags = rig.ReadFlags();
            float[] densities = rig.ReadDensities();

            var layerBuckets = new Dictionary<int, List<float>>();
            var layerY = new Dictionary<int, List<float>>();

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
                if (!layerBuckets.TryGetValue(layer, out List<float> bucket))
                {
                    bucket = new List<float>();
                    layerBuckets[layer] = bucket;
                    layerY[layer] = new List<float>();
                }

                bucket.Add(densities[i]);
                layerY[layer].Add(local.y);
            }

            var layers = new List<LayerRecord>();
            foreach (KeyValuePair<int, List<float>> kv in layerBuckets)
            {
                int layer = kv.Key;
                List<float> vals = kv.Value;
                float sum = 0f;
                float ySum = 0f;
                foreach (float v in vals)
                {
                    sum += v;
                }

                foreach (float y in layerY[layer])
                {
                    ySum += y;
                }

                float avg = sum / vals.Count;
                layers.Add(new LayerRecord
                {
                    LayerIndex = layer,
                    LocalYCenter = ySum / vals.Count,
                    ParticleCount = vals.Count,
                    AvgDensity = avg,
                    DensityRatio = avg / restDensity
                });
            }

            layers.Sort((a, b) => a.LayerIndex.CompareTo(b.LayerIndex));

            float bottomRatio = layers.Count > 0 ? layers[0].DensityRatio : 0f;
            float topRatio = layers.Count > 0 ? layers[layers.Count - 1].DensityRatio : 0f;

            return new DepthCaseResult
            {
                Label = label,
                TargetLayers = targetLayers,
                PbfIterations = pbfIterations,
                BucketHeight = rig.Bucket.height,
                ParticleCount = rig.Root.ActiveParticleCount,
                RestDensity = restDensity,
                LayerBand = layerBand,
                MeasuredLayers = layers.Count,
                Layers = layers,
                BottomLayerRatio = bottomRatio,
                TopLayerRatio = topRatio,
                AvgStepMs = stepMs
            };
        }

        private static string FormatCaseReport(DepthCaseResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {result.Label} (pbfIterations={result.PbfIterations}) ===");
            sb.AppendLine(
                $"targetLayers={result.TargetLayers} measuredLayers={result.MeasuredLayers} particles={result.ParticleCount} bucketH={result.BucketHeight:F4} restDensity={result.RestDensity:F2} layerBand={result.LayerBand:F5}");
            sb.AppendLine(
                $"bottomLayer rho/rho0={result.BottomLayerRatio:F4}  topLayer rho/rho0={result.TopLayerRatio:F4}");
            if (result.AvgStepMs > 0.0)
            {
                sb.AppendLine($"avgStepMs={result.AvgStepMs:F3}  approxFps={1000.0 / result.AvgStepMs:F1}");
            }

            sb.AppendLine("layer  yCenter   count   avgRho   rho/rho0");
            foreach (LayerRecord layer in result.Layers)
            {
                sb.AppendLine(
                    $"{layer.LayerIndex,4}  {layer.LocalYCenter,7:F4}  {layer.ParticleCount,5}  {layer.AvgDensity,8:F2}  {layer.DensityRatio,7:F4}");
            }

            return sb.ToString();
        }

        private static void LogReport(string report)
        {
            string[] lines = report.Split('\n');
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Debug.Log(line.TrimEnd('\r'));
                }
            }
        }

        private static void WriteReportFile(string fileName, string report)
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "HarmonicEngineV4/Tests/PlayMode/Results", fileName);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? string.Empty);
            System.IO.File.WriteAllText(path, report);
            Debug.Log($"Wrote instrumentation report: {path}");
        }

        [Test]
        public void Instrument_StackingDepth_AtDefaultIterations()
        {
            int defaultIterations = 2;
            DepthCaseResult shallow = RunCase("SHALLOW (~4 layers)", 4, defaultIterations, false);
            DepthCaseResult medium = RunCase("MEDIUM (~10 layers)", 10, defaultIterations, false);
            DepthCaseResult tall = RunCase("TALL (~20 layers)", 20, defaultIterations, false);

            var report = new StringBuilder();
            report.AppendLine("V4 stacking depth instrumentation — fixed pbfIterations=" + defaultIterations);
            report.Append(FormatCaseReport(shallow));
            report.Append(FormatCaseReport(medium));
            report.Append(FormatCaseReport(tall));
            report.AppendLine("--- depth comparison ---");
            report.AppendLine(
                $"bottom rho/rho0: shallow={shallow.BottomLayerRatio:F4} medium={medium.BottomLayerRatio:F4} tall={tall.BottomLayerRatio:F4}");
            report.AppendLine(
                $"floor convergence gap from 1.0: shallow={1f - shallow.BottomLayerRatio:F4} medium={1f - medium.BottomLayerRatio:F4} tall={1f - tall.BottomLayerRatio:F4}");

            LogReport(report.ToString());
            WriteReportFile("stacking_depth_default_iters.txt", report.ToString());
            Assert.Pass("Instrumentation complete — see log for density-by-layer report.");
        }

        [Test]
        public void Instrument_TallColumn_IterationSweep()
        {
            DepthCaseResult iter4 = RunCase("TALL iter=4", 20, 4, false);
            DepthCaseResult iter8 = RunCase("TALL iter=8", 20, 8, true);
            DepthCaseResult iter16 = RunCase("TALL iter=16", 20, 16, false);

            var report = new StringBuilder();
            report.AppendLine("V4 stacking depth instrumentation — TALL column iteration sweep");
            report.Append(FormatCaseReport(iter4));
            report.Append(FormatCaseReport(iter8));
            report.Append(FormatCaseReport(iter16));
            report.AppendLine("--- iteration sweep (bottom layer) ---");
            report.AppendLine(
                $"bottom rho/rho0: iter4={iter4.BottomLayerRatio:F4} iter8={iter8.BottomLayerRatio:F4} iter16={iter16.BottomLayerRatio:F4}");
            report.AppendLine(
                $"gap from 1.0: iter4={1f - iter4.BottomLayerRatio:F4} iter8={1f - iter8.BottomLayerRatio:F4} iter16={1f - iter16.BottomLayerRatio:F4}");

            LogReport(report.ToString());
            WriteReportFile("stacking_depth_iteration_sweep.txt", report.ToString());
            Assert.Pass("Iteration sweep complete — see log for density-by-layer report.");
        }
    }
}
