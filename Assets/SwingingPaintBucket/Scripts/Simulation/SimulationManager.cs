using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Particles;
using SwingingPaintBucket.Pendulum;
using UnityEngine;

namespace SwingingPaintBucket.Simulation
{
    public class SimulationManager : MonoBehaviour
    {

        [Header("مراجع الأنظمة")]
        [Tooltip("الـ GameObject الذي يحمل PendulumSimulator و BucketController")]
        public GameObject BucketObject;

        private PendulumSimulator _pendulum;
        private BucketController  _bucket;
        private bool _isRunning = false;
        private ParticleEmitter _emitter;

        public bool IsRunning => _isRunning;

        private void Start()
        {
            if (BucketObject == null)
            {
                Debug.LogError("[SimulationManager] BucketObject غير محدد في Inspector!");
                return;
            }

            _pendulum = BucketObject.GetComponent<PendulumSimulator>();
            _bucket   = BucketObject.GetComponent<BucketController>();
            _emitter = BucketObject.GetComponent<ParticleEmitter>();

            if (_emitter == null)
                Debug.LogError("[SimulationManager] ParticleEmitter is not attached to BucketObject!");


            if (_pendulum == null)
                Debug.LogError("[SimulationManager] PendulumSimulator is not attached to BucketObject!");

            if (_bucket == null)
                Debug.LogError("[SimulationManager] BucketController is not attached to BucketObject!");
        }

        public void StartSimulation()
        {
            _isRunning = true;
            Debug.Log("[SimulationManager] Simulation started");
        }

        public void PauseSimulation()
        {
            _isRunning = false;
            Debug.Log("[SimulationManager] simulation endded");
        }

        public void ResetSimulation()
        {
            _isRunning = false;
            _pendulum?.ResetSimulation();
            _bucket?.ResetBucket();
            _emitter?.ResetParticles();
            Debug.Log("[SimulationManager] reset is commited");
        }
    }
}
