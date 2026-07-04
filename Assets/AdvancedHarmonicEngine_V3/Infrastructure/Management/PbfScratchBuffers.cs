using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// GPU scratch buffers for Position Based Fluids (Macklin &amp; Müller 2013).
    /// Predicted/old positions are stored as float4 xyz buffers for spatial-hash compatibility.
    /// </summary>
    public sealed class PbfScratchBuffers
    {
        /// <summary>Predicted position (xyz) — bound as <c>_Block0</c> for spatial hash.</summary>
        public ComputeBuffer PredictedBlock0;
        /// <summary>Pre-step position (xyz) for velocity derivation.</summary>
        public ComputeBuffer OldBlock0;
        public ComputeBuffer Lambdas;
        /// <summary>Σ|∇C|² per particle from the lambda pass (solver tuning output).</summary>
        public ComputeBuffer GradSqSum;
        /// <summary>Per-particle OtcSampleField classification (dist + region flags).</summary>
        public ComputeBuffer ParticleField;

        public static PbfScratchBuffers Create(int maxCapacity)
        {
            const int particleFieldStride = sizeof(float) + sizeof(uint) + sizeof(float) * 2;
            return new PbfScratchBuffers
            {
                PredictedBlock0 = new ComputeBuffer(maxCapacity, sizeof(float) * 4, ComputeBufferType.Structured),
                OldBlock0 = new ComputeBuffer(maxCapacity, sizeof(float) * 4, ComputeBufferType.Structured),
                Lambdas = new ComputeBuffer(maxCapacity, sizeof(float), ComputeBufferType.Structured),
                GradSqSum = new ComputeBuffer(maxCapacity, sizeof(float), ComputeBufferType.Structured),
                ParticleField = new ComputeBuffer(maxCapacity, particleFieldStride, ComputeBufferType.Structured)
            };
        }

        public void Release()
        {
            PredictedBlock0?.Release();
            OldBlock0?.Release();
            Lambdas?.Release();
            GradSqSum?.Release();
            ParticleField?.Release();
            PredictedBlock0 = null;
            OldBlock0 = null;
            Lambdas = null;
            GradSqSum = null;
            ParticleField = null;
        }
    }
}
