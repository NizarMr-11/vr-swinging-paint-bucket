using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Per-frame CPU/GPU performance accumulator. Reset at Step start, finalized at Step end.
    /// Gated by <see cref="Enabled"/> (set from V4PipelineRoot.debugLogPerformance).
    /// </summary>
    public static class V4FramePerformanceCollector
    {
        public const int RollingAverageInterval = 60;
        public const int TopScopeCount = 6;

        public static bool Enabled { get; set; }
        public static bool GpuSyncEnabled { get; set; }
        public static bool ProfilerMarkersEnabled { get; set; }

        private static readonly Dictionary<string, double> CpuScopes = new Dictionary<string, double>(32);
        private static readonly Dictionary<string, double> GpuSyncScopes = new Dictionary<string, double>(32);
        private static readonly Dictionary<string, double> RollingCpuScopes = new Dictionary<string, double>(32);
        private static readonly Dictionary<string, double> RollingGpuSyncScopes = new Dictionary<string, double>(32);

        private static long _frameIndex;
        private static int _liveCount;
        private static double _frameCpuTotalMs;
        private static double _gpuFrameMs = double.NaN;
        private static double _cpuFrameMs = double.NaN;
        private static double _cpuMainThreadMs = double.NaN;
        private static string _debugFlags = string.Empty;
        private static int _rollingFrameCount;
        private static double _rollingCpuTotalMs;
        private static double _rollingGpuFrameMs;
        private static double _rollingCpuFrameMs;
        private static double _rollingCpuMainThreadMs;
        private static int _rollingGpuFrameSamples;

        public static void BeginFrame(long frameIndex, int liveCount)
        {
            if (!Enabled)
            {
                return;
            }

            _frameIndex = frameIndex;
            _liveCount = liveCount;
            _frameCpuTotalMs = 0d;
            _gpuFrameMs = double.NaN;
            _cpuFrameMs = double.NaN;
            _cpuMainThreadMs = double.NaN;
            _debugFlags = string.Empty;
            CpuScopes.Clear();
            GpuSyncScopes.Clear();
        }

        public static void SetDebugFlags(string debugFlags)
        {
            if (Enabled)
            {
                _debugFlags = debugFlags ?? string.Empty;
            }
        }

        public static void RecordCpuScope(string scopeName, double durationMs, bool contributesToTotal = false)
        {
            if (!Enabled || string.IsNullOrEmpty(scopeName) || durationMs < 0d)
            {
                return;
            }

            if (CpuScopes.TryGetValue(scopeName, out double existing))
            {
                CpuScopes[scopeName] = existing + durationMs;
            }
            else
            {
                CpuScopes[scopeName] = durationMs;
            }

            if (contributesToTotal)
            {
                _frameCpuTotalMs += durationMs;
            }
        }

        public static void RecordGpuSyncScope(string scopeName, double durationMs)
        {
            if (!Enabled || !GpuSyncEnabled || string.IsNullOrEmpty(scopeName) || durationMs < 0d)
            {
                return;
            }

            if (GpuSyncScopes.TryGetValue(scopeName, out double existing))
            {
                GpuSyncScopes[scopeName] = existing + durationMs;
            }
            else
            {
                GpuSyncScopes[scopeName] = durationMs;
            }
        }

        public static void SetFrameTimings(double gpuFrameMs, double cpuFrameMs, double cpuMainThreadMs)
        {
            if (!Enabled)
            {
                return;
            }

            _gpuFrameMs = gpuFrameMs;
            _cpuFrameMs = cpuFrameMs;
            _cpuMainThreadMs = cpuMainThreadMs;
        }

        public static void EndFrame()
        {
            if (!Enabled)
            {
                return;
            }

            V4Log.Info(V4LogCategory.Performance, BuildSummaryLine(isRollingAverage: false));

            AccumulateRollingTotals();

            _rollingFrameCount++;
            if (_rollingFrameCount >= RollingAverageInterval)
            {
                V4Log.Info(V4LogCategory.Performance, BuildSummaryLine(isRollingAverage: true));
                ResetRollingTotals();
            }
        }

        public static string FormatTopScopes(IReadOnlyDictionary<string, double> scopes, string label)
        {
            if (scopes == null || scopes.Count == 0)
            {
                return $"{label}=";
            }

            var ranked = new List<KeyValuePair<string, double>>(scopes);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));

            int count = Mathf.Min(TopScopeCount, ranked.Count);
            var sb = new StringBuilder(label);
            sb.Append('=');
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(ranked[i].Key);
                sb.Append(':');
                sb.Append(ranked[i].Value.ToString("F2"));
            }

            return sb.ToString();
        }

        private static string BuildSummaryLine(bool isRollingAverage)
        {
            if (isRollingAverage)
            {
                double frames = Math.Max(1, _rollingFrameCount);
                double avgCpu = _rollingCpuTotalMs / frames;
                double avgGpu = _rollingGpuFrameSamples > 0 ? _rollingGpuFrameMs / _rollingGpuFrameSamples : double.NaN;
                double avgCpuFrame = _rollingGpuFrameSamples > 0 ? _rollingCpuFrameMs / _rollingGpuFrameSamples : double.NaN;
                double avgCpuMain = _rollingGpuFrameSamples > 0 ? _rollingCpuMainThreadMs / _rollingGpuFrameSamples : double.NaN;

                return
                    $"avg frames={_rollingFrameCount} live={_liveCount} cpuTotalMs={avgCpu:F2} " +
                    $"gpuFrameMs={FormatOptional(avgGpu)} cpuFrameMs={FormatOptional(avgCpuFrame)} " +
                    $"cpuMainMs={FormatOptional(avgCpuMain)} " +
                    $"{FormatTopScopes(RollingCpuScopes, "topCpu")} " +
                    $"{FormatTopScopes(RollingGpuSyncScopes, "topGpuSync")} " +
                    $"debugFlags={_debugFlags}";
            }

            return
                $"frame={_frameIndex} live={_liveCount} cpuTotalMs={_frameCpuTotalMs:F2} " +
                $"gpuFrameMs={FormatOptional(_gpuFrameMs)} cpuFrameMs={FormatOptional(_cpuFrameMs)} " +
                $"cpuMainMs={FormatOptional(_cpuMainThreadMs)} " +
                $"{FormatTopScopes(CpuScopes, "topCpu")} " +
                $"{FormatTopScopes(GpuSyncScopes, "topGpuSync")} " +
                $"debugFlags={_debugFlags}";
        }

        private static void AccumulateRollingTotals()
        {
            _rollingCpuTotalMs += _frameCpuTotalMs;
            if (!double.IsNaN(_gpuFrameMs))
            {
                _rollingGpuFrameMs += _gpuFrameMs;
                _rollingCpuFrameMs += _cpuFrameMs;
                _rollingCpuMainThreadMs += _cpuMainThreadMs;
                _rollingGpuFrameSamples++;
            }

            MergeScopeTotals(CpuScopes, RollingCpuScopes);
            MergeScopeTotals(GpuSyncScopes, RollingGpuSyncScopes);
        }

        private static void MergeScopeTotals(
            IReadOnlyDictionary<string, double> frameScopes,
            Dictionary<string, double> rollingScopes)
        {
            foreach (KeyValuePair<string, double> pair in frameScopes)
            {
                if (rollingScopes.TryGetValue(pair.Key, out double existing))
                {
                    rollingScopes[pair.Key] = existing + pair.Value;
                }
                else
                {
                    rollingScopes[pair.Key] = pair.Value;
                }
            }
        }

        private static void ResetRollingTotals()
        {
            _rollingFrameCount = 0;
            _rollingCpuTotalMs = 0d;
            _rollingGpuFrameMs = 0d;
            _rollingCpuFrameMs = 0d;
            _rollingCpuMainThreadMs = 0d;
            _rollingGpuFrameSamples = 0;
            RollingCpuScopes.Clear();
            RollingGpuSyncScopes.Clear();
        }

        private static string FormatOptional(double value)
        {
            return double.IsNaN(value) ? "n/a" : value.ToString("F2");
        }
    }
}
