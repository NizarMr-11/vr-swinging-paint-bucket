using SwingingPaintBucket.Core;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Simulation;
using UnityEngine;

namespace SwingingPaintBucket.Bucket
{
    public class BucketController : MonoBehaviour
    {
        [Header("Bucket Physical Properties")]
        [Tooltip("Bucket weight/mass in kilograms. This is the required bucket weight, not a generic pendulum mass.")]
        [Range(0.1f, 50f)] public float BucketWeightKg = 1f;

        [Tooltip("Internal bucket radius in metres. Used to convert paint volume into paint height for flow physics.")]
        [Range(0.03f, 0.5f)] public float BucketRadius = 0.15f;

        [Header("Bucket Material Type")]
        public BucketMaterialType MaterialType = BucketMaterialType.Plastic;

        [Header("Material-Related Values")]
        [Tooltip("Discharge coefficient — affected by material type and hole shape.")]
        [Range(0.1f, 1f)] public float DischargeCoefficent = SimulationConstants.DefaultDischargeCoefficent;

        [Tooltip("Rate of paint loss from bucket walls in litres/second.")]
        [Range(0f, 0.5f)] public float PaintLossRate = 0.02f;

        [Tooltip("Material absorption rate for bucket walls in litres/second, mainly wood.")]
        [Range(0f, 0.1f)] public float AbsorptionRate = 0f;

        [Header("Paint")]
        [Tooltip("Paint initial volume inside the bucket in litres.")]
        [Range(0.1f, 10f)] public float InitialPaintVolume = 2f;

        [Header("Paint Properties")]
        [Tooltip("Set the colors layered in the bucket. Left (0) = top of paint, Right (1) = bottom of paint.")]
        public Gradient PaintColors;

        [Tooltip("Paint viscosity. Higher values reduce flow and make smaller paint spread.")]
        [Range(0.1f, 10f)] public float Viscosity = 1f;

        [Tooltip("Paint density in relative units. Used for particle mass.")]
        [Range(0.1f, 5f)] public float Density = 1f;

        [Tooltip("Nozzle radius in metres.")]
        [Range(0.001f, 0.05f)] public float NozzleRadius = 0.005f;

        [Header("References")]
        public EnvironmentController Environment;
        public SimulationManager SimulationManager;

        [Header("Runtime Debug")]
        [Tooltip("Live paint volume in litres during Play Mode. This is the value that should decrease.")]
        [SerializeField] private float CurrentPaintVolumeDebug;

        [Tooltip("Paint emitted in the last physics frame, in litres.")]
        [SerializeField] private float VolumeThisFrameDebug;

        [Tooltip("Current viscosity after environment effects such as humidity.")]
        [SerializeField] private float EffectiveViscosityDebug;

        [Tooltip("Approximate flow rate in litres per second.")]
        [SerializeField] private float FlowRateLitresPerSecondDebug;

        private float _paintVolume;
        private PendulumSimulator _pendulum;

        public bool HasPaint => _paintVolume > SimulationConstants.MinPaintVolume;
        public float PaintVolume => _paintVolume;
        public float VolumeThisFrame { get; private set; }

        // Single source of truth for gravity. Both the pendulum swing AND the falling
        // paint particles must use this same value, otherwise changing the Gravity slider
        // makes the paint land in the wrong place relative to where the bucket actually is.
        public float Gravity => _pendulum != null ? _pendulum.Gravity : SimulationConstants.DefaultGravity;

        public Color CurrentPaintColor
        {
            get
            {
                if (PaintColors == null || InitialPaintVolume <= 0f)
                    return Color.white;

                float percentFull = Mathf.Clamp01(_paintVolume / InitialPaintVolume);
                return PaintColors.Evaluate(1f - percentFull);
            }
        }

        public float EffectiveViscosity
        {
            get
            {
                if (Environment == null)
                    return Mathf.Max(0.1f, Viscosity);

                return Mathf.Max(0.1f, Viscosity * Environment.GetViscosityMultiplier());
            }
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            CacheReferences();
            ApplyMaterialPreset();
            _paintVolume = InitialPaintVolume;
            UpdateDebugValues(0f);
        }

