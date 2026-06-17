using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    /// <summary>
    /// Bridges legacy ParticleEmitter spawn events into the Harmonic V3 GPU append buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public class HarmonicGpuEmitterBridge : MonoBehaviour
    {
        [SerializeField] private PipelineExecutionController pipeline;
        [SerializeField] private Transform bucketTransform;

        private FluidParticle[] _batch;

        public void Bind(PipelineExecutionController controller, Transform bucket)
        {
            pipeline = controller;
            bucketTransform = bucket;
            pipeline.EnableExternalIngestion(true);
        }

        public bool TryIngestSpawn(Vector3 worldPosition, Vector3 worldVelocity, float restDensity)
        {
            if (pipeline == null)
            {
                return false;
            }

            Transform bucket = bucketTransform != null ? bucketTransform : transform;
            FluidParticle particle = FluidParticleFactory.FromWorldSpawn(
                worldPosition,
                worldVelocity,
                bucket,
                restDensity,
                mass: 0f);

            _batch ??= new FluidParticle[1];
            _batch[0] = particle;
            return pipeline.AppendParticles(_batch, 1) == 1;
        }
    }
}
