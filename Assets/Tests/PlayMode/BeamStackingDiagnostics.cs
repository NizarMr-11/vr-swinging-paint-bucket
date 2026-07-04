using System;
using System.Reflection;
using System.Text;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hypothesis-1 instrumentation (report only, no pass/fail gate on the physics): do canvas-arrested
/// beam particles (excluded from the PBF solve by the Zone-A floor bound) separate from one another,
/// or do co-located arrivals stack/overlap with zero self-repulsion?
///
/// Method: spawn ONLY a tight cohort (so every active particle IS a tracked particle — index churn
/// from the radix sort is irrelevant to order-independent aggregate stats). Each frame we log the
/// cohort's (x,z) centroid, (x,z) spread (max pairwise horizontal distance), mean neighbor count
/// within a 0.05 radius, and peak local occupancy. Two phases:
///   A) BEAM: spawn below the floor band (localY &lt; -floor_tol) so they are PBF-excluded, near a
///      close canvas with culling on. Expectation under H1: spread stays ~0, neighbor counts stay
///      maxed = permanent overlap.
///   B) FLUID control: same tight spawn but ABOVE the floor (participating) with a far canvas. PBF
///      self-repulsion should spread them and drop neighbor counts.
///
/// Trace: Logs/HarmonicSimulation/beam_zonea_stacking.log
/// </summary>
[Category("Diagnostics")]
public class BeamStackingDiagnostics
{
    const float DeltaTime = 1f / 60f;
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float NeighborRadius = 0.05f;
    const int CohortSize = 12;
    const int Frames = 100;

    [Test]
    public void BeamArrivals_DoNotSeparate_WhileFluidDoes()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[Stacking] {DateTime.Now:yyyy-MM-dd HH:mm:ss} cohort={CohortSize} neighborR={NeighborRadius} frames={Frames}");

        PhaseResult beam = RunPhase(sb, "BEAM (PBF-excluded, canvas -0.5, culling on)",
            spawnLocalY: -0.05f, spawnLocalYStagger: -0.30f, canvasY: -0.5f, aboveFloor: false);

        PhaseResult fluid = RunPhase(sb, "FLUID (PBF participant, above floor, canvas -20)",
            spawnLocalY: 0.15f, spawnLocalYStagger: 0.15f, canvasY: -20f, aboveFloor: true);

        sb.AppendLine(new string('=', 96));
        sb.AppendLine("[Stacking] SUMMARY (final frame):");
        sb.AppendLine($"  BEAM : spreadXZ={beam.FinalSpread:F4}  meanNeighbors={beam.FinalMeanNeighbors:F2}  peakOccupancy={beam.FinalPeakOccupancy}  minY={beam.MinY:F3}");
        sb.AppendLine($"  FLUID: spreadXZ={fluid.FinalSpread:F4}  meanNeighbors={fluid.FinalMeanNeighbors:F2}  peakOccupancy={fluid.FinalPeakOccupancy}  minY={fluid.MinY:F3}");
        sb.AppendLine($"  spread growth  BEAM x{SafeRatio(beam.FinalSpread, beam.InitialSpread):F2}  FLUID x{SafeRatio(fluid.FinalSpread, fluid.InitialSpread):F2}");

        WriteTrace("beam_zonea_stacking.log", sb.ToString());
        Debug.Log(sb.ToString());

