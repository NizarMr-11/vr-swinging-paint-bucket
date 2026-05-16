using UnityEngine;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;

namespace SwingingPaintBucket.Simulation
{
    public class SimulationManager : MonoBehaviour
    {
        // ---- المراجع للأنظمة الفرعية ----

        [Header("مراجع الأنظمة")]
        [Tooltip("الـ GameObject الذي يحمل PendulumSimulator و BucketController")]
        public GameObject BucketObject;

        // ---- الحالة الداخلية ----

        private PendulumSimulator _pendulum;
        private BucketController  _bucket;
        private bool _isRunning = false;

        // ---- Properties ----

        public bool IsRunning => _isRunning;

        // ---- Unity Methods ----

        private void Start()
        {
            if (BucketObject == null)
            {
                Debug.LogError("[SimulationManager] BucketObject غير محدد في Inspector!");
                return;
            }

            _pendulum = BucketObject.GetComponent<PendulumSimulator>();
            _bucket   = BucketObject.GetComponent<BucketController>();

            if (_pendulum == null)
                Debug.LogError("[SimulationManager] PendulumSimulator غير موجود على BucketObject!");

            if (_bucket == null)
                Debug.LogError("[SimulationManager] BucketController غير موجود على BucketObject!");
        }

        // ---- Public Methods ----

        /// <summary>
        /// بدء المحاكاة
        /// </summary>
        public void StartSimulation()
        {
            _isRunning = true;
            Debug.Log("[SimulationManager] بدأت المحاكاة");
        }

        /// <summary>
        /// إيقاف المحاكاة مؤقتاً
        /// </summary>
        public void PauseSimulation()
        {
            _isRunning = false;
            Debug.Log("[SimulationManager] توقفت المحاكاة");
        }

        /// <summary>
        /// إعادة تعيين جميع الأنظمة للحالة الابتدائية
        /// </summary>
        public void ResetSimulation()
        {
            _isRunning = false;
            _pendulum?.ResetSimulation();
            _bucket?.ResetBucket();
            Debug.Log("[SimulationManager] تمت إعادة التعيين");
        }
    }
}
