using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Particles;
using SwingingPaintBucket.Pendulum;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SwingingPaintBucket.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("Run Mode")]
        [Tooltip("When enabled, pressing Unity Play starts the simulation immediately. Disable it only if you want a manual Start button.")]
        public bool AutoStartOnPlay = true;

        [Tooltip("Keyboard controls in Play Mode: Space = start/pause, Enter = restart, Backspace = reset.")]
        public bool EnableKeyboardShortcuts = true;

        [Header("System References")]
        [Tooltip("GameObject that contains PendulumSimulator, BucketController, and ParticleEmitter")]
        public GameObject BucketObject;

        [Tooltip("Optional canvas reference used during reset.")]
        public CanvasController Canvas;

        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private ParticleEmitter _emitter;
        private bool _isRunning;
        private float _elapsedTime;

        public bool IsRunning => _isRunning;
        public float ElapsedTime => _elapsedTime;

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            CacheReferences();

            if (AutoStartOnPlay)
                StartSimulation();
            else
                PauseSimulation();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();

            if (_isRunning)
                _elapsedTime += Time.deltaTime;
        }

        private void HandleKeyboardShortcuts()
        {
            if (!EnableKeyboardShortcuts || Keyboard.current == null)
                return;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                ToggleSimulation();

            if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                RestartSimulation();

            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
                ResetSimulation();
        }

        private void CacheReferences()
        {
            if (BucketObject == null)
            {
                var bucket = FindAnyObjectByType<BucketController>();
                if (bucket != null)
                    BucketObject = bucket.gameObject;
            }

            if (BucketObject == null)
            {
                Debug.LogError("[SimulationManager] BucketObject is not assigned and no BucketController was found.");
                return;
            }

            _pendulum = BucketObject.GetComponent<PendulumSimulator>();
            _bucket = BucketObject.GetComponent<BucketController>();
            _emitter = BucketObject.GetComponent<ParticleEmitter>();

            if (Canvas == null)
                Canvas = FindAnyObjectByType<CanvasController>();

            if (_pendulum == null)
                Debug.LogError("[SimulationManager] PendulumSimulator is not attached to BucketObject.");
            if (_bucket == null)
                Debug.LogError("[SimulationManager] BucketController is not attached to BucketObject.");
            if (_emitter == null)
                Debug.LogError("[SimulationManager] ParticleEmitter is not attached to BucketObject.");
        }

        public void StartSimulation()
        {
            CacheReferences();
            _isRunning = true;
            Debug.Log("[SimulationManager] Simulation started.");
        }

        public void PauseSimulation()
        {
            _isRunning = false;
            Debug.Log("[SimulationManager] Simulation paused.");
        }

        public void ToggleSimulation()
        {
            if (_isRunning)
                PauseSimulation();
            else
                StartSimulation();
        }

        public void StopSimulation()
        {
            PauseSimulation();
        }

        public void ResetSimulation()
        {
            _isRunning = false;
            _elapsedTime = 0f;

            CacheReferences();

            _pendulum?.ResetSimulation();
            _bucket?.ResetBucket();
            _emitter?.ResetParticles();
            Canvas?.ClearCanvas();

            Debug.Log("[SimulationManager] Simulation reset.");
        }

        public void RestartSimulation()
        {
            ResetSimulation();
            StartSimulation();
        }
    }
}
