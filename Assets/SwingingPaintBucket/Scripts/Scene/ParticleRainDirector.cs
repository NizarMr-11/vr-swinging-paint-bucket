using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using SwingingPaintBucket.Canvas;
using Unity.Mathematics;
using UnityEngine;

namespace SwingingPaintBucket.Scene
{
    /// <summary>
    /// Countdown, then gradual world-space particle rain (no container).
    /// </summary>
    public class ParticleRainDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private CanvasController canvas;

        [Header("Countdown")]
        [SerializeField] private float countdownSeconds = 3f;
        [SerializeField] private bool autoStartOnPlay = true;

        [Header("Canvas / floor")]
        [Tooltip("Hide the paint canvas and treat the plane as a solid floor so particles fall, rest and stay visible instead of being consumed.")]
        [SerializeField] private bool disableCanvas = true;
        [Tooltip("World Y of the floor particles rest on (no-canvas mode). Should be well below the spawn height.")]
        [SerializeField] private float floorY = -4f;

        [Header("Spawn volume (world space)")]
        [Tooltip("Spawn well above the floor so the rain is clearly visible falling into view.")]
        [SerializeField] private Vector3 spawnCenter = new(0f, 10f, 0f);
        [SerializeField] private Vector3 spawnHalfExtents = new(1.2f, 0.15f, 1.2f);
        [SerializeField] private int initialParticleCount = 4096;
        [SerializeField] private float restDensity = 1000f;
        [SerializeField] private Vector2 horizontalVelocityRange = new(-0.15f, 0.15f);

        [Header("Gradual spawn (fixes 0 → 4096 jump)")]
        [SerializeField] private bool rampInitialSpawn = true;
        [SerializeField, Min(0.1f)] private float initialSpawnDuration = 2f;
        [SerializeField, Min(1)] private int initialSpawnChunkSize = 256;

        [Header("Continuous drizzle")]
        [SerializeField] private float spawnBurstPerSecond;
        [SerializeField] private int burstSize = 128;

        [Header("UI")]
        [SerializeField] private int countdownFontSize = 72;

