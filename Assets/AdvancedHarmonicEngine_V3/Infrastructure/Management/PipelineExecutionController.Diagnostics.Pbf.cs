using HarmonicEngine.Diagnostics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private float _pbfLogAccumulator;
        private bool _hasPbfColumnSample;
        private float _pbfColumnMinY;
        private float _pbfColumnMaxY;
        private float _pbfColumnAvgY;
        private float _pbfColumnAvgSpeed;
        private float _pbfColumnMaxSpeed;

        public bool TryGetPbfScratchBuffers(out PbfScratchBuffers scratch)
        {
            scratch = _pbfScratch;
            return scratch != null;
        }

        private void StorePbfColumnSample(
            float minY,
            float maxY,
            float avgY,
            float avgSpeed,
            float maxSpeed)
        {
            _hasPbfColumnSample = true;
            _pbfColumnMinY = minY;
            _pbfColumnMaxY = maxY;
            _pbfColumnAvgY = avgY;
            _pbfColumnAvgSpeed = avgSpeed;
            _pbfColumnMaxSpeed = maxSpeed;
        }

        private void LogPbfTelemetry(string message, bool warning = false)
        {
            if (mutePbfTelemetry)
            {
                return;
            }

            if (logPbfToConsole)
            {
                if (warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }

            PublishPbfDiagnostic(message);
        }

        private void PublishPbfDiagnostic(string message)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "PBF",
                message,
                session.FrameIndex,
                session.ElapsedSeconds,
                _cachedInternalCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        private void MaybeLogPbfTelemetry(float deltaTime, int substeps, float subDt)
        {
            if (!usePBF || mutePbfTelemetry)
            {
                return;
            }

            _pbfLogAccumulator += deltaTime;
            if (_pbfLogAccumulator < 1f)
            {
                return;
            }

            _pbfLogAccumulator = 0f;

            float h = SmoothingRadius;
            float epsilon = 600f / (h * h);
            float rho0 = sphSolver.RestDensity;

            LogPbfTelemetry(
                $"[PBF FRAME] active={_cachedInternalCount} substeps={substeps} pbfIters={pbfIterations} " +
                $"h={h:F4} epsilon={epsilon:F1} rho0={rho0:F1} subDt={subDt:F6} sortSize={_frameSortSize}");

            if (!perfDiagnosticsMuted
                && _bufferDensityCacheDensities != null
                && _pbfScratch?.Lambdas != null
                && _cachedInternalCount > 0)
            {
                int sampleCount = Mathf.Min(positionSampleCount, (int)_cachedInternalCount);
                int[] indices = BuildStratifiedSampleIndices((int)_cachedInternalCount, sampleCount);
                float[] densities = GpuParticleReadbackUtility.ReadFloatBufferSample(
                    _bufferDensityCacheDensities,
                    indices);
                float[] lambdas = GpuParticleReadbackUtility.ReadFloatBufferSample(
                    _pbfScratch.Lambdas,
                    indices);

                GpuParticleReadbackUtility.ScalarStats constraintStats =
                    GpuParticleReadbackUtility.ComputePbfConstraintStats(densities, rho0);
                LogPbfTelemetry(
                    $"[PBF CONSTRAINT] avgC={constraintStats.Avg:F4} maxC={constraintStats.Max:F4} minC={constraintStats.Min:F4}");

                GpuParticleReadbackUtility.ScalarStats lambdaStats =
                    GpuParticleReadbackUtility.ComputeScalarStats(lambdas, useAbs: true);
                LogPbfTelemetry(
                    $"[PBF LAMBDA] avgAbsLambda={lambdaStats.Avg:F4} maxAbsLambda={lambdaStats.Max:F4}");
            }

            if (_hasPbfColumnSample)
            {
                LogPbfTelemetry(
                    $"[PBF COLUMN] minY={_pbfColumnMinY:F2} maxY={_pbfColumnMaxY:F2} avgY={_pbfColumnAvgY:F2} " +
                    $"avgSpeed={_pbfColumnAvgSpeed:F2} maxSpeed={_pbfColumnMaxSpeed:F2}");
            }
        }
    }
}
