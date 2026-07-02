#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scale / lab-packing / canvas isolation diagnostics.
/// HarmonicEngine/Diagnostics/Run Scale Divergence Report — original 3k vs 20k harness comparison.
/// HarmonicEngine/Diagnostics/Run Lab Packing Isolation (A/B/C) — spacing vs pbfIters confounds.
/// HarmonicEngine/Diagnostics/Run Canvas Isolation (D/E) — lab canvas + spillOverRim confounds.
/// </summary>
public static class ScaleDivergenceDiagRunner
{
    const int FrameCount = 120;
    const float DeltaTime = 1f / 60f;
    const float SpinDegPerFrame = 8f;
    const float Radius = 0.55f;
    const float Height = 1.1f;
    const float RadiusEps = 1e-4f;
    const float BeamThresholdLocalY = -0.15f;
    const float LabCanvasY = -2f;
    const int RimTraceHorizon = 60;

    readonly struct DiagConfig
    {
        public readonly string Label;
        public readonly float CellSize;
        public readonly int LatticeMax;
        public readonly int PbfIters;
        public readonly int Capacity;
        public readonly float CanvasPlaneY;
        public readonly bool CanvasCulling;
        public readonly bool SpillOverRim;
        public readonly bool TraceRimExitWorldY;

        public DiagConfig(
            string label,
            float cellSize,
            int latticeMax,
            int pbfIters,
            int capacity,
            float canvasPlaneY = -6f,
            bool canvasCulling = true,
            bool spillOverRim = false,
            bool traceRimExitWorldY = false)
        {
            Label = label;
            CellSize = cellSize;
            LatticeMax = latticeMax;
            PbfIters = pbfIters;
            Capacity = capacity;
            CanvasPlaneY = canvasPlaneY;
            CanvasCulling = canvasCulling;
            SpillOverRim = spillOverRim;
            TraceRimExitWorldY = traceRimExitWorldY;
        }

        public float Spacing => CellSize * 0.5f;
    }

    sealed class RimExitTracker
    {
        public int Index;
        public int ExitFrame;
        public float ExitWorldY;
        public readonly float[] WorldY = new float[RimTraceHorizon + 1];
        public int SamplesFilled;
    }

    struct ArgminResult
    {
        public float GlobalMinLocalY;
        public int GlobalMinFrame;
        public int GlobalMinIndex;
        public float GlobalMinR;
        public int Spawned;
        public int FootprintTransitions;
        public int RimExitTransitions;
        public int UnderBucketTransitions;
        public int OtherTransitions;
        public Dictionary<int, int> RadialBins;
        public float[] PerFrameMinY;
        public DiagConfig Config;
    }

    [MenuItem("HarmonicEngine/Diagnostics/Run Scale Divergence Report")]
    public static void RunAndLog()
    {
        if (!CheckCompute())
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[ScaleDivergence] production canvas -6 / culling on; headless harness params");
        RunArgmin(new DiagConfig("3k harness", 0.1f, 3000, 3, 8192), sb);
        sb.AppendLine();
        RunArgmin(new DiagConfig("20k cap harness", 0.1f, 20000, 3, 32768), sb);
        Debug.Log(sb.ToString());
    }

    [MenuItem("HarmonicEngine/Diagnostics/Run Lab Packing Isolation (A/B/C)")]
    public static void RunLabPackingIsolation()
    {
        if (!CheckCompute())
        {
            return;
        }

        var configs = new[]
        {
            new DiagConfig("A spacing isolate", 0.05f, 20000, 3, 25000),
            new DiagConfig("B pbfIters isolate", 0.1f, 3000, 2, 8192),
            new DiagConfig("C full lab match", 0.05f, 20000, 2, 25000),
        };

        var sb = new StringBuilder();
        sb.AppendLine("[LabPackingIsolation] production canvas -6 / culling on; 120f 8deg/frame spin");
        ArgminResult? beamResult = null;

        foreach (DiagConfig config in configs)
        {
            sb.AppendLine();
            ArgminResult result = RunArgmin(config, sb);
            if (beamResult == null && result.GlobalMinLocalY < BeamThresholdLocalY)
            {
                beamResult = result;
            }
        }

        if (beamResult.HasValue)
        {
            sb.AppendLine();
            sb.AppendLine($"[LabPackingIsolation] BEAM reproduced in {beamResult.Value.Config.Label} — running PBF substage trace on idx={beamResult.Value.GlobalMinIndex} frame={beamResult.Value.GlobalMinFrame}");
            RunSubstageTrace(beamResult.Value, sb);
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("[LabPackingIsolation] No config reproduced beam (globalMinLocalYInRadius >= -0.15).");
            sb.AppendLine("  Next untested confounds: canvasPlaneY=-2, canvasCullingEnabled=false (lab canvas), spillOverRim transfer.");
        }

        Debug.Log(sb.ToString());
    }

