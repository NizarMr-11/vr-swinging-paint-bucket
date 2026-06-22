using System.IO;
using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using Unity.Mathematics;
using UnityEngine;

namespace SwingingPaintBucket.Simulation
{
    /// <summary>
    /// Drives the "YouTube" workflow. On load it asks the user for a particle count, a duration,
    /// and whether to save the calculation:
    /// <list type="bullet">
    /// <item>Save ON  — every frame is recorded to a tape that can be scrubbed/walked through
    /// while it is still calculating (like seeking a video that is still buffering), then saved
    /// to disk and replayed in full.</item>
    /// <item>Save OFF — the simulation simply runs live for the duration; no recording, scrubber
    /// or saved file.</item>
    /// </list>
    /// </summary>
    public class SimulationTimelineDirector : MonoBehaviour
    {
        private enum Phase
        {
            Config,
            LiveOnly,
            Timeline
        }

        [Header("Engine references")]
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private ParticleSpawnVolume shapeEmitter;
        [Tooltip("When enabled, every ParticleSpawnVolume in the scene is spawned (priority order) when a run starts.")]
        [SerializeField] private bool spawnAllSceneVolumes = true;
        [SerializeField] private SimulationTimelineRenderer timelineRenderer;
        [SerializeField] private HarmonicParticleDebugRenderer liveDebugRenderer;

        [Header("Defaults shown in the config prompt")]
        [SerializeField] private int defaultParticleCount = 4096;
        [SerializeField] private float defaultDurationSeconds = 6f;
        [SerializeField] private bool defaultSaveCalculation = true;
        [Tooltip("Skip the prompt and immediately start with the default values (kiosk / VR auto-run).")]
        [SerializeField] private bool autoBeginOnStart;
        [SerializeField, Min(1f)] private float captureFps = 30f;
        [SerializeField, Min(1)] private int maxRecordedFrames = 3600;

        [Header("Saved capture")]
        [SerializeField] private string captureSubDirectory = "HarmonicCaptures";

        private Phase _phase = Phase.Config;
        private SimulationCaptureStore _store;
        private FluidParticle[] _readback;
        private float3[] _positionScratch;

        private string _particleCountText;
        private string _durationText;
        private bool _saveCalculation;
        private int _requestedParticles;
        private float _requestedDuration;

        private float _recordElapsed;
        private float _captureAccumulator;
        private string _savedPath = string.Empty;

        // Timeline (save) state.
        private bool _calculating;
        private bool _followLive;
        private bool _isPlaying;
        private float _playhead;
        private int _lastDisplayedFrame = -1;

        // Live-only state.
        private bool _liveFinished;

        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (shapeEmitter == null)
            {
                shapeEmitter = FindFirstObjectByType<ParticleSpawnVolume>();
            }

            if (timelineRenderer == null)
            {
                timelineRenderer = FindFirstObjectByType<SimulationTimelineRenderer>();
            }

            if (liveDebugRenderer == null)
            {
                liveDebugRenderer = FindFirstObjectByType<HarmonicParticleDebugRenderer>();
            }

            _particleCountText = defaultParticleCount.ToString();
            _durationText = defaultDurationSeconds.ToString("0.#");
            _saveCalculation = defaultSaveCalculation;
        }

        private void Start()
        {
            EnterConfig();
            if (autoBeginOnStart)
            {
                BeginRun();
            }
        }

        /// <summary>Public entry point so VR UI / external triggers can start the run.</summary>
        public void BeginCalculation() => BeginRun();

        private void Update()
        {
            switch (_phase)
            {
                case Phase.LiveOnly:
                    TickLiveOnly();
                    break;
                case Phase.Timeline:
                    TickTimeline();
                    break;
            }
        }

        // ---- Config phase ------------------------------------------------------

        private void EnterConfig()
        {
            _phase = Phase.Config;
            if (pipeline != null)
            {
                pipeline.SetWorldFallingOnly(true);
                pipeline.EnableExternalIngestion(true);
                pipeline.SetCanvasCullingEnabled(false);
                pipeline.SetSimulationActive(false);
                pipeline.ClearAllParticles();
            }

            timelineRenderer?.Hide();
            if (liveDebugRenderer != null)
            {
                liveDebugRenderer.enabled = true;
            }
        }

