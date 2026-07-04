using System.Text;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    /// <summary>
    /// Diagnostic (not a pass/fail gate): decomposes the golden-frame avgC drop caused by the Zone-A
    /// beam-exclusion predicate. avgC = mean(density/rho0 - 1); an excluded particle has density = 0
    /// and therefore contributes exactly -1, so any increase in the excluded set mechanically lowers
    /// avgC regardless of whether the *participating* fluid changed its packing.
    ///
    /// This separates:
    ///   - avgC over ALL particles (reproduces the golden number)
    ///   - avgC over PARTICIPATING particles only (density > 0) — the real fluid packing
    ///   - how many newly-excluded particles are in-radius sub-floor (the Zone-A set) and their depth
    ///     distribution (shallow floor-fluid over-reach vs genuine deep beam/tunnel debris).
    /// The golden container is non-oriented with floorPivot at origin, so localY == worldY.
    /// </summary>
    [Category("GPU")]
    public class GoldenDeltaBreakdownDiagnostics
    {
        const float Radius = 0.55f;
        const float FloorTol = 0.01f;
        const float RadiusEps = 1e-4f;
        const float DensityEps = 1e-3f;

        [Test]
        public void GoldenDelta_SubFloorExclusionBreakdown()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                return;
            }
            if (Application.isPlaying)
            {
                Assert.Ignore("Edit Mode only.");
                return;
            }

            HarmonicPipelineController pipeline = null;
            try
            {
                pipeline = HarmonicGoldenFrameCapture.CreateLabEquivalentPipeline();
                HarmonicGoldenFrameCapture.RunSimulationFrames(pipeline);

                uint active = pipeline.GetActiveParticleCount();
                Assert.Greater(active, 0u);
                Assert.IsTrue(pipeline.TryGetDensityCacheBuffers(out ComputeBuffer densities, out _, out _));
                Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out _));

                int n = (int)active;
                var d = new float[n];
                densities.GetData(d, 0, 0, n);
                FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, n);

                float rho0 = pipeline.RestDensity;
                int nExcluded = 0, nParticipating = 0;
                int nExclInRadiusSubfloor = 0, nExclOutsideRadius = 0, nExclOther = 0;
                int shallow = 0, mid = 0, deep = 0; // (-tol..-0.05], (-0.05..-0.2], < -0.2
                float sumC_all = 0f, sumC_part = 0f;
                float minY = float.MaxValue;

                for (int i = 0; i < n; i++)
                {
                    float dev = d[i] / rho0 - 1f;
                    sumC_all += dev;

                    var pos = p[i].Position;
                    float y = pos.y; // localY == worldY for this container
                    float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
                    minY = Mathf.Min(minY, y);

                    if (d[i] < DensityEps)
                    {
                        nExcluded++;
                        bool inRadius = r <= Radius + RadiusEps;
                        bool subFloor = y < -FloorTol;
                        if (inRadius && subFloor)
                        {
                            nExclInRadiusSubfloor++;
                            if (y >= -0.05f) shallow++;
                            else if (y >= -0.2f) mid++;
                            else deep++;
                        }
                        else if (!inRadius)
                        {
                            nExclOutsideRadius++;
                        }
                        else
                        {
                            nExclOther++;
                        }
                    }
                    else
                    {
                        nParticipating++;
                        sumC_part += dev;
                    }
                }

                float avgC_all = sumC_all / n;
                float avgC_part = nParticipating > 0 ? sumC_part / nParticipating : 0f;
                float reincludeUpperBound = avgC_all + (float)nExclInRadiusSubfloor / n;

                var sb = new StringBuilder();
                sb.AppendLine($"[GoldenDelta] active={n} rho0={rho0:F1}");
                sb.AppendLine($"[GoldenDelta] avgC_all={avgC_all:F6}  avgC_participating_only={avgC_part:F6}");
                sb.AppendLine($"[GoldenDelta] participating={nParticipating}  excluded={nExcluded}");
                sb.AppendLine($"[GoldenDelta] excluded breakdown: inRadiusSubfloor(Zone-A set)={nExclInRadiusSubfloor}  outsideRadius(pre-existing)={nExclOutsideRadius}  other={nExclOther}");
                sb.AppendLine($"[GoldenDelta] Zone-A set depth: shallow(-{FloorTol}..-0.05]={shallow}  mid(-0.05..-0.2]={mid}  deep(<-0.2)={deep}");
                sb.AppendLine($"[GoldenDelta] minY={minY:F3}");
                sb.AppendLine($"[GoldenDelta] avgC_all upper-bound if Zone-A set were re-included at density=rho0: {reincludeUpperBound:F6}");
                Debug.Log(sb.ToString());

                string logPath = System.IO.Path.Combine(
                    Application.dataPath, "..", "Logs", "HarmonicSimulation", "beam_zonea_golden_delta.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
                System.IO.File.WriteAllText(logPath, sb.ToString());

                Assert.Pass(sb.ToString());
            }
            finally
            {
                if (pipeline != null)
                {
                    Object.DestroyImmediate(pipeline.gameObject);
                }
            }
        }
    }
}
