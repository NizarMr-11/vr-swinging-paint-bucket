using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Top-left FPS and simulation statistics overlay during play.</summary>
    public sealed class V4Lab2StatsHudController : MonoBehaviour
    {
        [SerializeField] private V4PipelineRoot pipeline;
        [SerializeField] private V4SphericalPendulumController pendulum;
        [SerializeField] private V4BucketMotionSettings motionSettings;
        [SerializeField] private Text statsText;
        [SerializeField] private Text pausedBadge;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button collapseButton;
        [SerializeField] private int updateIntervalFrames = 10;

        private float _smoothedFps;
        private int _frameCounter;
        private bool _collapsed;

        public void Bind(
            V4PipelineRoot pipelineRoot,
            V4SphericalPendulumController pendulumController,
            V4BucketMotionSettings bucketMotion,
            Text text,
            Text paused,
            CanvasGroup group,
            Button collapse)
        {
            pipeline = pipelineRoot;
            pendulum = pendulumController;
            motionSettings = bucketMotion;
            statsText = text;
            pausedBadge = paused;
            canvasGroup = group;
            collapseButton = collapse;
            WireCollapseButton();
        }

        private void Awake()
        {
            WireCollapseButton();
        }

        private void WireCollapseButton()
        {
            if (collapseButton == null)
            {
                return;
            }

            collapseButton.onClick.RemoveListener(ToggleCollapsed);
            collapseButton.onClick.AddListener(ToggleCollapsed);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetPaused(bool paused)
        {
            if (pausedBadge != null)
            {
                pausedBadge.gameObject.SetActive(paused);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = paused ? 0.65f : 1f;
            }
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy || statsText == null || pipeline == null)
            {
                return;
            }

            _frameCounter++;
            if (_frameCounter < updateIntervalFrames)
            {
                return;
            }

            _frameCounter = 0;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-6f);
            float instantFps = 1f / dt;
            _smoothedFps = Mathf.Lerp(_smoothedFps <= 0f ? instantFps : _smoothedFps, instantFps, 0.1f);
            statsText.text = BuildStatsText(dt);
        }

        private string BuildStatsText(float dt)
        {
            if (_collapsed)
            {
                return $"FPS {_smoothedFps:0}";
            }

            string profileName = pipeline.globalProfile != null
                ? pipeline.globalProfile.profileName
                : "n/a";
            bool gpuPendulum = motionSettings != null && motionSettings.UseGpuPendulum;
            string simState = pipeline.autoRun ? "Running" : "Paused";

            return
                $"FPS {_smoothedFps:0}  ({dt * 1000f:0.0} ms)\n" +
                $"Particles {pipeline.ActiveParticleCount} / {pipeline.Capacity}\n" +
                $"Spawned {pipeline.SpawnedTotal}  Escaped {pipeline.EscapedTotal}  Settled {pipeline.SettledTotal}\n" +
                $"Sim frame {pipeline.FrameIndex}  r={pipeline.ParticleRadius:F4}m\n" +
                $"Top band {pipeline.TopBandCountLastFrame}\n" +
                $"Profile {profileName}  Pendulum {(gpuPendulum ? "GPU" : "CPU")}\n" +
                $"State {simState}";
        }

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            if (collapseButton != null)
            {
                Text label = collapseButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = _collapsed ? "Stats +" : "Stats -";
                }
            }
        }
    }
}
