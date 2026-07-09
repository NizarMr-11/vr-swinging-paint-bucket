using HarmonicEngineV4.Core;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// GPU probe that aggregates in-bucket fluid mass for debug readback.
    /// In GPU pendulum mode the pipeline runs mass reduce internally; this component is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4Bucket))]
    [DefaultExecutionOrder(50)]
    public sealed class V4FluidMassProbe : MonoBehaviour
    {
        private V4Bucket _bucket;
        private V4PipelineRoot _pipeline;
        private V4BucketMotionSettings _motionSettings;

        public V4FluidMassStats LatestStats { get; private set; } = V4FluidMassStats.Empty;

        private void Awake()
        {
            _bucket = GetComponent<V4Bucket>();
            _pipeline = GetComponentInParent<V4PipelineRoot>();
            if (_pipeline == null)
            {
                _pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            _motionSettings = GetComponent<V4BucketMotionSettings>();
        }

        private void LateUpdate()
        {
            if (ShouldSkipGpuProbe())
            {
                LatestStats = V4FluidMassStats.Empty;
                return;
            }

            if (_pipeline == null || !_pipeline.Initialized || _pipeline.GpuBucketDriver == null)
            {
                LatestStats = V4FluidMassStats.Empty;
                return;
            }

            _pipeline.GpuBucketDriver.ReadStateToCpu();
            V4GpuBucketState state = _pipeline.GpuBucketDriver.LatestState;
            if (state.totalFluidMass <= 1e-8f)
            {
                LatestStats = V4FluidMassStats.Empty;
                return;
            }

            LatestStats = new V4FluidMassStats
            {
                totalMass = state.totalFluidMass,
                centerOfMassWorld = state.worldOrigin + state.comOffsetWorld,
                momentumWorld = state.linearVelocity * state.totalFluidMass,
                particleCount = 0
            };
        }

        private bool ShouldSkipGpuProbe()
        {
            return _motionSettings != null && _motionSettings.UseGpuPendulum;
        }
    }
}
