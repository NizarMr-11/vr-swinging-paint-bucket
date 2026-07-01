using HarmonicEngine.Diagnostics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class HarmonicPipelineController
    {
        private const float PbfConvergeWindowSeconds = 2f;
        private const int PbfConvergeFrameInterval = 5;

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

        private bool TrySamplePbfScalars(
            float epsilon,
            float rho0,
            out GpuParticleReadbackUtility.ScalarStats constraintStats,
            out GpuParticleReadbackUtility.ScalarStats lambdaStats,
            out GpuParticleReadbackUtility.ScalarStats gradSqStats,
            out GpuParticleReadbackUtility.ScalarStats gradSqImpliedStats)
        {
            constraintStats = default;
            lambdaStats = default;
            gradSqStats = default;
            gradSqImpliedStats = default;

            if (perfDiagnosticsMuted
                || _bufferDensityCacheDensities == null
                || _pbfScratch?.Lambdas == null
                || _pbfScratch.GradSqSum == null
                || _cachedInternalCount == 0)
            {
                return false;
            }

            int sampleCount = Mathf.Min(positionSampleCount, (int)_cachedInternalCount);
            int[] indices = BuildStratifiedSampleIndices((int)_cachedInternalCount, sampleCount);
            float[] densities = GpuParticleReadbackUtility.ReadFloatBufferSample(
                _bufferDensityCacheDensities,
                indices);
            float[] lambdas = GpuParticleReadbackUtility.ReadFloatBufferSample(
                _pbfScratch.Lambdas,
                indices);
            float[] gradSq = GpuParticleReadbackUtility.ReadFloatBufferSample(
                _pbfScratch.GradSqSum,
                indices);

            constraintStats = GpuParticleReadbackUtility.ComputePbfConstraintStats(densities, rho0);
            lambdaStats = GpuParticleReadbackUtility.ComputeScalarStats(lambdas, useAbs: true);
            gradSqStats = GpuParticleReadbackUtility.ComputeScalarStats(gradSq);
            gradSqImpliedStats = GpuParticleReadbackUtility.ComputePbfGradSqImpliedStats(
                densities,
                lambdas,
                rho0,
                epsilon);
            return true;
        }

        private void LogPbfScalarTelemetry(float epsilon, float rho0)
        {
            if (!TrySamplePbfScalars(
                    epsilon,
                    rho0,
                    out GpuParticleReadbackUtility.ScalarStats constraintStats,
                    out GpuParticleReadbackUtility.ScalarStats lambdaStats,
                    out GpuParticleReadbackUtility.ScalarStats gradSqStats,
                    out GpuParticleReadbackUtility.ScalarStats gradSqImpliedStats))
            {
                return;
            }

            LogPbfTelemetry(
                $"[PBF CONSTRAINT] avgC={constraintStats.Avg:F4} maxC={constraintStats.Max:F4} minC={constraintStats.Min:F4}");
            LogPbfTelemetry(
                $"[PBF LAMBDA] avgAbsLambda={lambdaStats.Avg:F6} maxAbsLambda={lambdaStats.Max:F6}");
            LogPbfTelemetry(
                $"[PBF GRADSQ] avgGradSq={gradSqStats.Avg:F4} maxGradSq={gradSqStats.Max:F4} minGradSq={gradSqStats.Min:F4}");
            LogPbfTelemetry(
                $"[PBF GRADSQ_IMPLIED] avgGradSq={gradSqImpliedStats.Avg:F4} " +
                $"(approximate, from per-particle C and lambda)");
        }

        private void MaybeLogPbfConvergenceInternal()
        {
            if (!openTopCylinderUsePbf
                || mutePbfTelemetry
                || !HarmonicDiagnosticHub.Enabled
                || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            if (session.ElapsedSeconds >= PbfConvergeWindowSeconds
                || session.FrameIndex % PbfConvergeFrameInterval != 0)
            {
                return;
            }

            float h = SmoothingRadius;
            float epsilon = ResolvePbfEpsilon(h);
            float rho0 = sphSolver.RestDensity;

            if (!TrySamplePbfScalars(
                    epsilon,
                    rho0,
                    out GpuParticleReadbackUtility.ScalarStats constraintStats,
                    out GpuParticleReadbackUtility.ScalarStats lambdaStats,
                    out GpuParticleReadbackUtility.ScalarStats gradSqStats,
                    out _))
            {
                return;
            }

            float avgY = _hasPbfColumnSample ? _pbfColumnAvgY : 0f;
            LogPbfTelemetry(
                $"[PBF CONVERGE] frame={session.FrameIndex} avgC={constraintStats.Avg:F4} " +
                $"avgAbsLambda={lambdaStats.Avg:F6} avgGradSq={gradSqStats.Avg:F4} avgY={avgY:F2}");
        }

        private void MaybeLogPbfTelemetryInternal(float deltaTime, int substeps, float subDt)
        {
            if (!openTopCylinderUsePbf || mutePbfTelemetry)
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
            float epsilon = ResolvePbfEpsilon(h);
            float rho0 = sphSolver.RestDensity;

            LogPbfTelemetry(
                $"[PBF FRAME] active={_cachedInternalCount} substeps={substeps} pbfIters={pbfIterations} " +
                $"relaxation={pbfRelaxation:F2} velDamp={pbfVelocityDamping:F2} maxDelta={pbfMaxPositionDelta:F3} " +
                $"cohesion={pbfCohesion:F3} " +
                $"h={h:F4} epsilon={epsilon:F2} rho0={rho0:F1} " +
                $"subDt={subDt:F6} sortSize={_frameSortSize} sortAlgo={(UseRadixSort ? "radix" : "bitonic")}");

            FlushSortTelemetry("PBF");

            LogPbfScalarTelemetry(epsilon, rho0);

            if (_hasPbfColumnSample)
            {
                LogPbfTelemetry(
                    $"[PBF COLUMN] minY={_pbfColumnMinY:F2} maxY={_pbfColumnMaxY:F2} avgY={_pbfColumnAvgY:F2} " +
                    $"avgSpeed={_pbfColumnAvgSpeed:F2} maxSpeed={_pbfColumnMaxSpeed:F2}");
            }
        }
    }
}
