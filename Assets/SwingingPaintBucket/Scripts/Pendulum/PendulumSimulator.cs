using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Core;
using SwingingPaintBucket.Simulation;
using UnityEngine;

namespace SwingingPaintBucket.Pendulum
{
    public enum RopeType
    {
        Cotton,
        Nylon,
        SteelCable,
        ElasticCord
    }

    public enum MovementDirection
    {
        Clockwise = 1,
        CounterClockwise = -1
    }

    public static class RopePhysicsPreset
    {
        public static float GetStiffness(RopeType type)
        {
            switch (type)
            {
                case RopeType.Cotton: return 520f;
                case RopeType.Nylon: return 850f;
                case RopeType.SteelCable: return 7000f;
                case RopeType.ElasticCord: return 75f;
                default: return 420f;
            }
        }

        public static float GetInternalFriction(RopeType type)
        {
            switch (type)
            {
                case RopeType.Cotton: return 0.012f;
                case RopeType.Nylon: return 0.008f;
                case RopeType.SteelCable: return 0.003f;
                case RopeType.ElasticCord: return 0.020f;
                default: return 0.030f;
            }
        }

        public static string GetDescription(RopeType type)
        {
            switch (type)
            {
                case RopeType.Cotton: return "Balanced rope: moderate stretch and moderate internal damping.";
                case RopeType.Nylon: return "Stiffer than cotton with lower damping.";
                case RopeType.SteelCable: return "Very stiff rope with almost no visible stretch.";
                case RopeType.ElasticCord: return "Soft elastic rope: visible stretch and bounce.";
                default: return string.Empty;
            }
        }
    }

    public class PendulumSimulator : MonoBehaviour
    {
        [Header("Rope Properties")]
        [Range(0.5f, 20f)] public float RopeLength = 4f;
        public RopeType RopeMaterial = RopeType.Cotton;

        [Tooltip("Calculated from Rope Type. Higher value = less stretch.")]
        public float RopeStiffness = 420f;

        [Tooltip("Calculated from Rope Type. This is energy loss inside the rope material.")]
        [Range(0f, 0.2f)] public float RopeInternalFriction = 0.030f;

        [Header("Elastic Rope")]
        [Tooltip("Enable spring-like rope extension. This is visual/educational elasticity, not Unity physics.")]
        public bool EnableElasticRope = true;

        [Tooltip("Amplifies visible stretch. Keep this low for realism; increase only for demonstration.")]
        [Range(0f, 3f)] public float RopeStretchVisualMultiplier = 1.0f;

        [Tooltip("Maximum rope stretch as a fraction of base rope length.")]
        [Range(0.01f, 0.5f)] public float MaxStretchFraction = 0.18f;

        [Header("Environment")]
        [Range(0f, 20f)] public float Gravity = SimulationConstants.DefaultGravity;

        [Tooltip("Air resistance applied as velocity-dependent drag. This replaces the old generic damping name.")]
        [Range(0f, 1f)] public float AirResistanceCoefficient = 0.006f;

        [Tooltip("Dry mechanical friction at the suspension point.")]
        [Range(0f, 1f)] public float FrictionCoefficient = 0.001f;

        [Header("Initial State")]
        [Range(-85f, 85f)] public float InitialAngleDegrees = 35f;
        [Tooltip("Horizontal direction of the swing plane around the pivot point.")]
        [Range(-180f, 180f)] public float InitialPhiDegrees = -85f;
        [Tooltip("Magnitude of the initial angular speed. For natural release, keep this at 0 or near 0.")]
        [Range(0f, 4f)] public float InitialAngularVelocity = 0f;
        public MovementDirection DirectionOfMovement = MovementDirection.Clockwise;

        [Tooltip("0 means unlimited. Positive values stop the simulation after that many full swings.")]
        [Range(0, 50)] public int TargetSwingCount = 0;

        [Header("Pivot")]
        public Vector3 PivotPoint = Vector3.zero;

        [Header("References")]
        public EnvironmentController Environment;
        public SimulationManager SimulationManager;
        public BucketController Bucket;

