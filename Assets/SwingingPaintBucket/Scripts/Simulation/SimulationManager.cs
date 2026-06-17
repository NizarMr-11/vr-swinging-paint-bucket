using HarmonicEngine.Core.Mathematics.Integrators;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Particles;
using SwingingPaintBucket.Pendulum;
using UnityEngine;

namespace SwingingPaintBucket.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("System References")]
        [Tooltip("GameObject with PendulumSimulator, BucketController, and ParticleEmitter")]
        public GameObject BucketObject;

        [Header("Harmonic V3 GPU Pipeline")]
        public bool UseHarmonicGpuPipeline = true;
        public PipelineExecutionController HarmonicPipeline;
        public HighScaleFramePresenter ImpastoPresenter;
        public HarmonicBakeRecorder BakeRecorder;
        public HarmonicBakePlaybackDriver BakePlayback;
        public CanvasController Canvas;

        [Header("Quality")]
        public HarmonicQualityTier DefaultQualityTier = HarmonicQualityTier.Medium;

        [Header("Development")]
        [SerializeField] private bool autoStartSimulationOnPlay;

        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private ParticleEmitter _emitter;
        private HarmonicGpuEmitterBridge _gpuBridge;
        private HarmonicBucketKinematicBridge _kinematicBridge;
        private HarmonicCanvasHitBridge _canvasHitBridge;
        private HarmonicSimulationControls _controls;
        private bool _isRunning;
        private HarmonicSimulationMode _simulationMode = HarmonicSimulationMode.Live;

        public bool IsRunning => _isRunning;
        public HarmonicSimulationMode SimulationMode => _simulationMode;

        private void Start()
        {
            if (!HarmonicGpuCapabilityGuard.TryValidate(out string gpuReason))
            {
                Debug.LogWarning($"[SimulationManager] GPU pipeline unavailable: {gpuReason} Falling back to CPU particles.");
                UseHarmonicGpuPipeline = false;
            }

            if (BucketObject == null)
            {
                Debug.LogError("[SimulationManager] BucketObject is not assigned.");
                return;
            }

            _pendulum = BucketObject.GetComponent<PendulumSimulator>();
            _bucket = BucketObject.GetComponent<BucketController>();
            _emitter = BucketObject.GetComponent<ParticleEmitter>();

            if (_emitter == null)
            {
                Debug.LogError("[SimulationManager] ParticleEmitter missing on BucketObject.");
            }

            if (_pendulum == null)
            {
                Debug.LogError("[SimulationManager] PendulumSimulator missing on BucketObject.");
            }

            if (_bucket == null)
            {
                Debug.LogError("[SimulationManager] BucketController missing on BucketObject.");
            }

            if (Canvas == null)
            {
                Canvas = FindFirstObjectByType<CanvasController>();
            }

            if (ImpastoPresenter == null)
            {
                ImpastoPresenter = FindFirstObjectByType<HighScaleFramePresenter>();
            }

            ConfigureHarmonicPipeline();
            ConfigureCanvasImpasto();
            EnsureSimulationControls();

            if (autoStartSimulationOnPlay)
            {
                StartSimulation();
            }
        }

        private void ConfigureHarmonicPipeline()
        {
            if (HarmonicPipeline == null)
            {
                HarmonicPipeline = FindFirstObjectByType<PipelineExecutionController>();
            }

            if (HarmonicPipeline == null)
            {
                return;
            }

            HarmonicPipeline.ApplyQualityTier(DefaultQualityTier);

            if (!UseHarmonicGpuPipeline || _emitter == null)
            {
                if (_emitter != null)
                {
                    _emitter.UseHarmonicGpuPipeline = false;
                }

                return;
            }

            _gpuBridge = BucketObject.GetComponent<HarmonicGpuEmitterBridge>();
            if (_gpuBridge == null)
            {
                _gpuBridge = BucketObject.AddComponent<HarmonicGpuEmitterBridge>();
            }

            _kinematicBridge = BucketObject.GetComponent<HarmonicBucketKinematicBridge>();
            if (_kinematicBridge == null)
            {
                _kinematicBridge = BucketObject.AddComponent<HarmonicBucketKinematicBridge>();
            }

            _canvasHitBridge = BucketObject.GetComponent<HarmonicCanvasHitBridge>();
            if (_canvasHitBridge == null)
            {
                _canvasHitBridge = BucketObject.AddComponent<HarmonicCanvasHitBridge>();
            }

            _emitter.UseHarmonicGpuPipeline = true;
            _emitter.HarmonicPipeline = HarmonicPipeline;
            _gpuBridge.Bind(HarmonicPipeline, BucketObject.transform);
            _kinematicBridge.BindPendulum(_pendulum);
            _canvasHitBridge.Bind(HarmonicPipeline, Canvas, ImpastoPresenter, _bucket);

            HarmonicPipeline.SetBucketTransform(BucketObject.transform);
            HarmonicPipeline.SetBucketKinematicProvider(_kinematicBridge);
            HarmonicPipeline.SetSimulationMode(_simulationMode);

            if (Canvas != null)
            {
                HarmonicPipeline.SetCanvasPlaneY(Canvas.transform.position.y);
            }

            HarmonicPipeline.EnableExternalIngestion(true);
            HarmonicPipeline.SetSimulationActive(false);
        }

        private void ConfigureCanvasImpasto()
        {
            if (Canvas == null)
            {
                return;
            }

            Canvas.ConfigureImpasto(ImpastoPresenter);
        }

        private void EnsureSimulationControls()
        {
            _controls = GetComponent<HarmonicSimulationControls>();
            if (_controls == null)
            {
                _controls = gameObject.AddComponent<HarmonicSimulationControls>();
            }
        }

        public void SetQualityTier(HarmonicQualityTier tier)
        {
            DefaultQualityTier = tier;
            HarmonicPipeline?.ApplyQualityTier(tier);
            Debug.Log($"[SimulationManager] Quality tier set to {tier} ({HarmonicQualityPresets.GetParticleCapacity(tier)} particles).");
        }

        public void SetSimulationMode(HarmonicSimulationMode mode)
        {
            _simulationMode = mode;
            HarmonicPipeline?.SetSimulationMode(mode);

            if (BakeRecorder != null)
            {
                BakeRecorder.enabled = mode == HarmonicSimulationMode.BakeRecord;
            }

            if (BakePlayback != null)
            {
                BakePlayback.enabled = mode == HarmonicSimulationMode.BakePlayback;
                if (mode == HarmonicSimulationMode.BakePlayback)
                {
                    BakePlayback.ResetPlayback();
                }
            }

            bool liveSim = mode == HarmonicSimulationMode.Live || mode == HarmonicSimulationMode.BakeRecord;
            if (_isRunning)
            {
                HarmonicPipeline?.SetSimulationActive(UseHarmonicGpuPipeline && liveSim);
            }

            Debug.Log($"[SimulationManager] Simulation mode set to {mode}.");
        }

        public void StartSimulation()
        {
            _isRunning = true;
            bool liveSim = _simulationMode == HarmonicSimulationMode.Live
                || _simulationMode == HarmonicSimulationMode.BakeRecord;
            HarmonicPipeline?.SetSimulationActive(UseHarmonicGpuPipeline && liveSim);
            Debug.Log("[SimulationManager] Simulation started.");
        }

        public void PauseSimulation()
        {
            _isRunning = false;
            HarmonicPipeline?.SetSimulationActive(false);
            Debug.Log("[SimulationManager] Simulation paused.");
        }

        public void ResetSimulation()
        {
            _isRunning = false;
            _pendulum?.ResetSimulation();
            _bucket?.ResetBucket();
            _emitter?.ResetParticles();
            HarmonicPipeline?.SetSimulationActive(false);
            HarmonicPipeline?.ClearAllParticles();
            Canvas?.ClearCanvas();
            ImpastoPresenter?.ClearHeightMap();
            BakePlayback?.ResetPlayback();
            Debug.Log("[SimulationManager] Simulation reset.");
        }
    }
}
