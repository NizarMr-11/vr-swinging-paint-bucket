using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Diagnostics.Aspects
{
    public sealed class PerformanceTelemetryAspect : IHarmonicDiagnosticAspect
    {
        public string AspectName => "PerformanceTelemetry";
        public int Order => 5;

        private HarmonicPipelineDiagnosticsSettings _settings;
        private HarmonicDiagnosticSession _session;
        private int _framesSinceLog;
        private bool _frameTimingEnabled;

        private ProfilerRecorder _gridRecorder;
        private ProfilerRecorder _sortRecorder;
        private ProfilerRecorder _radixSortRecorder;
        private ProfilerRecorder _buildRangesRecorder;
        private ProfilerRecorder _densityRecorder;
        private ProfilerRecorder _integrationRecorder;
        private ProfilerRecorder _pbfPredictRecorder;
        private ProfilerRecorder _pbfDensityRecorder;
        private ProfilerRecorder _pbfLambdaRecorder;
        private ProfilerRecorder _pbfSolveRecorder;
        private ProfilerRecorder _pbfApplyRecorder;
        private ProfilerRecorder _containerRecorder;
        private ProfilerRecorder _worldFallingRecorder;
        private ProfilerRecorder _ssfrRecorder;

        public void Configure(HarmonicPipelineDiagnosticsSettings settings) => _settings = settings;

        public void OnAttach(HarmonicDiagnosticSession session)
        {
            _session = session;
            _framesSinceLog = 0;
            _frameTimingEnabled = _settings.enableFrameTimingStats && FrameTimingManager.IsFeatureEnabled();

            _gridRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SpatialHashGrid");
            _sortRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.BitonicSort");
            _radixSortRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.RadixSort");
            _buildRangesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.BuildRanges");
            _densityRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphDensity");
            _integrationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SphIntegration");
            _pbfPredictRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.PbfPredict");
            _pbfDensityRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.PbfDensity");
            _pbfLambdaRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.PbfLambda");
            _pbfSolveRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.PbfSolve");
            _pbfApplyRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.PbfApply");
            _containerRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.ContainerFluidFrame");
            _worldFallingRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.WorldFalling");
            _ssfrRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Harmonic.SSFR");
        }

        public void OnDetach()
        {
            _session = null;
            DisposeRecorder(ref _gridRecorder);
            DisposeRecorder(ref _sortRecorder);
            DisposeRecorder(ref _radixSortRecorder);
            DisposeRecorder(ref _buildRangesRecorder);
            DisposeRecorder(ref _densityRecorder);
            DisposeRecorder(ref _integrationRecorder);
            DisposeRecorder(ref _pbfPredictRecorder);
            DisposeRecorder(ref _pbfDensityRecorder);
            DisposeRecorder(ref _pbfLambdaRecorder);
            DisposeRecorder(ref _pbfSolveRecorder);
            DisposeRecorder(ref _pbfApplyRecorder);
            DisposeRecorder(ref _containerRecorder);
            DisposeRecorder(ref _worldFallingRecorder);
            DisposeRecorder(ref _ssfrRecorder);
        }

        public void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent)
        {
            if (!_settings.enableProfileTelemetry
                || diagnosticEvent.Type != HarmonicDiagnosticEventType.PipelineFrameAfter)
            {
                return;
            }

            if (_frameTimingEnabled)
            {
                FrameTimingManager.CaptureFrameTimings();
            }

            float frameMs = Time.unscaledDeltaTime * 1000f;
            bool spike = frameMs >= _settings.spikeThresholdMs;
            _framesSinceLog++;

            int interval = Mathf.Max(1, _settings.profileLogInterval);
            if (!spike && _framesSinceLog < interval)
            {
                return;
            }

            _framesSinceLog = 0;
            PublishPerfLine(diagnosticEvent, frameMs, spike);
        }

        private void PublishPerfLine(in HarmonicDiagnosticEvent source, float frameMs, bool spike)
        {
            if (_session == null)
            {
                return;
            }

            var sb = new StringBuilder(256);
            sb.Append(spike ? "spike=1 " : "spike=0 ");
            sb.Append($"frameMs={frameMs:F2}");

            if (_frameTimingEnabled && FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
            {
                FrameTiming timing = _frameTimings[0];
                sb.Append($" gpuMs={timing.gpuFrameTime:F2} cpuMainMs={timing.cpuFrameTime:F2}");
            }

            sb.Append($" gridMs={ToMs(_gridRecorder):F3}");
            bool usingRadix = _session.Pipeline != null && _session.Pipeline.UseRadixSort;
            sb.Append(usingRadix ? " sortAlgo=radix" : " sortAlgo=bitonic");
            sb.Append($" radixSortMs={ToMs(_radixSortRecorder):F3}");
            sb.Append($" bitonicSortMs={ToMs(_sortRecorder):F3}");
            sb.Append($" rangesMs={ToMs(_buildRangesRecorder):F3}");
            bool openTopCylinderUsePbf = _session.Pipeline != null && _session.Pipeline.UsePbf;
            if (openTopCylinderUsePbf)
            {
                sb.Append($" densityMs={ToMs(_pbfDensityRecorder):F3}");
                sb.Append($" integrationMs={ToMs(_pbfSolveRecorder):F3}");
                sb.Append($" pbfPredictMs={ToMs(_pbfPredictRecorder):F3}");
                sb.Append($" pbfLambdaMs={ToMs(_pbfLambdaRecorder):F3}");
                sb.Append($" pbfApplyMs={ToMs(_pbfApplyRecorder):F3}");
            }
            else
            {
                sb.Append($" densityMs={ToMs(_densityRecorder):F3}");
                sb.Append($" integrationMs={ToMs(_integrationRecorder):F3}");
            }

            sb.Append($" containerMs={ToMs(_containerRecorder):F3}");
            sb.Append($" worldMs={ToMs(_worldFallingRecorder):F3}");
            sb.Append($" ssfrMs={ToMs(_ssfrRecorder):F3}");

            uint active = source.ActiveParticleCount;
            int sortSize = _session.Pipeline?.FrameSortSize ?? 0;
            int lastRadixDispatches = _session.Pipeline?.LastRadixSortDispatchCount ?? 0;
            sb.Append($" active={active} sortSize={sortSize} radixDispatches={lastRadixDispatches}");

            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "PERF",
                sb.ToString(),
                source.FrameIndex,
                source.TimeSeconds,
                active,
                source.PeakParticleCount,
                source.CanvasHitCount,
                spike ? 1 : 0));
        }

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private static double ToMs(ProfilerRecorder recorder) =>
            recorder.Valid ? recorder.LastValue * 1e-6 : 0.0;

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
            {
                recorder.Dispose();
            }

            recorder = default;
        }
    }
}