        Assert.Pass(sb.ToString());
    }

    struct PhaseResult
    {
        public float InitialSpread;
        public float FinalSpread;
        public float FinalMeanNeighbors;
        public int FinalPeakOccupancy;
        public float MinY;
    }

    PhaseResult RunPhase(StringBuilder sb, string label, float spawnLocalY, float spawnLocalYStagger, float canvasY, bool aboveFloor)
    {
        HarmonicPipelineController pipeline = CreatePipeline(capacity: 8192);
        var result = new PhaseResult { MinY = float.MaxValue };
        try
        {
            pipeline.SetPbfIterations(3);
            pipeline.SetCanvasPlaneY(canvasY);
            pipeline.SetCanvasCullingEnabled(true);
            SetupStationaryContainer(pipeline, out Matrix4x4 localToWorld);

            // Tight cohort within a 0.02 horizontal disc, staggered vertically to mimic a rain column
            // landing on the same radial spot.
            var particles = new FluidParticle[CohortSize];
            var rng = new System.Random(1234);
            for (int i = 0; i < CohortSize; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                float rad = 0.02f * (float)Math.Sqrt(rng.NextDouble());
                float x = Mathf.Cos(ang) * rad;
                float z = Mathf.Sin(ang) * rad;
                float y = spawnLocalY + spawnLocalYStagger * (i / (float)(CohortSize - 1));
                particles[i] = MakeParticle(localToWorld.MultiplyPoint3x4(new Vector3(x, y, z)), Vector3.zero);
            }
            int appended = pipeline.AppendParticles(particles, particles.Length);
            Assert.AreEqual(CohortSize, appended, "cohort spawn failed");

            sb.AppendLine(new string('-', 96));
            sb.AppendLine($"[Stacking] PHASE {label}");
            sb.AppendLine("[Stacking] frame | active | centroidXZ | spreadXZ | meanNbr | peakOcc | minY");

            for (int frame = 0; frame < Frames; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);
                if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint active) || active == 0)
                {
                    sb.AppendLine($"f{frame,4} | active=0 (all culled/removed)");
                    break;
                }

                FluidParticle[] p = GpuParticleReadbackUtility.ReadParticles(soa, (int)active);
                int n = p.Length;

                // world -> local for consistent (x,z) measurement
                var local = new Vector3[n];
                float cx = 0f, cz = 0f, minY = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var w = new Vector3(p[i].Position.x, p[i].Position.y, p[i].Position.z);
                    local[i] = localToWorld.inverse.MultiplyPoint3x4(w);
                    cx += local[i].x; cz += local[i].z;
                    minY = Mathf.Min(minY, local[i].y);
                }
                cx /= n; cz /= n;

                float maxPairXZ = 0f;
                int sumNeighbors = 0;
                int peakOcc = 0;
                for (int i = 0; i < n; i++)
                {
                    int nbr = 0;
                    for (int j = 0; j < n; j++)
                    {
                        if (i == j) continue;
                        Vector3 d = local[i] - local[j];
                        if (d.magnitude <= NeighborRadius) nbr++;
                        float dxz = new Vector2(local[i].x - local[j].x, local[i].z - local[j].z).magnitude;
                        if (dxz > maxPairXZ) maxPairXZ = dxz;
                    }
                    sumNeighbors += nbr;
                    peakOcc = Mathf.Max(peakOcc, nbr + 1);
                }
                float meanNbr = (float)sumNeighbors / n;

                result.MinY = Mathf.Min(result.MinY, minY);
                if (frame == 0) result.InitialSpread = maxPairXZ;
                result.FinalSpread = maxPairXZ;
                result.FinalMeanNeighbors = meanNbr;
                result.FinalPeakOccupancy = peakOcc;

                if (frame % 10 == 0 || frame == Frames - 1)
                {
                    sb.AppendLine($"f{frame,4} | {active,4} | ({cx,7:F4},{cz,7:F4}) | {maxPairXZ,7:F4} | {meanNbr,6:F2} | {peakOcc,5} | {minY,7:F3}");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
        return result;
    }

    static float SafeRatio(float a, float b) => b > 1e-5f ? a / b : 0f;

    static FluidParticle MakeParticle(Vector3 position, Vector3 velocity)
    {
        return new FluidParticle
        {
            Position = new float3(position.x, position.y, position.z),
            Velocity = new float3(velocity.x, velocity.y, velocity.z),
            Density = 1000f,
            Pressure = 0f,
            PackedColorRGBA = 0xFFFFFFFFu,
        };
    }

    static void SetupStationaryContainer(HarmonicPipelineController pipeline, out Matrix4x4 localToWorld)
    {
        Vector3 floorPivot = Vector3.zero;
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        localToWorld = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
    }

    static void StepStationaryFrame(HarmonicPipelineController pipeline, Matrix4x4 localToWorld)
    {
        pipeline.SetContainerFluidOriented(Vector3.zero, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.ApplyContainerRigidRotation(localToWorld, localToWorld, DeltaTime);
        SyncGpu();
        pipeline.ExecutePipelineFrame(DeltaTime);
        SyncGpu();
    }

    static void WriteTrace(string fileName, string report)
    {
        string logPath = System.IO.Path.Combine(
            Application.dataPath, "..", "Logs", "HarmonicSimulation", fileName);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
        System.IO.File.WriteAllText(logPath, report);
        Debug.Log($"[Stacking] trace written to {logPath}");
    }

    static HarmonicPipelineController CreatePipeline(int capacity)
    {
        var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
        Assert.IsNotNull(settings, "HarmonicPipelineTestSettings missing from Resources.");

#if UNITY_EDITOR
        var carryShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ContainerRigidCarry.compute");
#else
        ComputeShader carryShader = null;
#endif
        Assert.IsNotNull(carryShader, "ContainerRigidCarry.compute not found.");

        var go = new GameObject("BeamStackingDiagnosticsPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        FieldInfo carryField = typeof(HarmonicPipelineController).GetField(
            "containerRigidCarryShader", BindingFlags.Instance | BindingFlags.NonPublic);
        carryField?.SetValue(pipeline, carryShader);

        FieldInfo muteField = typeof(HarmonicPipelineController).GetField(
            "perfDiagnosticsMuted", BindingFlags.Instance | BindingFlags.NonPublic);
        muteField?.SetValue(pipeline, true);

        pipeline.ConfigureAndInitialize(
            settings.argumentUtilityShader,
            settings.spatialHashGridShader,
            settings.wcsphDensityShader,
            settings.dataCompactionShader,
            capacity: capacity,
            externalIngestion: true,
            autoRun: false,
            fallingShader: settings.fallingFluidWorldShader,
            eulerianShader: settings.eulerianDragGridShader,
            integrateShader: settings.wcsphIntegrationShader,
            pbfShader: settings.pbfSolverShader,
            radixShader: settings.radixSortShader);

        return pipeline;
    }

    static void SyncGpu()
    {
        GraphicsFence fence = Graphics.CreateGraphicsFence(
            GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }
}
