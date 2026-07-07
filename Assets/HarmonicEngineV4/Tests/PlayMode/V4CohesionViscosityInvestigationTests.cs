using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Investigates whether FinalizeKernel cohesion/XSPH terms drive top-of-pile upward velocity.
    /// Reports only.
    /// </summary>
    public sealed class V4CohesionViscosityInvestigationTests
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

        private static V4TestRig.Config TallConfig(System.Action<V4LiquidProfile> configureProfile = null)
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
                ConfigureProfile = configureProfile,
                ConfigureRoot = root =>
                {
                    root.pbfIterations = PbfIterations;
                    root.boundaryGhostWeight = 0f;
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
        public void Investigate_FormulasAndRestTopLayer()
        {
            using var rig = V4TestRig.Create(TallConfig());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float particleRadius = V4SpawnMath.ParticleRadiusFromDensity(GlobalDensity);
            float bucketHeight = rig.Bucket.height;
            float topBandMin = bucketHeight - particleRadius * 2f;
            float restDensity = rig.Root.GpuRestDensity();

            var frameStats = new Dictionary<int, (float mean, float max, float min, int count)>();

            for (int frame = 0; frame < Frames; frame++)
            {
                rig.Step(1, Dt);
                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();

                float maxInsideY = float.MinValue;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(positions[i]);
                    maxInsideY = Mathf.Max(maxInsideY, local.y);
                }

                float bandMin = maxInsideY - particleRadius * 2f;
                float sum = 0f;
                float max = float.MinValue;
                float min = float.MaxValue;
                int count = 0;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(positions[i]);
                    if (local.y < bandMin)
                    {
                        continue;
                    }

                    float vy = velocities[i].y;
                    sum += vy;
                    max = Mathf.Max(max, vy);
                    min = Mathf.Min(min, vy);
                    count++;
                }

                if (count > 0)
                {
                    frameStats[frame] = (sum / count, max, min, count);
                }
            }

            // Particles in top band at final frame — retroactive vel.y from last snapshot only
            Vector4[] finalPos = rig.ReadPositions();
            Vector4[] finalVel = rig.ReadVelocities();
            uint[] finalFlags = rig.ReadFlags();
            var settledTopIndices = new List<int>();
            for (int i = 0; i < finalPos.Length; i++)
            {
                if (!V4ParticleFlags.IsInside(finalFlags[i]))
                {
                    continue;
                }

                Vector3 local = rig.Bucket.transform.InverseTransformPoint(finalPos[i]);
                if (local.y >= topBandMin && local.y <= bucketHeight)
                {
                    settledTopIndices.Add(i);
                }
            }

            var report = new StringBuilder();
            report.AppendLine("V4 cohesion/XSPH investigation — formulas + rest top layer");
            report.AppendLine("=== 1. FinalizeKernel formulas (V4PbfSolver.compute) ===");
            report.AppendLine("Neighbor loop over predicted-position grid (radius h):");
            report.AppendLine("  w = V4Poly6(r, h)");
            report.AppendLine("  xsph += (vj - vel) * w");
            report.AppendLine("  cohesion -= (diff/r) * w   [diff = predicted_i - predicted_j]");
            report.AppendLine("selfW = max(V4Poly6(0, h), 1e-6)   // fixed kernel at r=0, NOT neighbor-weight sum");
            report.AppendLine("vel += profile.viscosity * (xsph / selfW)");
            report.AppendLine("vel += profile.cohesion * (cohesion / selfW) * _DeltaTime");
            report.AppendLine("Notes:");
            report.AppendLine("  - Neither term divides by local density or neighbor count.");
            report.AppendLine("  - Normalization is ONLY by selfW (constant per particle, ~Poly6(0,h)).");
            report.AppendLine("  - No pbfEpsilon-style safeguard on cohesion/xsph denominators.");
            report.AppendLine("  - At low neighbor count, numerators shrink but divisor stays fixed -> smaller deltas, not blow-up.");
            report.AppendLine($"Default profile: cohesion=0.5 viscosity=0.25 restDensity(target)={restDensity:F1}");
            report.AppendLine();
            report.AppendLine("=== 2. Rest 300 frames — sparse top layer vel.y (top 2*radius below current max inside y) ===");
            report.AppendLine($"absolute rim band for reference: [{topBandMin:F4}, {bucketHeight:F4}]");
            report.AppendLine($"particles in absolute rim band at f299: {settledTopIndices.Count}");
            report.AppendLine("frame  meanVelY  maxVelY  minVelY  count");

            int[] sampleFrames = { 0, 1, 2, 3, 10, 30, 60, 120, 180, 299 };
            foreach (int f in sampleFrames)
            {
                if (!frameStats.TryGetValue(f, out var stat))
                {
                    report.AppendLine($"{f,5}  (no particles in surface band)");
                    continue;
                }

                report.AppendLine($"{f,5}  {stat.mean,8:F4}  {stat.max,7:F4}  {stat.min,7:F4}  {stat.count,5}");
            }

            if (frameStats.TryGetValue(0, out var f0) && frameStats.TryGetValue(299, out var f299))
            {
                report.AppendLine(
                    $"rest summary: f0 surfaceBand mean/max={f0.mean:F4}/{f0.max:F4} f299 mean/max={f299.mean:F4}/{f299.max:F4}");
            }

            WriteReport("cohesion_viscosity_rest_top_layer.txt", report.ToString());
            Assert.Pass("Formula + rest top-layer report logged.");
        }

        [Test]
        public void Investigate_Frame3RimCrosserFinalizeTerms()
        {
            const int RimFrame = 3;
            int[] trackIndices = { 1472, 1488 };

            using var rig = V4TestRig.Create(TallConfig());
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.Root.DebugTrackParticleIndices = trackIndices;

            var perFrame = new Dictionary<int, V4PipelineRoot.V4FinalizeTermsProbe[]>();
            var velYHistory = new Dictionary<int, float[]>();
            foreach (int idx in trackIndices)
            {
                velYHistory[idx] = new float[RimFrame + 1];
            }

            float time = 0f;
            var previousAboveRim = new HashSet<int>();
            int firstRim = -1;

            for (int frame = 0; frame <= RimFrame; frame++)
            {
                time += Dt;
                rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * ShakeOmega) * ShakeAmplitude, 0f, 0f);

                V4PipelineRoot.V4FinalizeTermsProbe[] probes = null;
                rig.Root.DebugAfterFinalizeTermsProbe = p => probes = p;
                rig.Step(1, Dt);
                rig.Root.DebugAfterFinalizeTermsProbe = null;

                if (probes != null)
                {
                    perFrame[frame] = probes;
                }

                Vector4[] velocities = rig.ReadVelocities();
                Vector4[] positions = rig.ReadPositions();
                uint[] flags = rig.ReadFlags();
                var aboveRimNow = new HashSet<int>();
                float bucketHeight = rig.Bucket.height;

                for (int i = 0; i < positions.Length; i++)
                {
                    if (velYHistory.TryGetValue(i, out float[] hist))
                    {
                        hist[frame] = velocities[i].y;
                    }

                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(positions[i]);
                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                    }
                }

                foreach (int idx in aboveRimNow)
                {
                    if (!previousAboveRim.Contains(idx) && firstRim < 0)
                    {
                        firstRim = frame;
                    }
                }

                previousAboveRim = aboveRimNow;
            }

            var report = new StringBuilder();
            report.AppendLine("V4 cohesion/XSPH — frame-3 rim crosser finalize terms (particles 1472, 1488)");
            report.AppendLine($"firstRimCrossingFrame={firstRim}");
            report.AppendLine("Per frame (pre-collision Finalize terms probe):");
            report.AppendLine("frame  idx  vel.y  density  neighbors  cohesionDelta(x,y,z)  xsphDelta(x,y,z)");

            for (int frame = 0; frame <= RimFrame; frame++)
            {
                if (!perFrame.TryGetValue(frame, out V4PipelineRoot.V4FinalizeTermsProbe[] probes))
                {
                    continue;
                }

                foreach (V4PipelineRoot.V4FinalizeTermsProbe probe in probes)
                {
                    float vy = velYHistory[probe.ParticleIndex][frame];
                    report.AppendLine(
                        $"{frame,5}  {probe.ParticleIndex,4}  {vy,6:F4}  {probe.Density,7:F1}  {probe.NeighborCount,9}  " +
                        $"({probe.CohesionDelta.x:F5},{probe.CohesionDelta.y:F5},{probe.CohesionDelta.z:F5})  " +
                        $"({probe.XsphDelta.x:F5},{probe.XsphDelta.y:F5},{probe.XsphDelta.z:F5})");
                }
            }

            WriteReport("cohesion_viscosity_frame3_crossers.txt", report.ToString());
            Assert.Pass("Frame-3 crosser finalize terms logged.");
        }

        [Test]
        public void Investigate_ShakeIsolation_CohesionAndViscosity()
        {
            var report = new StringBuilder();
            report.AppendLine("V4 cohesion/XSPH isolation — 300-frame lateral shake");
            report.AppendLine($"shake: x = {ShakeAmplitude} * sin({ShakeOmega} * t)");
            report.AppendLine();

            RunShakeIsolation(report, "default", null);
            RunShakeIsolation(report, "cohesion=0", p => p.cohesion = 0f);
            RunShakeIsolation(report, "viscosity=0", p => p.viscosity = 0f);

            WriteReport("cohesion_viscosity_shake_isolation.txt", report.ToString());
            Assert.Pass("Shake isolation report logged.");
        }

        private static void RunShakeIsolation(StringBuilder report, string label, System.Action<V4LiquidProfile> configure)
        {
            using var rig = V4TestRig.Create(TallConfig(configure));
            rig.Bucket.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float spacing = V4SpawnMath.SpacingFromDensity(GlobalDensity);
            float bucketHeight = rig.Bucket.height;

            int firstRim = -1;
            int firstSettle = -1;
            var previousAboveRim = new HashSet<int>();
            float time = 0f;
            float particleRadius = V4SpawnMath.ParticleRadiusFromDensity(GlobalDensity);
            float topBandMin = bucketHeight - particleRadius * 2f;
            var topVelSamples = new Dictionary<int, float>();

            for (int frame = 0; frame < Frames; frame++)
            {
                time += Dt;
                rig.Bucket.transform.position = new Vector3(Mathf.Sin(time * ShakeOmega) * ShakeAmplitude, 0f, 0f);
                rig.Step(1, Dt);

                Vector4[] positions = rig.ReadPositions();
                Vector4[] velocities = rig.ReadVelocities();
                uint[] flags = rig.ReadFlags();
                var aboveRimNow = new HashSet<int>();
                float topVelSum = 0f;
                int topN = 0;

                for (int i = 0; i < positions.Length; i++)
                {
                    if (!V4ParticleFlags.IsInside(flags[i]))
                    {
                        continue;
                    }

                    Vector3 local = rig.Bucket.transform.InverseTransformPoint(positions[i]);
                    if (local.y >= topBandMin && local.y <= bucketHeight)
                    {
                        topVelSum += velocities[i].y;
                        topN++;
                    }

                    if (local.y > bucketHeight)
                    {
                        aboveRimNow.Add(i);
                    }
                }

                if (frame == 0 || frame == 3 || frame == 30 || frame == 299)
                {
                    topVelSamples[frame] = topN > 0 ? topVelSum / topN : 0f;
                }

                foreach (int idx in aboveRimNow)
                {
                    if (!previousAboveRim.Contains(idx) && firstRim < 0)
                    {
                        firstRim = frame;
                    }
                }

                if (firstSettle < 0 && rig.Root.SettledTotal > 0)
                {
                    firstSettle = frame;
                }

                previousAboveRim = aboveRimNow;
            }

            report.AppendLine($"=== {label} ===");
            report.AppendLine($"firstRimCrossing=f{firstRim} firstSettle=f{firstSettle} settled@f299={rig.Root.SettledTotal}");
            report.AppendLine(
                $"topBand mean vel.y: f0={Sample(topVelSamples, 0):F4} f3={Sample(topVelSamples, 3):F4} " +
                $"f30={Sample(topVelSamples, 30):F4} f299={Sample(topVelSamples, 299):F4}");
            report.AppendLine();
        }

        private static float Sample(Dictionary<int, float> samples, int frame)
        {
            return samples.TryGetValue(frame, out float value) ? value : 0f;
        }
    }
}