        [Header("Runtime Debug")]
        [SerializeField] private float CurrentAngleDegreesDebug;
        [SerializeField] private float CurrentAngularVelocityDebug;
        [SerializeField] private float EffectiveRopeLengthDebug;
        [SerializeField] private float HorizontalDisplacementDebug;
        [SerializeField] private int CompletedSwingsDebug;

        private float _theta;
        private float _omega;
        private float _phi;
        private float _radialStretch;
        private float _radialVelocity;
        private float _effectiveRopeLength;
        private int _centerCrossingCount;
        private float _previousTheta;

        public float Theta => _theta;
        public float Omega => _omega;
        public float Phi => _phi;
        public float PhiOmega => 0f;
        public float EffectiveRopeLength => _effectiveRopeLength;
        public int CompletedSwings => _centerCrossingCount / 2;

        // Backward compatibility for older code that still reads/writes DampingCoefficient.
        public float DampingCoefficient
        {
            get => AirResistanceCoefficient;
            set => AirResistanceCoefficient = value;
        }

        // Backward compatibility: the physical mass now belongs to the bucket.
        public float Mass
        {
            get => Bucket != null ? Bucket.BucketWeightKg : 1f;
            set
            {
                if (Bucket != null)
                    Bucket.BucketWeightKg = value;
            }
        }

        public Vector3 BucketVelocity
        {
            get
            {
                float length = Mathf.Max(0.1f, _effectiveRopeLength);
                Vector3 swingDirection = GetSwingPlaneDirection();

                float sinT = Mathf.Sin(_theta);
                float cosT = Mathf.Cos(_theta);

                Vector3 tangentialVelocity = swingDirection * (length * _omega * cosT)
                                           + Vector3.down * (-length * _omega * sinT);

                Vector3 radialVelocity = swingDirection * (_radialVelocity * sinT)
                                       + Vector3.down * (_radialVelocity * cosT);

                return tangentialVelocity + radialVelocity;
            }
        }

        public Vector3 Momentum => BucketVelocity * Mass;

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            CacheReferences();
            ApplyRopeTypePreset();
            ResetSimulation();
        }

        private void FixedUpdate()
        {
            if (SimulationManager != null && !SimulationManager.IsRunning)
                return;

            ApplyRopeTypePreset();

            float dt = Time.fixedDeltaTime;
            float baseLength = Mathf.Max(0.1f, RopeLength);
            float bucketMass = Mathf.Max(0.05f, Mass);

            UpdateElasticRope(baseLength, bucketMass, dt);

            float length = Mathf.Max(0.1f, _effectiveRopeLength);
            float sinTheta = Mathf.Sin(_theta);

            float drag = AirResistanceCoefficient + (RopeInternalFriction * 0.35f);
            float dryFriction = 0f;
            if (Mathf.Abs(_omega) > 0.002f)
                dryFriction = Mathf.Sign(_omega) * FrictionCoefficient * Gravity / length;

            float windAcceleration = 0f;
            if (Environment != null)
            {
                Vector3 wind = Environment.WindForce;
                windAcceleration = Vector3.Dot(wind, GetSwingPlaneDirection()) * Mathf.Cos(_theta) / length;
            }

            // Stable planar pendulum equation with optional changing rope length.
            // theta = angle from vertical; omega = angular speed.
            float thetaAcceleration = -(Gravity / length) * sinTheta
                                      - drag * _omega
                                      - dryFriction
                                      - 2f * (_radialVelocity / length) * _omega
                                      + windAcceleration;

            _previousTheta = _theta;
            _omega += thetaAcceleration * dt;
            _theta += _omega * dt;

            // Prevent accidental full-loop explosions from unrealistic settings during demo.
            float maxDemoAngle = 85f * Mathf.Deg2Rad;
            if (Mathf.Abs(_theta) > maxDemoAngle)
            {
                _theta = Mathf.Sign(_theta) * maxDemoAngle;
                _omega *= -0.35f;
            }

            CountSwingsAndStopIfNeeded();
            UpdateBucketPosition();
            UpdateDebugValues();
        }

