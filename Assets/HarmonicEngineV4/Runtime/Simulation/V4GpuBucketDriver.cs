using HarmonicEngineV4.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Owns the GPU bucket state buffer and dispatches integrate / upload kernels.
    /// Pendulum mode integrates on GPU; keyboard mode uploads CPU kinematics each frame.
    /// </summary>
    public sealed class V4GpuBucketDriver : MonoBehaviour
    {
        private const int FluidAccumSlotCount = 8;

        private static readonly int BucketStateId = Shader.PropertyToID(V4GpuBucketState.BufferName);
        private static readonly int FluidAccumId = Shader.PropertyToID("_FluidAccum");
        private static readonly int MassReduceAccumId = Shader.PropertyToID("_Accum");
        private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
        private static readonly int CpuBucketWorldToLocalId = Shader.PropertyToID("_CpuBucketWorldToLocal");
        private static readonly int CpuBucketLocalToWorldId = Shader.PropertyToID("_CpuBucketLocalToWorld");
        private static readonly int CpuBucketLinearVelocityId = Shader.PropertyToID("_CpuBucketLinearVelocity");
        private static readonly int CpuBucketAngularVelocityId = Shader.PropertyToID("_CpuBucketAngularVelocity");
        private static readonly int CpuBucketAngularAccelerationId = Shader.PropertyToID("_CpuBucketAngularAcceleration");
        private static readonly int CpuBucketWorldOriginId = Shader.PropertyToID("_CpuBucketWorldOrigin");
        private static readonly int BucketHangEarLocalYId = Shader.PropertyToID("_BucketHangEarLocalY");

        [Tooltip("When true, copies GPU pose to Transform after each step (debug/hierarchy).")]
        public bool syncTransformToCpu;

        private V4BucketHangEar _hangEar;

        private ComputeShader _bucketPhysicsShader;
        private ComputeShader _massReduceShader;
        private int _integrateKernel;
        private int _uploadKernel;
        private int _applyMassReduceKernel;
        private int _massReduceKernel;

        private ComputeBuffer _bucketStateBuffer;
        private ComputeBuffer _fluidAccumBuffer;
        private V4GpuBucketState[] _cpuScratch = new V4GpuBucketState[1];
        private bool _initialized;

        public ComputeBuffer BucketStateBuffer => _bucketStateBuffer;
        public ComputeBuffer FluidAccumBuffer => _fluidAccumBuffer;
        public bool IsInitialized => _initialized;

        public V4GpuBucketState LatestState { get; private set; }

        private void Awake()
        {
            _hangEar = GetComponent<V4BucketHangEar>();
        }

        private float ResolveHangEarLocalY(V4SphericalPendulumController pendulum)
        {
            if (pendulum != null)
            {
                return pendulum.HangEarAttachLocalY;
            }

            if (_hangEar != null)
            {
                return _hangEar.AttachLocalY;
            }

            V4Bucket bucket = GetComponent<V4Bucket>();
            return bucket != null ? bucket.height : 0f;
        }

        public void Initialize(V4BufferRegistry registry)
        {
            if (_initialized)
            {
                return;
            }

            _bucketPhysicsShader = V4ShaderLibrary.Load(V4ShaderLibrary.BucketPhysics);
            _massReduceShader = V4ShaderLibrary.Load(V4ShaderLibrary.FluidMassReduce);
            _integrateKernel = _bucketPhysicsShader.FindKernel("BucketIntegrateKernel");
            _uploadKernel = _bucketPhysicsShader.FindKernel("BucketUploadFromCpuKernel");
            _applyMassReduceKernel = _bucketPhysicsShader.FindKernel("BucketApplyMassReduceKernel");
            _massReduceKernel = _massReduceShader.FindKernel("FluidMassReduceKernel");

            _bucketStateBuffer = registry.Create(
                V4GpuBucketState.BufferName,
                1,
                V4GpuBucketState.Stride,
                V4BufferLifetime.Persistent);
            _fluidAccumBuffer = registry.Create(
                "_FluidAccum",
                FluidAccumSlotCount,
                sizeof(uint),
                V4BufferLifetime.PerFrame);

            _initialized = true;
        }

        public void UploadPendulumState(V4SphericalPendulumController pendulum)
        {
            if (!_initialized || pendulum == null)
            {
                return;
            }

            float attachY = ResolveHangEarLocalY(pendulum);
            V4GpuBucketState state = V4GpuBucketState.FromPendulumParams(
                pendulum.GetPivotWorld(),
                pendulum.ropeLength,
                pendulum.UnitDirection,
                pendulum.TangentialVelocity,
                pendulum.gravity,
                pendulum.dampingCoefficient,
                pendulum.bucketMass,
                pendulum.sloshFeedbackScale,
                pendulum.fluidMassSmoothing,
                pendulum.twistAngleDegrees,
                applySlosh: true,
                attachY);

            V4GpuBucketState.ApplySceneTransformPose(
                ref state,
                pendulum.transform,
                pendulum.GetPivotWorld(),
                attachY);

            _bucketStateBuffer.SetData(new[] { state });
            LatestState = state;
            pendulum.SyncStateFromGpu(state);
        }

        public void ResetFromPendulum(V4SphericalPendulumController pendulum)
        {
            if (!_initialized || pendulum == null)
            {
                return;
            }

            if (pendulum.adoptManualBucketPoseOnReset)
            {
                pendulum.ApplyManualRestPoseToTransform();
            }
            else
            {
                pendulum.CaptureRestPoseFromScene();
            }

            Vector3 pivot = pendulum.GetPivotWorld();
            Vector3 bucketWorld = pendulum.transform.position;
            float attachY = ResolveHangEarLocalY(pendulum);
            Vector3 hangPoint = V4SphericalPendulumMath.HangPointFromFloor(
                bucketWorld,
                pendulum.transform.rotation,
                attachY);
            Vector3 delta = hangPoint - pivot;
            Vector3 unitDir = delta.sqrMagnitude > 1e-8f ? delta.normalized : Vector3.down;
            Vector3 tangential = V4SphericalPendulumMath.ProjectOntoTangentPlane(
                pendulum.initialTangentialVelocity,
                unitDir);

            V4GpuBucketState state = V4GpuBucketState.FromScenePose(
                pivot,
                bucketWorld,
                pendulum.transform.rotation,
                unitDir,
                delta.sqrMagnitude > 1e-8f ? delta.magnitude : pendulum.ropeLength,
                tangential,
                pendulum.gravity,
                pendulum.dampingCoefficient,
                pendulum.bucketMass,
                pendulum.sloshFeedbackScale,
                pendulum.fluidMassSmoothing,
                pendulum.twistAngleDegrees,
                applySlosh: true,
                attachY);

            _bucketStateBuffer.SetData(new[] { state });
            LatestState = state;
            pendulum.SyncStateFromGpu(state);
        }

        public void SyncPoseFromTransform(Transform bucketTransform, Vector3 pivotWorld, float hangEarLocalY)
        {
            if (!_initialized || bucketTransform == null)
            {
                return;
            }

            _bucketStateBuffer.GetData(_cpuScratch);
            V4GpuBucketState state = _cpuScratch[0];
            V4GpuBucketState.ApplySceneTransformPose(ref state, bucketTransform, pivotWorld, hangEarLocalY);
            _cpuScratch[0] = state;
            _bucketStateBuffer.SetData(_cpuScratch);
            LatestState = state;
        }

        public void DispatchMassReduce(
            ComputeBuffer block0,
            ComputeBuffer block1,
            ComputeBuffer flags,
            float particleMass,
            int activeCount,
            float innerRadius,
            float height,
            float wallThickness)
        {
            if (!_initialized || activeCount <= 0)
            {
                return;
            }

            _fluidAccumBuffer.SetData(new uint[FluidAccumSlotCount]);
            _massReduceShader.SetBuffer(_massReduceKernel, BucketStateId, _bucketStateBuffer);
            _massReduceShader.SetBuffer(_massReduceKernel, Shader.PropertyToID("_Block0"), block0);
            _massReduceShader.SetBuffer(_massReduceKernel, Shader.PropertyToID("_Block1"), block1);
            _massReduceShader.SetBuffer(_massReduceKernel, Shader.PropertyToID("_Flags"), flags);
            _massReduceShader.SetBuffer(_massReduceKernel, MassReduceAccumId, _fluidAccumBuffer);
            _massReduceShader.SetFloat(Shader.PropertyToID("_ParticleMass"), particleMass);
            _massReduceShader.SetInt(Shader.PropertyToID("_ActiveParticleCount"), activeCount);
            _massReduceShader.SetFloat(Shader.PropertyToID("_BucketInnerRadius"), innerRadius);
            _massReduceShader.SetFloat(Shader.PropertyToID("_BucketHeight"), height);
            _massReduceShader.SetFloat(Shader.PropertyToID("_BucketWallThickness"), wallThickness);

            int groups = Mathf.CeilToInt(activeCount / 64f);
            _massReduceShader.Dispatch(_massReduceKernel, groups, 1, 1);
        }

        public void DispatchApplyMassReduce()
        {
            if (!_initialized)
            {
                return;
            }

            _bucketPhysicsShader.SetBuffer(_applyMassReduceKernel, BucketStateId, _bucketStateBuffer);
            _bucketPhysicsShader.SetBuffer(_applyMassReduceKernel, FluidAccumId, _fluidAccumBuffer);
            _bucketPhysicsShader.Dispatch(_applyMassReduceKernel, 1, 1, 1);
        }

        public void DispatchIntegrate(float deltaTime, float hangEarLocalY)
        {
            if (!_initialized)
            {
                return;
            }

            _bucketPhysicsShader.SetBuffer(_integrateKernel, BucketStateId, _bucketStateBuffer);
            _bucketPhysicsShader.SetFloat(DeltaTimeId, deltaTime);
            _bucketPhysicsShader.SetFloat(BucketHangEarLocalYId, hangEarLocalY);
            _bucketPhysicsShader.Dispatch(_integrateKernel, 1, 1, 1);
        }

        public void DispatchUploadFromCpu(V4Bucket bucket)
        {
            if (!_initialized || bucket == null)
            {
                return;
            }

            _bucketPhysicsShader.SetBuffer(_uploadKernel, BucketStateId, _bucketStateBuffer);
            _bucketPhysicsShader.SetMatrix(CpuBucketWorldToLocalId, bucket.WorldToLocal);
            _bucketPhysicsShader.SetMatrix(CpuBucketLocalToWorldId, bucket.LocalToWorld);
            _bucketPhysicsShader.SetVector(CpuBucketLinearVelocityId, bucket.LinearVelocity);
            _bucketPhysicsShader.SetVector(CpuBucketAngularVelocityId, bucket.AngularVelocity);
            _bucketPhysicsShader.SetVector(CpuBucketAngularAccelerationId, bucket.AngularAcceleration);
            _bucketPhysicsShader.SetVector(CpuBucketWorldOriginId, bucket.transform.position);
            _bucketPhysicsShader.Dispatch(_uploadKernel, 1, 1, 1);
        }

        public void ReadStateToCpu()
        {
            if (!_initialized)
            {
                return;
            }

            _bucketStateBuffer.GetData(_cpuScratch);
            LatestState = _cpuScratch[0];
        }

        public void SyncTransformIfRequested(Transform target)
        {
            if (!syncTransformToCpu || target == null || !_initialized)
            {
                return;
            }

            ReadStateToCpu();
            V4GpuBucketState state = LatestState;
            var rot = new Quaternion(state.worldRotation.x, state.worldRotation.y, state.worldRotation.z, state.worldRotation.w);
            target.SetPositionAndRotation(state.worldOrigin, rot);
        }

        public void ApplyStateToBucket(V4Bucket bucket)
        {
            if (!_initialized || bucket == null)
            {
                return;
            }

            ReadStateToCpu();
            V4GpuBucketState state = LatestState;
            bucket.SetAnalyticKinematics(
                state.linearVelocity,
                state.angularVelocity,
                state.angularAcceleration,
                useAnalytic: state.useGpuState > 0.5f);
        }

        private static readonly int BucketWorldToLocalId = Shader.PropertyToID("_BucketWorldToLocal");
        private static readonly int BucketLocalToWorldId = Shader.PropertyToID("_BucketLocalToWorld");
        private static readonly int BucketWorldOriginUniformId = Shader.PropertyToID("_BucketWorldOrigin");

        public static void SetBucketTransformUniforms(ComputeShader shader, V4Bucket bucket)
        {
            if (shader == null || bucket == null)
            {
                return;
            }

            shader.SetMatrix(BucketWorldToLocalId, bucket.WorldToLocal);
            shader.SetMatrix(BucketLocalToWorldId, bucket.LocalToWorld);
            shader.SetVector(BucketWorldOriginUniformId, bucket.transform.position);
        }

        public void SetMassReduceTransformUniforms(V4Bucket bucket)
        {
            SetBucketTransformUniforms(_massReduceShader, bucket);
        }

        public void BindBucketStateToShader(ComputeShader shader, params int[] kernelIndices)
        {
            if (!_initialized || shader == null)
            {
                return;
            }

            foreach (int kernel in kernelIndices)
            {
                shader.SetBuffer(kernel, BucketStateId, _bucketStateBuffer);
            }
        }

        private void OnDestroy()
        {
            _initialized = false;
        }
    }
}