        private void BeginRun()
        {
            if (pipeline == null)
            {
                Debug.LogWarning("[SimulationTimelineDirector] No pipeline; cannot start.");
                return;
            }

            _requestedParticles = Mathf.Clamp(
                ParseInt(_particleCountText, defaultParticleCount), 1, pipeline.MaxCapacity);
            _requestedDuration = Mathf.Max(0.1f, ParseFloat(_durationText, defaultDurationSeconds));

            _recordElapsed = 0f;
            _captureAccumulator = 0f;

            pipeline.SetWorldFallingOnly(true);
            pipeline.EnableExternalIngestion(true);
            pipeline.SetCanvasCullingEnabled(false);
            pipeline.ClearAllParticles();

            if (shapeEmitter != null)
            {
                shapeEmitter.PrepareRun(_requestedParticles);
            }

            if (spawnAllSceneVolumes)
            {
                HarmonicParticleSpawnCoordinator.SpawnAll(
                    pipeline,
                    clearFirst: false,
                    activateSimulation: true);
            }
            else if (shapeEmitter != null)
            {
                shapeEmitter.Emit();
            }
            else
            {
                pipeline.SetSimulationActive(true);
            }

            if (_saveCalculation)
            {
                // Scrub-while-calculating: drive the view from the captured tape only.
                _store = new SimulationCaptureStore(captureFps);
                _calculating = true;
                _followLive = true;
                _isPlaying = true;
                _playhead = 0f;
                _lastDisplayedFrame = -1;
                if (liveDebugRenderer != null)
                {
                    liveDebugRenderer.enabled = false;
                }

                timelineRenderer?.Hide();
                _phase = Phase.Timeline;
            }
            else
            {
                // Run-only: just show the live simulation, no tape.
                _store = null;
                _liveFinished = false;
                if (liveDebugRenderer != null)
                {
                    liveDebugRenderer.enabled = true;
                }

                timelineRenderer?.Hide();
                _phase = Phase.LiveOnly;
            }
        }

        // ---- Live-only (no save) ----------------------------------------------

        private void TickLiveOnly()
        {
            if (_liveFinished)
            {
                return;
            }

            _recordElapsed += Time.deltaTime;
            if (_recordElapsed >= _requestedDuration)
            {
                _recordElapsed = _requestedDuration;
                _liveFinished = true;
                pipeline.SetSimulationActive(false);
            }
        }

        // ---- Timeline (save: calculate + scrub) -------------------------------

        private void TickTimeline()
        {
            if (pipeline == null || _store == null)
            {
                return;
            }

            if (_calculating)
            {
                _recordElapsed += Time.deltaTime;
                _captureAccumulator += Time.deltaTime;
                float interval = 1f / Mathf.Max(1f, captureFps);
                if (_captureAccumulator >= interval)
                {
                    _captureAccumulator -= interval;
                    CaptureFrame();
                }

                if (_recordElapsed >= _requestedDuration || _store.FrameCount >= maxRecordedFrames)
                {
                    FinishCalculation();
                }
            }

            AdvanceView();
            DisplayPlayheadFrame();
        }

        private void AdvanceView()
        {
            float frontier = Frontier();
            float interval = 1f / Mathf.Max(1f, captureFps);

            if (_followLive)
            {
                _playhead = frontier;
                return;
            }

            if (_isPlaying)
            {
                _playhead += Time.deltaTime;
                if (_playhead >= frontier)
                {
                    _playhead = frontier;
                    if (_calculating)
                    {
                        // Caught up to the live edge while it is still being calculated.
                        _followLive = true;
                    }
                    else
                    {
                        _isPlaying = false;
                    }
                }
            }

            _playhead = Mathf.Clamp(_playhead, 0f, frontier + interval);
        }

        private void CaptureFrame()
        {
            if (!pipeline.TryGetFallingParticleBuffer(out ComputeBuffer buffer, out uint count) || buffer == null)
            {
                _store.AddFrame(System.Array.Empty<float3>(), 0, _recordElapsed);
                return;
            }

            int n = (int)count;
            if (n <= 0)
            {
                _store.AddFrame(System.Array.Empty<float3>(), 0, _recordElapsed);
                return;
            }

            if (_readback == null || _readback.Length < n)
            {
                _readback = new FluidParticle[Mathf.NextPowerOfTwo(n)];
            }

            if (_positionScratch == null || _positionScratch.Length < n)
            {
                _positionScratch = new float3[Mathf.NextPowerOfTwo(n)];
            }

            buffer.GetData(_readback, 0, 0, n);
            for (int i = 0; i < n; i++)
            {
                _positionScratch[i] = _readback[i].Position;
            }

            _store.AddFrame(_positionScratch, n, _recordElapsed);
        }

