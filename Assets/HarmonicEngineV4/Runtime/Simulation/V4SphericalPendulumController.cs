using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// 3D spherical pendulum driver: bucket moves on a sphere around a world pivot.
    /// Place the bucket manually and assign a pivot Transform; rope length, α/β/ω, and
    /// bucket orientation (floor→rim axis toward pivot) are derived from that pose.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4Bucket))]
    [ExecuteAlways]
    [DefaultExecutionOrder(-150)]
    public sealed class V4SphericalPendulumController : MonoBehaviour
    {
        [Header("Rope")]
        [Tooltip("Scene hang point. When set, overrides pivotPoint.")]
        public Transform pivotTransform;

        [Tooltip("Fallback hang point when pivotTransform is not assigned.")]
        public Vector3 pivotPoint = new Vector3(0f, 3f, 0f);

        [Min(0.1f)] public float ropeLength = 2.6f;

        [Header("Environment")]
        [Min(0f)] public float gravity = 9.81f;
        [Range(0f, 2f)] public float dampingCoefficient = 0.05f;

        [Header("Initial state")]
        [Tooltip("When true, Reset uses the manually placed bucket position to derive rope length and direction.")]
        public bool adoptManualBucketPoseOnReset = true;

        [Tooltip("Twist ω (degrees) about the rope axis after hang alignment.")]
        public float twistAngleDegrees;

        [Tooltip("Starting tangential velocity (m/s), projected onto the tangent plane.")]
        public Vector3 initialTangentialVelocity = Vector3.zero;

        [Header("Slosh feedback")]
        [Tooltip("When false, Space no longer resets the pendulum (e.g. Lab2 UI uses Space for pause).")]
        public bool enableKeyboardReset = true;
        [Min(0f)] public float bucketMass = 0.5f;
        [Range(0f, 1f)] public float sloshFeedbackScale = 1f;
        [Range(0f, 1f)] public float fluidMassSmoothing = 0.2f;

        private V4Bucket _bucket;
        private V4FluidMassProbe _fluidProbe;
        private V4GpuBucketDriver _gpuDriver;
        private bool _gpuIntegrationActive;
        private Vector3 _unitDirection;
        private Vector3 _tangentialVelocity;
        private Vector3 _prevAngularVelocity;
        private Vector3 _restBucketPosition;
        private float _smoothedFluidMass;
        private Vector3 _smoothedComOffset;
        private bool _hasRestPose;

        public Vector3 UnitDirection => _unitDirection;
        public Vector3 TangentialVelocity => _tangentialVelocity;
        public Vector3 AngularVelocityWorld { get; private set; }
        public Vector3 AngularAccelerationWorld { get; private set; }
        public Vector3 PivotPoint => GetPivotWorld();

        /// <summary>Azimuth α (degrees) of the rope direction around world Y.</summary>
        public float AlphaDegrees { get; private set; }

        /// <summary>Polar β (degrees) of the rope direction from world +Y.</summary>
        public float BetaDegrees { get; private set; }

        /// <summary>Twist ω (degrees) about the rope axis.</summary>
        public float OmegaDegrees => twistAngleDegrees;

        public bool HasRestPose => _hasRestPose;

        public Vector3 RestBucketPosition => _restBucketPosition;

        public void SetGpuIntegrationActive(bool active)
        {
            _gpuIntegrationActive = active;
        }

        public bool IsGpuIntegrationActive => _gpuIntegrationActive;

        private void Awake()
        {
            _bucket = GetComponent<V4Bucket>();
            _fluidProbe = GetComponent<V4FluidMassProbe>();
            _gpuDriver = GetComponent<V4GpuBucketDriver>();
            CaptureRestPoseFromScene();
            if (!_gpuIntegrationActive)
            {
                ResetSimulation();
            }
            else
            {
                SyncInternalStateFromScene();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                SyncSceneSetupPose(moveBucket: false);
                return;
            }

            CaptureRestPoseFromScene();
            if (_gpuIntegrationActive)
            {
                SyncInternalStateFromScene();
                if (adoptManualBucketPoseOnReset)
                {
                    ApplyManualRestPoseToTransform();
                }

                _gpuDriver?.ResetFromPendulum(this);
                return;
            }

            ResetSimulation();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                SyncSceneSetupPose(moveBucket: false);
            }
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || _gpuIntegrationActive)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            if (dt <= 1e-6f)
            {
                return;
            }

            if (_fluidProbe != null)
            {
                SetFluidFeedback(_fluidProbe.LatestStats);
            }

            Vector3 sloshAccel = ComputeSloshAcceleration();
            V4SphericalPendulumMath.IntegrateStep(
                ref _unitDirection,
                ref _tangentialVelocity,
                ropeLength,
                gravity,
                dampingCoefficient,
                dt,
                sloshAccel);

            ApplyPoseAndKinematics(dt);
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (enableKeyboardReset && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ResetSimulation();
            }
        }
