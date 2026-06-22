using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private uint _cachedInternalCount;
        private uint _lastFallingQuantizeCount;
        private uint _lastFallingDebugCount;
        private uint _lastCanvasHitCount;
        private uint _lastLoggedActive;
        private bool _hasLoggedActive;
        private int _framesSincePositionSample;
        private FluidParticle[] _diagSampleBuffer;
        private FluidParticle[] _seedParticles;
        private HarmonicSpawnRegion _defaultSpawnRegion;
        private Vector3 _lastBucketPosition;

        private static void PublishDiagnostic(
            HarmonicDiagnosticEventType type,
            string category,
            string message,
            int intArg0 = 0,
            int intArg1 = 0,
            bool boolArg0 = false)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            uint active = session.ReadActiveParticleCount();
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                type,
                category,
                message,
                session.FrameIndex,
                session.ElapsedSeconds,
                active,
                canvasHitCount: 0,
                intArg0: intArg0,
                intArg1: intArg1,
                boolArg0: boolArg0));
        }

        private void PublishPipelineFrameDiagnostic(uint activeCount)
        {
            FrameCompleted?.Invoke(new HarmonicFrameInfo(
                activeCount,
                _lastCanvasHitCount,
                _frameSortSize,
                containerFluid.enabled,
                worldFallingOnly));

            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            bool countChanged = !_hasLoggedActive || activeCount != _lastLoggedActive;
            bool periodic = frameDiagnosticInterval <= 1
                || (HarmonicDiagnosticHub.Session.FrameIndex % frameDiagnosticInterval) == 0;
            if (!countChanged && _lastCanvasHitCount == 0 && !periodic)
            {
                return;
            }

            _lastLoggedActive = activeCount;
            _hasLoggedActive = true;

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineFrameAfter,
                "PIPELINE",
                $"active={activeCount} canvasHits={_lastCanvasHitCount} worldOnly={worldFallingOnly} culling={canvasCullingEnabled}",
                session.FrameIndex,
                session.ElapsedSeconds,
                activeCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        private void PublishStageDiagnostic(string stage, string detail)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "ENGINE",
                $"stage={stage} {detail}",
                session.FrameIndex,
                session.ElapsedSeconds,
                _cachedInternalCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        /// <summary>
        /// Writes SPH perf telemetry to the Unity console and the sph.log channel.
        /// </summary>
        private void LogSphTelemetry(string message, bool warning = false)
        {
            if (muteSphTelemetry)
            {
                return;
            }

            if (logSphToConsole)
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

            PublishSphDiagnostic(message);
        }

        private void PublishSphDiagnostic(string message)
        {
            if (!HarmonicDiagnosticHub.Enabled || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                HarmonicDiagnosticEventType.PipelineStage,
                "SPH",
                message,
                session.FrameIndex,
                session.ElapsedSeconds,
                _cachedInternalCount,
                canvasHitCount: _lastCanvasHitCount));
        }

        private void MaybeSampleParticlePositions(ComputeBuffer buffer, uint activeCount, string stage)
        {
            if (positionSampleInterval <= 0 || buffer == null || activeCount == 0)
            {
                return;
            }

            if (++_framesSincePositionSample < positionSampleInterval)
            {
                return;
            }

            _framesSincePositionSample = 0;

            int sampleCount = Mathf.Min(positionSampleCount, (int)activeCount);
            if (sampleCount <= 0)
            {
                return;
            }

            if (_diagSampleBuffer == null || _diagSampleBuffer.Length < sampleCount)
            {
                _diagSampleBuffer = new FluidParticle[Mathf.Max(sampleCount, positionSampleCount)];
            }

            buffer.GetData(_diagSampleBuffer, 0, 0, sampleCount);

            float minY = float.MaxValue, maxY = float.MinValue, sumY = 0f, sumSpeed = 0f, maxSpeed = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float y = _diagSampleBuffer[i].Position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                sumY += y;
                float speed = math.length(_diagSampleBuffer[i].Velocity);
                sumSpeed += speed;
                maxSpeed = Mathf.Max(maxSpeed, speed);
            }

            float decayed = Mathf.Lerp(_estimatedMaxSpeed, maxSpeed, 0.3f);
            _estimatedMaxSpeed = Mathf.Max(decayed, 5f);

            if (perfDiagnosticsMuted
                || !HarmonicDiagnosticHub.Enabled
                || HarmonicDiagnosticHub.Session == null)
            {
                return;
            }

            float3 p0 = _diagSampleBuffer[0].Position;
            PublishStageDiagnostic(
                $"{stage}.sample",
                $"n={sampleCount} minY={minY:F2} maxY={maxY:F2} avgY={(sumY / sampleCount):F2} " +
                $"avgSpeed={(sumSpeed / sampleCount):F2} p0=({p0.x:F2},{p0.y:F2},{p0.z:F2})");
        }

        private int SpawnContainerLatticeFillInternal()
        {
            if (GetActiveParticleCount() > 0)
            {
                return 0;
            }

            if (!containerFluid.enabled)
            {
                SetContainerFluidEnabled(true);
            }

            useExternalParticleIngestion = true;
            seedTestParticlesOnStart = false;

            float spacing = cellSize;
            float fillTopY = containerFluid.floorY + (containerFluid.rimY - containerFluid.floorY) * 0.5f;
            float spawnRadius = containerFluid.radius * 0.9f;

            int spawned = HarmonicLatticeSpawner.SpawnContainerCylinderFill(
                this,
                containerFluid.center,
                containerFluid.floorY,
                fillTopY,
                spawnRadius,
                spacing,
                sphSolver.RestDensity,
                new Color(0.2f, 0.55f, 1f),
                Vector3.zero);

            if (spawned > 0)
            {
                simulationActive = true;
                _cachedInternalCount = GetActiveParticleCount();
                RecordRunSpawnInfo(new HarmonicRunSpawnInfo
                {
                    method = "lattice",
                    spawnCount = spawned,
                    spacing = spacing,
                    fillTopY = fillTopY,
                    spawnRadius = spawnRadius
                });
                PublishDiagnostic(
                    HarmonicDiagnosticEventType.ParticlesAppended,
                    "PIPELINE",
                    $"latticeSpawn={spawned} spacing={spacing:F4} fillTop={fillTopY:F2} radius={spawnRadius:F2}",
                    intArg0: spawned);
                Debug.Log($"[HarmonicPipeline] Lattice spawn: {spawned} particles (fillTop={fillTopY:F2}, radius={spawnRadius:F2}).");
            }
            else
            {
                Debug.LogWarning(
                    $"[HarmonicPipeline] Lattice spawn produced 0 particles " +
                    $"(floor={containerFluid.floorY:F2}, fillTop={fillTopY:F2}, radius={spawnRadius:F2}, spacing={spacing:F4}).");
            }

            return spawned;
        }

        private void SeedTestParticlesIfEmpty()
        {
            uint count = FetchActiveCount(_bufferInternalA);
            if (count > 0)
            {
                return;
            }

            int spawnCount = Mathf.Clamp(testParticleCount, 1, maxCapacity);
            _seedParticles ??= new FluidParticle[spawnCount];
            if (_seedParticles.Length < spawnCount)
            {
                _seedParticles = new FluidParticle[spawnCount];
            }

            var rng = new Unity.Mathematics.Random(0xC0FFEEu);
            for (int i = 0; i < spawnCount; i++)
            {
                float3 offset = rng.NextFloat3Direction() * rng.NextFloat(0f, testSpawnRadius);
                _seedParticles[i] = new FluidParticle
                {
                    Position = offset,
                    Velocity = float3.zero,
                    Density = sphSolver.RestDensity,
                    Pressure = 0f,
                    PackedColorRGBA = 0xFFFFFFFFu
                };
            }

            _bufferInternalA.SetData(_seedParticles, 0, 0, spawnCount);
            _bufferInternalA.SetCounterValue((uint)spawnCount);
            _bufferInternalB.SetCounterValue(0);
            _cachedInternalCount = (uint)spawnCount;
            RecordRunSpawnInfo(new HarmonicRunSpawnInfo
            {
                method = "seedTest",
                spawnCount = spawnCount,
                spacing = testSpawnRadius
            });
        }
    }
}