        public void ResetSimulation()
        {
            ApplyRopeTypePreset();

            float signedAngle = Mathf.Abs(InitialAngleDegrees) * (int)DirectionOfMovement;
            _theta = signedAngle * Mathf.Deg2Rad;
            _omega = Mathf.Abs(InitialAngularVelocity) * (int)DirectionOfMovement;
            _phi = InitialPhiDegrees * Mathf.Deg2Rad;
            _radialStretch = 0f;
            _radialVelocity = 0f;
            _effectiveRopeLength = Mathf.Max(0.1f, RopeLength);
            _centerCrossingCount = 0;
            _previousTheta = _theta;

            UpdateBucketPosition();
            UpdateDebugValues();
        }

        public void ApplyRopeTypePreset()
        {
            RopeStiffness = RopePhysicsPreset.GetStiffness(RopeMaterial);
            RopeInternalFriction = RopePhysicsPreset.GetInternalFriction(RopeMaterial);
        }

        private void CacheReferences()
        {
            if (SimulationManager == null)
                SimulationManager = FindAnyObjectByType<SimulationManager>();

            if (Bucket == null)
                Bucket = GetComponent<BucketController>();

            if (Environment == null)
                Environment = FindAnyObjectByType<EnvironmentController>();
        }

        private void UpdateElasticRope(float baseLength, float bucketMass, float dt)
        {
            if (!EnableElasticRope)
            {
                _radialStretch = 0f;
                _radialVelocity = 0f;
                _effectiveRopeLength = baseLength;
                return;
            }

            float currentLength = Mathf.Max(0.1f, baseLength + _radialStretch);
            float tensionEstimate = Mathf.Max(0f, bucketMass * (Gravity * Mathf.Cos(_theta) + currentLength * _omega * _omega));
            float targetStretch = tensionEstimate / Mathf.Max(1f, RopeStiffness);
            targetStretch *= Mathf.Max(0f, RopeStretchVisualMultiplier);
            targetStretch = Mathf.Clamp(targetStretch, 0f, baseLength * MaxStretchFraction);

            float naturalFrequency = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, RopeStiffness) / bucketMass), 2.0f, 18.0f);
            float dampingRatio = Mathf.Lerp(0.16f, 0.95f, Mathf.Clamp01(RopeInternalFriction / 0.06f));

            float stretchAcceleration = naturalFrequency * naturalFrequency * (targetStretch - _radialStretch)
                                      - 2f * dampingRatio * naturalFrequency * _radialVelocity;

            _radialVelocity += stretchAcceleration * dt;
            _radialStretch += _radialVelocity * dt;
            _radialStretch = Mathf.Clamp(_radialStretch, 0f, baseLength * MaxStretchFraction);

            if (_radialStretch <= 0f && _radialVelocity < 0f)
                _radialVelocity = 0f;

            _effectiveRopeLength = baseLength + _radialStretch;
        }

        private void UpdateDebugValues()
        {
            CurrentAngleDegreesDebug = _theta * Mathf.Rad2Deg;
            CurrentAngularVelocityDebug = _omega;
            EffectiveRopeLengthDebug = _effectiveRopeLength;
            HorizontalDisplacementDebug = _effectiveRopeLength * Mathf.Sin(_theta);
            CompletedSwingsDebug = CompletedSwings;
        }

        private void CountSwingsAndStopIfNeeded()
        {
            bool crossedCenter = (_previousTheta > 0f && _theta <= 0f) || (_previousTheta < 0f && _theta >= 0f);
            if (crossedCenter)
                _centerCrossingCount++;

            if (TargetSwingCount > 0 && _centerCrossingCount >= TargetSwingCount * 2)
            {
                _omega = 0f;
                _radialVelocity = 0f;
                SimulationManager?.PauseSimulation();
            }
        }

        private Vector3 GetSwingPlaneDirection()
        {
            return new Vector3(Mathf.Cos(_phi), 0f, Mathf.Sin(_phi)).normalized;
        }

        private void UpdateBucketPosition()
        {
            float length = Mathf.Max(0.1f, _effectiveRopeLength);
            Vector3 swingDirection = GetSwingPlaneDirection();

            float xzDistance = length * Mathf.Sin(_theta);
            float yDistance = -length * Mathf.Cos(_theta);

            transform.position = PivotPoint + swingDirection * xzDistance + Vector3.up * yDistance;
        }
    }
}