#else
        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (enableKeyboardReset && Input.GetKeyDown(KeyCode.Space))
            {
                ResetSimulation();
            }
        }
#endif

        public Vector3 GetPivotWorld()
        {
            return pivotTransform != null ? pivotTransform.position : pivotPoint;
        }

        /// <summary>Derive rope length, α/β, and bucket orientation from the current transform position.</summary>
        public void SyncSceneSetupPose(bool moveBucket)
        {
            Vector3 pivot = GetPivotWorld();
            Vector3 bucketPos = transform.position;
            V4SphericalPendulumMath.DecomposePose(
                pivot,
                bucketPos,
                twistAngleDegrees,
                out float length,
                out Vector3 direction,
                out float alpha,
                out float beta,
                out float omegaIgnored);

            if (length > 1e-4f)
            {
                ropeLength = length;
                _unitDirection = direction;
                AlphaDegrees = alpha;
                BetaDegrees = beta;
            }

            transform.rotation = V4SphericalPendulumMath.ComputeBucketRotation(_unitDirection, twistAngleDegrees);

            if (moveBucket && length > 1e-4f && !adoptManualBucketPoseOnReset)
            {
                transform.position = V4SphericalPendulumMath.WorldPosition(pivot, ropeLength, _unitDirection);
            }
        }

        /// <summary>
        /// Play/init path for a manually placed bucket: keep the authored world XYZ (e.g. off to the side),
        /// only derive rope length/direction and hang rotation from that position.
        /// </summary>
        public void ApplyManualRestPoseToTransform()
        {
            CaptureRestPoseFromScene();
            if (!_hasRestPose)
            {
                SyncSceneSetupPose(moveBucket: false);
                return;
            }

            Vector3 pivot = GetPivotWorld();
            Vector3 delta = _restBucketPosition - pivot;
            if (delta.sqrMagnitude > 1e-8f)
            {
                ropeLength = delta.magnitude;
                _unitDirection = delta / ropeLength;
                UpdateAlphaBeta();
            }

            transform.SetPositionAndRotation(
                _restBucketPosition,
                V4SphericalPendulumMath.ComputeBucketRotation(_unitDirection, twistAngleDegrees));
        }

        public void PreparePlayInitPose()
        {
            if (adoptManualBucketPoseOnReset)
            {
                ApplyManualRestPoseToTransform();
                return;
            }

            SyncSceneSetupPose(moveBucket: true);
        }

        public void CaptureRestPoseFromScene()
        {
            _restBucketPosition = transform.position;
            Vector3 pivot = GetPivotWorld();
            Vector3 delta = _restBucketPosition - pivot;
            if (delta.sqrMagnitude > 1e-8f)
            {
                ropeLength = delta.magnitude;
                _unitDirection = delta / ropeLength;
                _hasRestPose = true;
                UpdateAlphaBeta();
            }
        }

        public void ResetSimulation()
        {
            if (_gpuIntegrationActive)
            {
                SyncInternalStateFromScene();
                if (adoptManualBucketPoseOnReset)
                {
                    ApplyManualRestPoseToTransform();
                }

                _gpuDriver?.ResetFromPendulum(this);
                return;
            }

            if (adoptManualBucketPoseOnReset && _hasRestPose)
            {
                Vector3 pivot = GetPivotWorld();
                Vector3 delta = _restBucketPosition - pivot;
                if (delta.sqrMagnitude > 1e-8f)
                {
                    ropeLength = delta.magnitude;
                    _unitDirection = delta / ropeLength;
                }
            }
            else
            {
                _unitDirection = V4SphericalPendulumMath.NormalizeDirection(_unitDirection, Vector3.down);
            }

            _tangentialVelocity = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                initialTangentialVelocity,
                _unitDirection);
            _smoothedFluidMass = 0f;
            _smoothedComOffset = Vector3.zero;
            _prevAngularVelocity = Vector3.zero;
            AngularVelocityWorld = Vector3.zero;
            AngularAccelerationWorld = Vector3.zero;
            UpdateAlphaBeta();
            ApplyPoseAndKinematics(Time.fixedDeltaTime > 1e-6f ? Time.fixedDeltaTime : 0.02f);
        }

        private void SyncInternalStateFromScene()
        {
            if (adoptManualBucketPoseOnReset && _hasRestPose)
            {
                Vector3 pivot = GetPivotWorld();
                Vector3 delta = _restBucketPosition - pivot;
                if (delta.sqrMagnitude > 1e-8f)
                {
                    ropeLength = delta.magnitude;
                    _unitDirection = delta / ropeLength;
                }
            }

            _tangentialVelocity = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                initialTangentialVelocity,
                _unitDirection);
            UpdateAlphaBeta();
        }

        public void SyncStateFromGpu(V4GpuBucketState state)
        {
            _unitDirection = state.unitDirection;
            _tangentialVelocity = state.tangentialVelocity;
            AngularVelocityWorld = state.angularVelocity;
            AngularAccelerationWorld = state.angularAcceleration;
            UpdateAlphaBeta();

            var rotation = new Quaternion(
                state.worldRotation.x,
                state.worldRotation.y,
                state.worldRotation.z,
                state.worldRotation.w);
            transform.SetPositionAndRotation(state.worldOrigin, rotation);

            if (_bucket != null && Application.isPlaying)
            {
                _bucket.SetAnalyticKinematics(
                    state.linearVelocity,
                    state.angularVelocity,
                    state.angularAcceleration,
                    useAnalytic: state.useGpuState > 0.5f);
            }
        }

        private void SetFluidFeedback(V4FluidMassStats stats)
        {
            float blend = Mathf.Clamp01(fluidMassSmoothing);
            _smoothedFluidMass = Mathf.Lerp(_smoothedFluidMass, stats.totalMass, blend);
            Vector3 comOffset = stats.particleCount > 0
                ? stats.centerOfMassWorld - transform.position
                : Vector3.zero;
            _smoothedComOffset = Vector3.Lerp(_smoothedComOffset, comOffset, blend);
        }

        private void UpdateAlphaBeta()
        {
            V4SphericalPendulumMath.SphericalAlphaBetaFromDirection(
                _unitDirection,
                out float alpha,
                out float beta);
            AlphaDegrees = alpha;
            BetaDegrees = beta;
        }

        private Vector3 ComputeSloshAcceleration()
        {
            return V4SphericalPendulumMath.ComputeSloshAcceleration(
                _unitDirection,
                _smoothedComOffset,
                _smoothedFluidMass,
                bucketMass,
                ropeLength,
                gravity,
                sloshFeedbackScale);
        }

        private void ApplyPoseAndKinematics(float deltaTime)
        {
            transform.SetPositionAndRotation(
                V4SphericalPendulumMath.WorldPosition(GetPivotWorld(), ropeLength, _unitDirection),
                V4SphericalPendulumMath.ComputeBucketRotation(_unitDirection, twistAngleDegrees));

            Vector3 linearVel = _tangentialVelocity;
            Vector3 angularVel = V4SphericalPendulumMath.ComputeAngularVelocity(
                _unitDirection,
                _tangentialVelocity,
                ropeLength);

            if (deltaTime > 1e-6f)
            {
                AngularAccelerationWorld = (angularVel - _prevAngularVelocity) / deltaTime;
            }

            _prevAngularVelocity = angularVel;
            AngularVelocityWorld = angularVel;
            UpdateAlphaBeta();

            if (_bucket != null && Application.isPlaying)
            {
                _bucket.SetAnalyticKinematics(linearVel, angularVel, AngularAccelerationWorld, useAnalytic: true);
            }
        }
    }
}
