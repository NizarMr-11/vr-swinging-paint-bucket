using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// GPU probe that aggregates in-bucket fluid mass, center of mass, and momentum
    /// for spherical pendulum slosh feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4Bucket))]
    [DefaultExecutionOrder(50)]
    public sealed class V4FluidMassProbe : MonoBehaviour
    {
        private const int AccumSlotCount = 8;
        private const float MicroScale = 1_000_000f;

        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int Block1Id = Shader.PropertyToID("_Block1");
        private static readonly int FlagsId = Shader.PropertyToID("_Flags");
        private static readonly int BucketWorldToLocalId = Shader.PropertyToID("_BucketWorldToLocal");
        private static readonly int BucketInnerRadiusId = Shader.PropertyToID("_BucketInnerRadius");
        private static readonly int BucketHeightId = Shader.PropertyToID("_BucketHeight");
        private static readonly int BucketWallThicknessId = Shader.PropertyToID("_BucketWallThickness");
        private static readonly int ParticleMassId = Shader.PropertyToID("_ParticleMass");
        private static readonly int ActiveParticleCountId = Shader.PropertyToID("_ActiveParticleCount");
        private static readonly int AccumId = Shader.PropertyToID("_Accum");

        private V4Bucket _bucket;
        private V4PipelineRoot _pipeline;
        private ComputeShader _shader;
        private int _kernel;
        private ComputeBuffer _accumBuffer;
        private readonly uint[] _accumScratch = new uint[AccumSlotCount];

        public V4FluidMassStats LatestStats { get; private set; } = V4FluidMassStats.Empty;

        private void Awake()
        {
            _bucket = GetComponent<V4Bucket>();
            _pipeline = GetComponentInParent<V4PipelineRoot>();
            if (_pipeline == null)
            {
                _pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }
        }

        private void OnDestroy()
        {
            _accumBuffer?.Release();
        }

        private void LateUpdate()
        {
            if (_pipeline == null || !_pipeline.Initialized || _pipeline.ActiveParticleCount <= 0)
            {
                LatestStats = V4FluidMassStats.Empty;
                return;
            }

            EnsureShader();
            if (_shader == null || _accumBuffer == null)
            {
                return;
            }

            float particleMass = ComputeParticleMass();
            _accumBuffer.SetData(new uint[AccumSlotCount]);

            _shader.SetBuffer(_kernel, Block0Id, _pipeline.Soa.ReadBlock0);
            _shader.SetBuffer(_kernel, Block1Id, _pipeline.Soa.ReadBlock1);
            _shader.SetBuffer(_kernel, FlagsId, _pipeline.Soa.ReadFlags);
            _shader.SetBuffer(_kernel, AccumId, _accumBuffer);
            _shader.SetMatrix(BucketWorldToLocalId, _bucket.WorldToLocal);
            _shader.SetFloat(BucketInnerRadiusId, _bucket.innerRadius);
            _shader.SetFloat(BucketHeightId, _bucket.height);
            _shader.SetFloat(BucketWallThicknessId, _bucket.wallThickness);
            _shader.SetFloat(ParticleMassId, particleMass);
            _shader.SetInt(ActiveParticleCountId, _pipeline.ActiveParticleCount);

            int groups = Mathf.CeilToInt(_pipeline.ActiveParticleCount / 64f);
            _shader.Dispatch(_kernel, groups, 1, 1);

            _accumBuffer.GetData(_accumScratch);
            LatestStats = DecodeStats(_accumScratch);
        }

        private void EnsureShader()
        {
            if (_shader != null)
            {
                return;
            }

            _shader = V4ShaderLibrary.Load(V4ShaderLibrary.FluidMassReduce);
            _kernel = _shader.FindKernel("FluidMassReduceKernel");
            _accumBuffer = new ComputeBuffer(AccumSlotCount, sizeof(uint), ComputeBufferType.Structured);
        }

        private float ComputeParticleMass()
        {
            float radius = _pipeline.ParticleRadius;
            float density = _pipeline.GpuRestDensity();
            return density * (4f / 3f * Mathf.PI * radius * radius * radius);
        }

        private static V4FluidMassStats DecodeStats(uint[] accum)
        {
            float totalMass = accum[0] / MicroScale;
            if (totalMass <= 1e-8f)
            {
                return V4FluidMassStats.Empty;
            }

            return new V4FluidMassStats
            {
                totalMass = totalMass,
                centerOfMassWorld = new Vector3(
                    accum[1] / MicroScale,
                    accum[2] / MicroScale,
                    accum[3] / MicroScale) / totalMass,
                momentumWorld = new Vector3(
                    accum[4] / MicroScale,
                    accum[5] / MicroScale,
                    accum[6] / MicroScale),
                particleCount = (int)accum[7]
            };
        }
    }
}