        private void FinishCalculation()
        {
            _calculating = false;
            pipeline.SetSimulationActive(false);
            SaveCapture();
        }

        private void SaveCapture()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, captureSubDirectory);
                string stamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _savedPath = Path.Combine(dir, $"capture_{stamp}.harmonicbake");
                _store.SaveToFile(_savedPath);
                Debug.Log($"[SimulationTimelineDirector] Saved {_store.FrameCount} frames -> {_savedPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SimulationTimelineDirector] Save failed: {ex.Message}");
            }
        }

        private float Frontier() => _store?.Duration ?? 0f;

        private void DisplayPlayheadFrame()
        {
            if (_store == null || _store.FrameCount == 0)
            {
                return;
            }

            int frame = _store.FrameIndexForTime(_playhead);
            if (frame == _lastDisplayedFrame)
            {
                return;
            }

            _lastDisplayedFrame = frame;
            timelineRenderer?.Display(_store.GetFramePositions(frame), _store.GetFrameParticleCount(frame));
        }

        private void RestartFromConfig()
        {
            _store = null;
            _calculating = false;
            _liveFinished = false;
            timelineRenderer?.Hide();
            EnterConfig();
        }

        // ---- GUI ---------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            switch (_phase)
            {
                case Phase.Config:
                    DrawConfigGui();
                    break;
                case Phase.LiveOnly:
                    DrawLiveOnlyGui();
                    break;
                case Phase.Timeline:
                    DrawTimelineGui();
                    break;
            }
        }

        private void DrawConfigGui()
        {
            float w = 380f;
            float h = 330f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 20f, rect.y + 16f, w - 40f, h - 32f));
            GUILayout.Label("Harmonic Simulation Setup", _titleStyle);
            GUILayout.Space(10f);

            GUILayout.Label("Number of particles", _labelStyle);
            _particleCountText = GUILayout.TextField(_particleCountText, GUILayout.Height(24f));

            GUILayout.Space(6f);
            GUILayout.Label("Duration (seconds)", _labelStyle);
            _durationText = GUILayout.TextField(_durationText, GUILayout.Height(24f));

            GUILayout.Space(10f);
            _saveCalculation = GUILayout.Toggle(_saveCalculation, " Save calculation (record + scrub timeline)");
            GUILayout.Label(
                _saveCalculation
                    ? "Records every frame so you can walk through it while it calculates, then saves to disk."
                    : "Runs the simulation live only — no recording, scrubbing or saved file.",
                _labelStyle);

            GUILayout.Space(12f);
            if (GUILayout.Button(_saveCalculation ? "▶  Calculate & Scrub" : "▶  Run Simulation", GUILayout.Height(40f)))
            {
                BeginRun();
            }

            GUILayout.EndArea();
        }

        private void DrawLiveOnlyGui()
        {
            float progress = _requestedDuration > 0f ? Mathf.Clamp01(_recordElapsed / _requestedDuration) : 0f;
            float w = 460f;
            float h = 70f;
            var rect = new Rect((Screen.width - w) * 0.5f, 24f, w, h);
            GUI.Box(rect, GUIContent.none);
            int liveParticles = pipeline != null ? (int)pipeline.GetActiveParticleCount() : 0;
            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 10f, w - 32f, h - 20f));
            GUILayout.Label(
                _liveFinished
                    ? $"Simulation finished  ({liveParticles} particles)"
                    : $"Running simulation…  {(int)(progress * 100f)}%   ({liveParticles} particles)",
                _labelStyle);
            DrawBar(progress, new Color(0.2f, 0.85f, 0.4f, 1f));
            GUILayout.EndArea();

            if (GUI.Button(new Rect(Screen.width - 16f - 150f, Screen.height - 44f, 150f, 30f), "New Simulation"))
            {
                RestartFromConfig();
            }
        }

        private void DrawTimelineGui()
        {
            float barHeight = 84f;
            var rect = new Rect(0f, Screen.height - barHeight, Screen.width, barHeight);
            GUI.Box(rect, GUIContent.none);

            float pad = 16f;
            float y = rect.y + 12f;
            float buttonW = 74f;
            float total = Mathf.Max(0.0001f, _requestedDuration);
            float frontier = Frontier();

            string playLabel = _isPlaying && !_followLive ? "❚❚ Pause" : "▶ Play";
            if (_followLive)
            {
                playLabel = "❚❚ Pause";
            }

            if (GUI.Button(new Rect(pad, y, buttonW, 28f), playLabel))
            {
                TogglePlay();
            }

            float x = pad + buttonW + 8f;
            if (_calculating)
            {
                GUI.enabled = !_followLive;
                if (GUI.Button(new Rect(x, y, buttonW, 28f), "⇥ Live"))
                {
                    _followLive = true;
                    _isPlaying = true;
                }

                GUI.enabled = true;
                x += buttonW + 8f;
            }
            else
            {
                if (GUI.Button(new Rect(x, y, buttonW, 28f), "⟲ Restart"))
                {
                    _playhead = 0f;
                    _isPlaying = true;
                    _followLive = false;
                    _lastDisplayedFrame = -1;
                }

                x += buttonW + 8f;
            }

            if (GUI.Button(new Rect(Screen.width - pad - 140f, y, 140f, 28f), "New Simulation"))
            {
                RestartFromConfig();
            }

            string state = _calculating
                ? $"Calculating {(int)(Mathf.Clamp01(frontier / total) * 100f)}%"
                : "Saved";
            GUI.Label(
                new Rect(x, y + 4f, Screen.width - x - pad - 150f, 20f),
                $"{FormatTime(_playhead)} / {FormatTime(total)}    [{state}]" +
                (string.IsNullOrEmpty(_savedPath) || _calculating ? string.Empty : "  ✓ saved"),
                _labelStyle);

            // Timeline track: full range with a "buffered/calculated" fill, plus the scrub slider.
            float trackX = pad;
            float trackW = Screen.width - pad * 2f;
            float trackY = y + 44f;
            DrawTrack(new Rect(trackX, trackY, trackW, 6f), Mathf.Clamp01(frontier / total));

            float scrubMax = frontier;
            float newHead = GUI.HorizontalSlider(
                new Rect(trackX, trackY + 10f, trackW, 16f), _playhead, 0f, total);
            if (!Mathf.Approximately(newHead, _playhead))
            {
                _playhead = Mathf.Clamp(newHead, 0f, scrubMax);
                _lastDisplayedFrame = -1;
                // Grabbing the bar takes manual control; only re-follow if dragged to the live edge.
                _followLive = _calculating && _playhead >= scrubMax - (1f / Mathf.Max(1f, captureFps));
                if (!_followLive)
                {
                    _isPlaying = false;
                }

                DisplayPlayheadFrame();
            }
        }

        private void TogglePlay()
        {
            if (_followLive)
            {
                // Pause out of live-follow and freeze where we are.
                _followLive = false;
                _isPlaying = false;
                return;
            }

            _isPlaying = !_isPlaying;
            if (_isPlaying)
            {
                float frontier = Frontier();
                if (!_calculating && _playhead >= frontier)
                {
                    _playhead = 0f;
                    _lastDisplayedFrame = -1;
                }
            }
        }

        private void DrawTrack(Rect track, float bufferedFraction)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            GUI.DrawTexture(
                new Rect(track.x, track.y, track.width * Mathf.Clamp01(bufferedFraction), track.height),
                Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void DrawBar(float fill01, Color fillColor)
        {
            Rect r = GUILayoutUtility.GetRect(10f, 14f);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.4f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill01), r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold
                };
                _titleStyle.normal.textColor = Color.white;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                _labelStyle.normal.textColor = Color.white;
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int m = total / 60;
            int s = total % 60;
            int frac = Mathf.Clamp(Mathf.FloorToInt((seconds - total) * 10f), 0, 9);
            return $"{m:0}:{s:00}.{frac}";
        }

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text, out int value) ? value : fallback;

        private static float ParseFloat(string text, float fallback) =>
            float.TryParse(text, out float value) ? value : fallback;
    }
}