    [MenuItem("HarmonicEngine/Diagnostics/Run Canvas Isolation (D/E)")]
    public static void RunCanvasIsolation()
    {
        if (!CheckCompute())
        {
            return;
        }

        var configs = new[]
        {
            new DiagConfig(
                "D lab canvas no spill",
                0.05f, 20000, 2, 25000,
                canvasPlaneY: LabCanvasY,
                canvasCulling: false,
                spillOverRim: false,
                traceRimExitWorldY: true),
            new DiagConfig(
                "E lab canvas + spillOverRim",
                0.05f, 20000, 2, 25000,
                canvasPlaneY: LabCanvasY,
                canvasCulling: false,
                spillOverRim: true,
                traceRimExitWorldY: true),
        };

        var sb = new StringBuilder();
        sb.AppendLine("[CanvasIsolation] Config C particle/PBF held; 120f 8deg/frame spin");
        foreach (DiagConfig config in configs)
        {
            sb.AppendLine();
            RunArgmin(config, sb);
        }

        Debug.Log(sb.ToString());
    }

    static bool CheckCompute()
    {
        if (SystemInfo.supportsComputeShaders)
        {
            return true;
        }

        Debug.LogError("[ScaleDivergence] Compute shaders not supported.");
        return false;
    }

    static ArgminResult RunArgmin(DiagConfig config, StringBuilder sb)
    {
        var radialBins = new Dictionary<int, int>();
        HarmonicPipelineController pipeline = CreatePipeline(config);
        Vector3 floorPivot = Vector3.zero;
        pipeline.SetContainerFluidOriented(
            floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        int spawned = pipeline.TrySpawnContainerLatticeFill();
        float fillTopY = Height * 0.5f;
        float spacing = pipeline.LatticeSpacing;
        Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);

        float globalMinY = float.MaxValue;
        int globalMinFrame = -1, globalMinIdx = -1;
        float globalMinR = 0f;
        float globalMinWorldY = float.MaxValue;
        float globalMinOutsideWorldY = float.MaxValue;
        var perFrameMin = new float[FrameCount];
        var prevOutside = new Dictionary<int, bool>();
        int footprintTransitions = 0;
        int rimExit = 0, underBucket = 0, otherTransitions = 0;
        var transitionLog = new List<string>();
        var rimTraces = new Dictionary<int, RimExitTracker>();
        int maxOutsideFootprintCount = 0;
        int steadyOutsideTail = 0;
        uint prevFallingReport = 0;
        int spillFramesWithDelta = 0;
        float spillRateSum = 0f;

        for (int frame = 0; frame < FrameCount; frame++)
        {
            float t = frame * SpinDegPerFrame;
            Quaternion rot = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
            Matrix4x4 w2l = currL2W.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot, rot, Radius, Height, 0.1f, 0.85f, 400f);
            pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
            SyncGpu();
            pipeline.ExecutePipelineFrame(DeltaTime);
            SyncGpu();
            prevL2W = currL2W;

            uint fallingReport = pipeline.LastFallingQuantizeCount;
            if (config.SpillOverRim && frame > 0)
            {
                uint delta = fallingReport > prevFallingReport ? fallingReport - prevFallingReport : 0;
                if (delta > 0)
                {
                    spillFramesWithDelta++;
                    spillRateSum += delta;
                }
            }

            prevFallingReport = fallingReport;

            if (!pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint activeCount) || activeCount == 0)
            {
                perFrameMin[frame] = float.NaN;
                continue;
            }

            FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)activeCount);
            float frameMin = float.MaxValue;
            int frameIdx = -1;
            float frameR = 0f;
            int outsideCount = 0;

            for (int i = 0; i < particles.Length; i++)
            {
                Vector3 pos = new Vector3(particles[i].Position.x, particles[i].Position.y, particles[i].Position.z);
                Vector3 local = w2l.MultiplyPoint3x4(pos);
                float r = new Vector2(local.x, local.z).magnitude;
                bool outside = r > Radius + RadiusEps;

                if (outside)
                {
                    outsideCount++;
                    if (pos.y < globalMinOutsideWorldY)
                    {
                        globalMinOutsideWorldY = pos.y;
                    }
                }

                if (pos.y < globalMinWorldY)
                {
                    globalMinWorldY = pos.y;
                }

                if (prevOutside.TryGetValue(i, out bool wasOutside) && !wasOutside && outside)
                {
                    footprintTransitions++;
                    string kind = ClassifyTransition(r, local.y);
                    switch (kind)
                    {
                        case "rim-exit": rimExit++; break;
                        case "under-bucket": underBucket++; break;
                        default: otherTransitions++; break;
                    }

                    if (transitionLog.Count < 12)
                    {
                        transitionLog.Add(
                            $"  f={frame} idx={i} r={r:F4} localY={local.y:F4} worldY={pos.y:F4} kind={kind} stage=postApply");
                    }

                    if (config.TraceRimExitWorldY && kind == "rim-exit" && !rimTraces.ContainsKey(i))
                    {
                        var tracker = new RimExitTracker
                        {
                            Index = i,
                            ExitFrame = frame,
                            ExitWorldY = pos.y,
                        };
                        tracker.WorldY[0] = pos.y;
                        tracker.SamplesFilled = 1;
                        rimTraces[i] = tracker;
                    }
                }

                prevOutside[i] = outside;

                if (config.TraceRimExitWorldY && rimTraces.TryGetValue(i, out RimExitTracker trace))
                {
                    int offset = frame - trace.ExitFrame;
                    if (offset >= 0 && offset <= RimTraceHorizon && trace.SamplesFilled <= offset)
                    {
                        trace.WorldY[offset] = pos.y;
                        trace.SamplesFilled = offset + 1;
                    }
                }

                if (r <= Radius + RadiusEps && local.y < frameMin)
                {
                    frameMin = local.y;
                    frameIdx = i;
                    frameR = r;
                }
            }

            maxOutsideFootprintCount = Mathf.Max(maxOutsideFootprintCount, outsideCount);
            if (frame >= FrameCount - 30)
            {
                steadyOutsideTail += outsideCount;
            }

            perFrameMin[frame] = frameMin == float.MaxValue ? float.NaN : frameMin;
            if (frameMin < globalMinY)
            {
                globalMinY = frameMin;
                globalMinFrame = frame;
                globalMinIdx = frameIdx;
                globalMinR = frameR;
            }

            if (frameIdx >= 0)
            {
                int bin = Mathf.Clamp(Mathf.RoundToInt(frameR / Radius * 20f), 0, 20);
                radialBins.TryGetValue(bin, out int c);
                radialBins[bin] = c + 1;
            }
        }

        float maxTs = GetContainerMaxTimeStep(pipeline);
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(DeltaTime, maxTs) / maxTs), 1, 2);

        sb.AppendLine($"--- {config.Label} (latticeMax={config.LatticeMax} spawned={spawned} capacity={config.Capacity}) ---");
        sb.AppendLine(
            $"  cellSize={config.CellSize:F3} spacing={spacing:F4} fillTop={fillTopY:F2} spawnRadius={Radius * 0.9f:F2}");
        sb.AppendLine(
            $"  canvasY={pipeline.CanvasPlaneY:F1} culling={pipeline.CanvasCullingEnabled} spillOverRim={config.SpillOverRim} " +
            $"maxTimeStep={maxTs:F4} frameDt={DeltaTime:F4} pbfSubsteps={steps} pbfIters={pipeline.PbfIterations}");
        sb.AppendLine(
            $"  globalMinLocalYInRadius={globalMinY:F6} @frame={globalMinFrame} idx={globalMinIdx} " +
            $"r={globalMinR:F6} |r-radius|={Mathf.Abs(globalMinR - Radius):F6}");
        sb.AppendLine(
            $"  globalMinWorldY(all)={globalMinWorldY:F4} globalMinWorldY(outsideFp)={globalMinOutsideWorldY:F4} " +
            $"maxOutsideFpCount={maxOutsideFootprintCount} avgOutsideLast30f={(steadyOutsideTail / 30f):F1}");
        sb.AppendLine(
            $"  footprintTransitions={footprintTransitions} rim-exit={rimExit} under-bucket={underBucket} other={otherTransitions}");
        foreach (string line in transitionLog)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine("  radialBins (argmin, bin/20 ~= r/R):");
        foreach (KeyValuePair<int, int> kv in radialBins.OrderBy(k => k.Key))
        {
            sb.AppendLine($"    bin{kv.Key}={kv.Value}");
        }

        sb.AppendLine("  perFrameMinY[0..9]: " + string.Join(", ", SampleRange(perFrameMin, 0, 10)));
        sb.AppendLine("  perFrameMinY[110..119]: " + string.Join(", ", SampleRange(perFrameMin, 110, 10)));

        if (config.TraceRimExitWorldY)
        {
            AppendRimExitWorldYReport(sb, rimTraces, config);
            AppendSpillBufferReport(sb, pipeline, config, spillFramesWithDelta, spillRateSum);
        }

        var result = new ArgminResult
        {
            GlobalMinLocalY = globalMinY,
            GlobalMinFrame = globalMinFrame,
            GlobalMinIndex = globalMinIdx,
            GlobalMinR = globalMinR,
            Spawned = spawned,
            FootprintTransitions = footprintTransitions,
            RimExitTransitions = rimExit,
            UnderBucketTransitions = underBucket,
            OtherTransitions = otherTransitions,
            RadialBins = radialBins,
            PerFrameMinY = perFrameMin,
            Config = config,
        };

        UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        return result;
    }

    static void AppendRimExitWorldYReport(StringBuilder sb, Dictionary<int, RimExitTracker> rimTraces, DiagConfig config)
    {
        if (rimTraces.Count == 0)
        {
            sb.AppendLine("  rimExitWorldY: no rim-exit transitions observed");
            return;
        }

        int pastCanvas = 0;
        int uncaughtAtHorizon = 0;
        float deepest = float.MaxValue;
        RimExitTracker deepestTrace = null;

        foreach (RimExitTracker trace in rimTraces.Values)
        {
            int last = Mathf.Min(trace.SamplesFilled - 1, RimTraceHorizon);
            float minY = float.MaxValue;
            for (int s = 0; s <= last; s++)
            {
                minY = Mathf.Min(minY, trace.WorldY[s]);
            }

            if (minY < config.CanvasPlaneY - 0.01f)
            {
                pastCanvas++;
            }

            if (last >= RimTraceHorizon && trace.WorldY[RimTraceHorizon] < config.CanvasPlaneY - 0.5f)
            {
                uncaughtAtHorizon++;
            }

            if (minY < deepest)
            {
                deepest = minY;
                deepestTrace = trace;
            }
        }

        sb.AppendLine(
            $"  rimExitWorldY: tracked={rimTraces.Count} pastCanvasY({config.CanvasPlaneY:F1})={pastCanvas} " +
            $"uncaught@+{RimTraceHorizon}f={uncaughtAtHorizon} deepestMinWorldY={deepest:F4}");

        RimExitTracker first = rimTraces.Values.OrderBy(t => t.ExitFrame).First();
        AppendSingleRimTrace(sb, "firstRimExit", first);

        if (deepestTrace != null && deepestTrace.Index != first.Index)
        {
            AppendSingleRimTrace(sb, "deepestFall", deepestTrace);
        }

        RimExitTracker median = rimTraces.Values.OrderBy(t => t.ExitFrame).ElementAt(rimTraces.Count / 2);
        if (median.Index != first.Index && (deepestTrace == null || median.Index != deepestTrace.Index))
        {
            AppendSingleRimTrace(sb, "medianExitOrder", median);
        }
    }

    static void AppendSingleRimTrace(StringBuilder sb, string label, RimExitTracker trace)
    {
        int last = Mathf.Min(trace.SamplesFilled - 1, RimTraceHorizon);
        sb.AppendLine(
            $"    {label}: idx={trace.Index} exitF={trace.ExitFrame} exitWorldY={trace.ExitWorldY:F4} " +
            $"y@+10={SampleAt(trace, 10):F4} y@+30={SampleAt(trace, 30):F4} y@+60={SampleAt(trace, 60):F4} " +
            $"samples={last + 1}");
        sb.AppendLine(
            $"      worldY[0..9]: {string.Join(", ", Enumerable.Range(0, Mathf.Min(10, last + 1)).Select(i => trace.WorldY[i].ToString("F3")))}");
        if (last >= 10)
        {
            int start = Mathf.Max(0, last - 9);
            sb.AppendLine(
                $"      worldY[{start}..{last}]: {string.Join(", ", Enumerable.Range(start, last - start + 1).Select(i => trace.WorldY[i].ToString("F3")))}");
        }
    }

    static float SampleAt(RimExitTracker trace, int offset)
    {
        if (offset < trace.SamplesFilled)
        {
            return trace.WorldY[offset];
        }

        return float.NaN;
    }

    static void AppendSpillBufferReport(
        StringBuilder sb,
        HarmonicPipelineController pipeline,
        DiagConfig config,
        int spillFramesWithDelta,
        float spillRateSum)
    {
        sb.AppendLine(
            $"  spillBuffer: LastFallingQuantizeCount={pipeline.LastFallingQuantizeCount} " +
            $"LastFallingDebugCount={GetLastFallingDebugCount(pipeline)} " +
            $"LastCanvasHitCount={pipeline.LastCanvasHitCount} internalActive={GetCachedInternalCount(pipeline)}");

        if (config.SpillOverRim)
        {
            float avgSpill = spillFramesWithDelta > 0 ? spillRateSum / spillFramesWithDelta : 0f;
            sb.AppendLine(
                $"  spillOverRim=true: framesWithFallingDelta={spillFramesWithDelta} avgDeltaPerFrame={avgSpill:F2} " +
                $"(live lab spill.log ~15-50/frame into falling/quarantine when wired)");
        }
        else
        {
            sb.AppendLine("  spillOverRim=false: rim-exit particles remain in internal SOA (no falling transfer expected)");
        }
    }

    static uint GetLastFallingDebugCount(HarmonicPipelineController pipeline)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            "_lastFallingDebugCount", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (uint)field.GetValue(pipeline) : 0;
    }

    static uint GetCachedInternalCount(HarmonicPipelineController pipeline)
    {
        FieldInfo field = typeof(HarmonicPipelineController).GetField(
            "_cachedInternalCount", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? (uint)field.GetValue(pipeline) : 0;
    }

    static void RunSubstageTrace(ArgminResult beam, StringBuilder sb)
    {
        HarmonicPipelineController pipeline = CreatePipeline(beam.Config);
        Vector3 floorPivot = Vector3.zero;
        pipeline.SetContainerFluidOriented(
            floorPivot, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.TrySpawnContainerLatticeFill();

        Matrix4x4 prevL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, Quaternion.identity);
        Matrix4x4 w2lAtWorst = Matrix4x4.identity;

        for (int frame = 0; frame <= beam.GlobalMinFrame; frame++)
        {
            float t = frame * SpinDegPerFrame;
            Quaternion rot = Quaternion.Euler(t * 1.2f, t * 2.0f, t * 0.9f);
            Matrix4x4 currL2W = ContainerOrientedBounds.BuildLocalToWorld(floorPivot, rot);
            w2lAtWorst = currL2W.inverse;

            pipeline.SetContainerFluidOriented(
                floorPivot, rot, Radius, Height, 0.1f, 0.85f, 400f);
            pipeline.ApplyContainerRigidRotation(prevL2W, currL2W, DeltaTime);
            SyncGpu();

            if (frame < beam.GlobalMinFrame)
            {
                pipeline.ExecutePipelineFrame(DeltaTime);
                SyncGpu();
            }
        }

        if (!TryInvokeInstrumentedPbfTrace(pipeline, beam.GlobalMinIndex, w2lAtWorst, out string traceReport))
        {
            sb.AppendLine("  [SubstageTrace] Reflection invoke failed — see RigidCarrySpin_CornerPbfSubstage_Diagnostics manually.");
            return;
        }

        sb.AppendLine(traceReport);
        UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
    }

    static bool TryInvokeInstrumentedPbfTrace(
        HarmonicPipelineController pipeline,
        int particleIndex,
        Matrix4x4 worldToLocal,
        out string report)
    {
        report = string.Empty;
        Type testType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == "RigidCarrySpinHeadlessTests");
        if (testType == null)
        {
            return false;
        }

        MethodInfo instrumented = testType.GetMethod(
            "ExecuteInstrumentedPbfFrame",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (instrumented == null)
        {
            return false;
        }

        object traceObj = instrumented.Invoke(null, new object[] { pipeline, particleIndex, worldToLocal });
        if (traceObj == null)
        {
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"  [SubstageTrace] particle={particleIndex} stages:");
        bool populationB = false;

        foreach (object sample in (System.Collections.IEnumerable)traceObj)
        {
            Type st = sample.GetType();
            string stage = (string)st.GetField("Stage").GetValue(sample);
            Vector3 local = (Vector3)st.GetField("LocalPosition").GetValue(sample);
            float r = new Vector2(local.x, local.z).magnitude;
            bool outsideFp = r > Radius + RadiusEps;
            bool insideRigid = local.y >= -1e-4f && local.y <= Height + 1e-4f && r <= Radius + RadiusEps;
            bool participates = !outsideFp;

            if (!outsideFp && local.y < -1e-4f && r <= Radius + RadiusEps)
            {
                populationB = true;
            }

            sb.AppendLine(
                $"    {stage}: localY={local.y:F6} r={r:F6} outsideFp={outsideFp} " +
                $"insideRigid={insideRigid} participatesPbf={participates}");
        }

        sb.AppendLine($"  populationB_insideFootprint_subFloor_seenInTrace={populationB}");
        report = sb.ToString();
        return true;
    }

    static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
    }

    static string ClassifyTransition(float r, float localY)
    {
        if (r <= Radius + RadiusEps && localY < -1e-4f)
        {
            return "under-bucket";
        }

        if (r > Radius + RadiusEps && localY > Height - 0.05f)
        {
            return "rim-exit";
        }

        if (r > Radius + RadiusEps)
        {
            return "wall-exit";
        }

        return "other";
    }

    static IEnumerable<string> SampleRange(float[] arr, int start, int count)
    {
        for (int i = start; i < start + count && i < arr.Length; i++)
        {
            yield return float.IsNaN(arr[i]) ? "nan" : arr[i].ToString("F4");
        }
    }

    static HarmonicPipelineController CreatePipeline(DiagConfig config)
    {
        HarmonicPipelineController pipeline = CreatePipeline(config.Capacity, config.LatticeMax);
        SetField(pipeline, "cellSize", config.CellSize);
        pipeline.SetPbfIterations(config.PbfIters);
        pipeline.SetCanvasPlaneY(config.CanvasPlaneY);
        pipeline.SetCanvasCullingEnabled(config.CanvasCulling);
        pipeline.SetContainerSpillOverRim(config.SpillOverRim);
        return pipeline;
    }

    static HarmonicPipelineController CreatePipeline(int capacity, int latticeMax)
    {
        var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
        var carryShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ContainerRigidCarry.compute");

        var go = new GameObject("ScaleDivergenceDiag");
        var pipeline = go.AddComponent<HarmonicPipelineController>();
        SetField(pipeline, "containerRigidCarryShader", carryShader);
        SetField(pipeline, "perfDiagnosticsMuted", true);
        pipeline.ConfigureAndInitialize(
            settings.argumentUtilityShader,
            settings.spatialHashGridShader,
            settings.wcsphDensityShader,
            settings.dataCompactionShader,
            capacity,
            externalIngestion: true,
            autoRun: false,
            fallingShader: settings.fallingFluidWorldShader,
            eulerianShader: settings.eulerianDragGridShader,
            integrateShader: settings.wcsphIntegrationShader,
            pbfShader: settings.pbfSolverShader,
            radixShader: settings.radixSortShader);

        SetField(pipeline, "latticeSpawnMaxCount", latticeMax);
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        return pipeline;
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field?.SetValue(target, value);
    }

    static float GetContainerMaxTimeStep(HarmonicPipelineController pipeline)
    {
        FieldInfo otcField = typeof(HarmonicPipelineController).GetField(
            "openTopCylinder",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (otcField?.GetValue(pipeline) is OpenTopCylinderSettings otc)
        {
            return otc.maxTimeStep;
        }

        return 0.008f;
    }

    static void SyncGpu()
    {
        GraphicsFence fence = Graphics.CreateGraphicsFence(
            GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }
}
#endif
