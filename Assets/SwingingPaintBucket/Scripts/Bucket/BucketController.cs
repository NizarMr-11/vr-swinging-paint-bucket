using UnityEngine;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Pendulum;

namespace SwingingPaintBucket.Bucket
{
    public class BucketController : MonoBehaviour
    {

        [Header("Bucket Material Type")]
        public BucketMaterialType MaterialType = BucketMaterialType.Plastic;

        [Header("Adjustable values, material-related)")]
        [Tooltip("Discharge coefficient — affected by material type and hole shape")]
        [Range(0.1f, 1f)]
        public float DischargeCoefficent;

        [Tooltip("Rate of paint loss from bucket walls")]
        public float PaintLossRate;

        [Tooltip("Material absorption rate for paint (wood only)")]
        public float AbsorptionRate;



        [Header("Paint")]
        [Tooltip("Paint initial volume inside the bucket (Liter)")]
        public float InitialPaintVolume = 2f;

        [Header("Paint properties")]
        [Tooltip("Set the colors layered in the bucket. Left (0) = Top of paint, Right (1) = Bottom of paint")]
        public Gradient PaintColors;

        public Color CurrentPaintColor
        {
            get
            {
                if (InitialPaintVolume <= 0f) return Color.white;
                float percentFull = Mathf.Clamp01(_paintVolume / InitialPaintVolume);
                return PaintColors.Evaluate(1f - percentFull);
            }
        }

        [Tooltip("Paint viscosity")]
        [Range(0.1f, 10f)]
        public float Viscosity = 1f;

        [Tooltip("Paint density")]
        [Range(0.1f, 5f)]
        public float Density = 1f;


        [Tooltip("Nozzle radius")]
        [Range(0.001f, 0.05f)]
        public float NozzleRadius = 0.005f;


        private float _paintVolume;


        public bool HasPaint => _paintVolume > SimulationConstants.MinPaintVolume;


        public float PaintVolume => _paintVolume;


        public float VolumeThisFrame { get; private set; }


        private PendulumSimulator _pendulum;



        private void Start()
        {

            DischargeCoefficent = BucketMaterialPreset.GetDischargeCoefficent(MaterialType);
            PaintLossRate = BucketMaterialPreset.GetPaintLossRate(MaterialType);
            AbsorptionRate = BucketMaterialPreset.GetAbsorptionRate(MaterialType);

            _paintVolume = InitialPaintVolume;


            _pendulum = GetComponent<PendulumSimulator>();
        }

        private void FixedUpdate()
        {
            VolumeThisFrame = 0f;

            if (!HasPaint) return;

            float dt = Time.fixedDeltaTime;


            float h = _paintVolume;

            if (h < SimulationConstants.MinPaintHeight) return;

            // Torricelli formula for exit velocity
            float vExit = DischargeCoefficent * Mathf.Sqrt(2f * SimulationConstants.DefaultGravity * h);

            // Nozzle area: A = π × r²
            float nozzleArea = Mathf.PI * NozzleRadius * NozzleRadius;

            // Flow rate: Q = A × v
            float flowRate = nozzleArea * vExit;

            // Volume in dt
            VolumeThisFrame = flowRate * dt;

            // Paint decrease: Exited + absorption + loss
            _paintVolume -= VolumeThisFrame;
            _paintVolume -= PaintLossRate * dt;
            _paintVolume -= AbsorptionRate * dt;

            // No negative values
            _paintVolume = Mathf.Max(0f, _paintVolume);
        }

        // Methods
        public Vector3 GetParticleInitialVelocity()
        {
            Vector3 bucketVelocity = _pendulum != null
                ? _pendulum.BucketVelocity
                : Vector3.zero;

            float h = Mathf.Max(_paintVolume, SimulationConstants.MinPaintHeight);
            float vExit = DischargeCoefficent * Mathf.Sqrt(2f * SimulationConstants.DefaultGravity * h);

            Vector3 torricelliVelocity = Vector3.down * vExit;

            return bucketVelocity + torricelliVelocity;
        }

        // Reset
        public void ResetBucket()
        {
            _paintVolume = InitialPaintVolume;
            VolumeThisFrame = 0f;

            DischargeCoefficent = BucketMaterialPreset.GetDischargeCoefficent(MaterialType);
            PaintLossRate = BucketMaterialPreset.GetPaintLossRate(MaterialType);
            AbsorptionRate = BucketMaterialPreset.GetAbsorptionRate(MaterialType);
        }
    }
}