        private void FixedUpdate()
        {
            VolumeThisFrame = 0f;
            UpdateDebugValues(0f);

            if (SimulationManager != null && !SimulationManager.IsRunning)
                return;

            if (!HasPaint)
                return;

            float dt = Time.fixedDeltaTime;
            float paintHeight = CalculatePaintHeightMeters();
            if (paintHeight < SimulationConstants.MinPaintHeight)
                return;

            float gravity = _pendulum != null ? _pendulum.Gravity : SimulationConstants.DefaultGravity;

            // Torricelli-based approximation:
            // v = Cd * sqrt(2gh), Q = A * v, then viscosity slows the flow.
            float exitVelocity = DischargeCoefficent * Mathf.Sqrt(2f * gravity * paintHeight);
            float nozzleArea = Mathf.PI * NozzleRadius * NozzleRadius;
            float flowRateM3PerSecond = (nozzleArea * exitVelocity) / EffectiveViscosity;
            float flowRateLitresPerSecond = flowRateM3PerSecond * 1000f;

            // Convert from cubic metres to litres because InitialPaintVolume is stored in litres.
            VolumeThisFrame = flowRateLitresPerSecond * dt;

            _paintVolume -= VolumeThisFrame;
            _paintVolume -= PaintLossRate * dt;
            _paintVolume -= AbsorptionRate * dt;
            _paintVolume = Mathf.Max(0f, _paintVolume);

            UpdateDebugValues(flowRateLitresPerSecond);
        }

        public Vector3 GetParticleInitialVelocity()
        {
            Vector3 bucketVelocity = _pendulum != null ? _pendulum.BucketVelocity : Vector3.zero;
            float paintHeight = Mathf.Max(CalculatePaintHeightMeters(), SimulationConstants.MinPaintHeight);
            float gravity = _pendulum != null ? _pendulum.Gravity : SimulationConstants.DefaultGravity;
            float exitVelocity = DischargeCoefficent * Mathf.Sqrt(2f * gravity * paintHeight) / Mathf.Sqrt(EffectiveViscosity);
            Vector3 torricelliVelocity = Vector3.down * exitVelocity;

            return bucketVelocity + torricelliVelocity;
        }

        public void ResetBucket()
        {
            _paintVolume = InitialPaintVolume;
            VolumeThisFrame = 0f;
            ApplyMaterialPreset();
            UpdateDebugValues(0f);
        }

        public void SyncPaintVolume()
        {
            _paintVolume = InitialPaintVolume;
            UpdateDebugValues(0f);
        }

        public void ApplyMaterialPreset()
        {
            DischargeCoefficent = BucketMaterialPreset.GetDischargeCoefficent(MaterialType);
            PaintLossRate = BucketMaterialPreset.GetPaintLossRate(MaterialType);
            AbsorptionRate = BucketMaterialPreset.GetAbsorptionRate(MaterialType);
        }

        private void UpdateDebugValues(float flowRateLitresPerSecond)
        {
            CurrentPaintVolumeDebug = _paintVolume;
            VolumeThisFrameDebug = VolumeThisFrame;
            EffectiveViscosityDebug = EffectiveViscosity;
            FlowRateLitresPerSecondDebug = flowRateLitresPerSecond;
        }

        private float CalculatePaintHeightMeters()
        {
            float volumeM3 = Mathf.Max(0f, _paintVolume) * 0.001f;
            float bucketArea = Mathf.PI * BucketRadius * BucketRadius;
            return volumeM3 / Mathf.Max(0.0001f, bucketArea);
        }

        private void CacheReferences()
        {
            if (_pendulum == null)
                _pendulum = GetComponent<PendulumSimulator>();

            if (Environment == null)
                Environment = FindAnyObjectByType<EnvironmentController>();

            if (SimulationManager == null)
                SimulationManager = FindAnyObjectByType<SimulationManager>();
        }
    }
}