        private float _countdownRemaining;
        private bool _countdownFinished;
        private bool _simulationStarted;
        private bool _rampActive;
        private int _rampParticlesRemaining;
        private float _rampChunkTimer;
        private float _burstAccumulator;
        private FluidParticle[] _spawnBatch;
        private uint _randomState = 0xA14A5123u;
        private int _lastCountdownDisplay = -1;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<CanvasController>();
            }

            if (pipeline != null)
            {
                pipeline.SetWorldFallingOnly(true);
                pipeline.EnableExternalIngestion(true);
                pipeline.SetSimulationActive(false);
                pipeline.ClearAllParticles();

                if (disableCanvas)
                {
                    // No canvas: the plane becomes a solid floor below the spawn so particles
                    // keep falling, land and stay visible (not consumed into the canvas buffer).
                    pipeline.SetCanvasCullingEnabled(false);
                    pipeline.SetCanvasPlaneY(floorY);

                    if (canvas != null)
                    {
                        canvas.gameObject.SetActive(false);
                    }
                }
                else if (canvas != null)
                {
                    pipeline.SetCanvasCullingEnabled(true);
                    pipeline.SetCanvasPlaneY(canvas.transform.position.y);
                }
            }
        }

        private void Start()
        {
            if (!autoStartOnPlay)
            {
                return;
            }

            BeginCountdown();
        }

        public void BeginCountdown()
        {
            _countdownRemaining = Mathf.Max(0f, countdownSeconds);
            _countdownFinished = _countdownRemaining <= 0f;
            _simulationStarted = false;
            _rampActive = false;
            _rampParticlesRemaining = 0;
            _burstAccumulator = 0f;
            _lastCountdownDisplay = -1;
            pipeline?.ClearAllParticles();
            pipeline?.SetSimulationActive(false);
            Publish(HarmonicDiagnosticEventType.SessionStart, "RAIN", $"countdownBegin seconds={countdownSeconds}");
        }

        private void Update()
        {
            if (!_countdownFinished)
            {
                int display = Mathf.Max(1, Mathf.CeilToInt(_countdownRemaining));
                if (display != _lastCountdownDisplay)
                {
                    _lastCountdownDisplay = display;
                    Publish(
                        HarmonicDiagnosticEventType.CountdownTick,
                        "RAIN",
                        $"countdown={display} active={pipeline?.GetActiveParticleCount() ?? 0}");
                }

                _countdownRemaining -= Time.deltaTime;
                if (_countdownRemaining <= 0f)
                {
                    _countdownFinished = true;
                    StartParticleRain();
                }

                return;
            }

            if (_rampActive)
            {
                TickRampSpawn();
            }

            if (!_simulationStarted || pipeline == null || spawnBurstPerSecond <= 0f)
            {
                return;
            }

            _burstAccumulator += spawnBurstPerSecond * Time.deltaTime;
            while (_burstAccumulator >= 1f)
            {
                _burstAccumulator -= 1f;
                SpawnBurst(burstSize);
            }
        }

        private void StartParticleRain()
        {
            if (pipeline == null)
            {
                return;
            }

            pipeline.SetWorldFallingOnly(true);
            pipeline.SetSimulationActive(true);
            _simulationStarted = true;

            if (rampInitialSpawn && initialSpawnDuration > 0f && initialParticleCount > 0)
            {
                _rampActive = true;
                _rampParticlesRemaining = initialParticleCount;
                _rampChunkTimer = 0f;
                Publish(
                    HarmonicDiagnosticEventType.RainStart,
                    "RAIN",
                    $"rampStart total={initialParticleCount} duration={initialSpawnDuration} chunk={initialSpawnChunkSize}");
            }
            else
            {
                Publish(HarmonicDiagnosticEventType.RainStart, "RAIN", $"instantStart total={initialParticleCount}");
                SpawnBurst(initialParticleCount);
            }
        }

        private void TickRampSpawn()
        {
            if (_rampParticlesRemaining <= 0)
            {
                _rampActive = false;
                Publish(HarmonicDiagnosticEventType.RainStart, "RAIN", "rampComplete");
                return;
            }

            float chunkInterval = initialSpawnDuration / Mathf.Max(1, Mathf.CeilToInt((float)initialParticleCount / initialSpawnChunkSize));
            _rampChunkTimer += Time.deltaTime;

            while (_rampChunkTimer >= chunkInterval && _rampParticlesRemaining > 0)
            {
                _rampChunkTimer -= chunkInterval;
                int chunk = Mathf.Min(initialSpawnChunkSize, _rampParticlesRemaining);
                SpawnBurst(chunk);
                _rampParticlesRemaining -= chunk;
            }
        }

        private void SpawnBurst(int count)
        {
            int spawnCount = Mathf.Clamp(count, 0, pipeline.MaxCapacity - (int)pipeline.GetActiveParticleCount());
            if (spawnCount <= 0)
            {
                Publish(HarmonicDiagnosticEventType.SpawnBurst, "RAIN", $"spawnSkipped requested={count}");
                return;
            }

            _spawnBatch ??= new FluidParticle[spawnCount];
            if (_spawnBatch.Length < spawnCount)
            {
                _spawnBatch = new FluidParticle[spawnCount];
            }

            for (int i = 0; i < spawnCount; i++)
            {
                float3 pos = RandomInSpawnBox();
                var vel = new Vector3(
                    RandomRange(horizontalVelocityRange.x, horizontalVelocityRange.y),
                    0f,
                    RandomRange(horizontalVelocityRange.x, horizontalVelocityRange.y));
                _spawnBatch[i] = FluidParticleFactory.FromWorldPosition(pos, vel, restDensity);
            }

            int appended = pipeline.AppendParticles(_spawnBatch, spawnCount);
            uint total = pipeline.GetActiveParticleCount();
            Publish(
                HarmonicDiagnosticEventType.SpawnBurst,
                "RAIN",
                $"requested={count} appended={appended} total={total}",
                intArg0: appended,
                intArg1: count);
        }

        private static void Publish(
            HarmonicDiagnosticEventType type,
            string category,
            string message,
            int intArg0 = 0,
            int intArg1 = 0)
        {
            if (!HarmonicDiagnosticHub.Enabled)
            {
                return;
            }

            var session = HarmonicDiagnosticHub.Session;
            float t = session?.ElapsedSeconds ?? 0f;
            int frame = session?.FrameIndex ?? 0;
            uint active = session?.ReadActiveParticleCount() ?? 0;
            HarmonicDiagnosticHub.Publish(new HarmonicDiagnosticEvent(
                type, category, message, frame, t, active, intArg0: intArg0, intArg1: intArg1));
        }

        private float3 RandomInSpawnBox()
        {
            return new float3(
                spawnCenter.x + RandomRange(-spawnHalfExtents.x, spawnHalfExtents.x),
                spawnCenter.y + RandomRange(-spawnHalfExtents.y, spawnHalfExtents.y),
                spawnCenter.z + RandomRange(-spawnHalfExtents.z, spawnHalfExtents.z));
        }

        private float RandomRange(float min, float max)
        {
            _randomState = _randomState * 747796405u + 2891336453u;
            float t = (_randomState & 0x00FFFFFFu) / 16777215f;
            return Mathf.Lerp(min, max, t);
        }

        private void OnGUI()
        {
            if (_countdownFinished)
            {
                return;
            }

            int display = Mathf.Max(1, Mathf.CeilToInt(_countdownRemaining));
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = countdownFontSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), display.ToString(), style);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawWireCube(spawnCenter, spawnHalfExtents * 2f);
        }
    }
}
