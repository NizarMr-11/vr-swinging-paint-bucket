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

            if (_emitter == null)
                Debug.LogError("[SimulationManager] ParticleEmitter غير موجود على BucketObject!");


            if (_pendulum == null)
                Debug.LogError("[SimulationManager] PendulumSimulator غير موجود على BucketObject!");

            if (_bucket == null)
                Debug.LogError("[SimulationManager] BucketController غير موجود على BucketObject!");
        }

        public void StartSimulation()
        {
            _isRunning = true;
            Debug.Log("[SimulationManager] بدأت المحاكاة");
        }

        public void PauseSimulation()
        {
            _isRunning = false;
            Debug.Log("[SimulationManager] توقفت المحاكاة");
        }

        public void ResetSimulation()
        {
            _isRunning = false;
            _pendulum?.ResetSimulation();
            _bucket?.ResetBucket();
            _emitter?.ResetParticles();
            Debug.Log("[SimulationManager] تمت إعادة التعيين");
        }
    }
}
