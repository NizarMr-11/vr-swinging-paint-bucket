using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// GPU particle fields in packed SOA (4 buffers, D3D11-safe UAV count).
    /// Block0 holds the authoritative append counter.
    /// </summary>
    public sealed class ParticleSoaBuffers
    {
        /// <summary>float4(position.xyz, density)</summary>
        public ComputeBuffer Block0;
        /// <summary>float4(velocity.xyz, pressure)</summary>
        public ComputeBuffer Block1;
        public ComputeBuffer PackedColors;
        public ComputeBuffer Wetness;

        public ComputeBuffer CounterBuffer => Block0;

        public static ParticleSoaBuffers Create(int maxCapacity, ComputeBufferType bufferType)
        {
            return new ParticleSoaBuffers
            {
                Block0 = new ComputeBuffer(maxCapacity, sizeof(float) * 4, bufferType),
                Block1 = new ComputeBuffer(maxCapacity, sizeof(float) * 4, bufferType),
                PackedColors = new ComputeBuffer(maxCapacity, sizeof(uint), bufferType),
                Wetness = new ComputeBuffer(maxCapacity, sizeof(float), bufferType)
            };
        }

        public void SetCounterValue(uint count)
        {
            Block0?.SetCounterValue(count);
            Block1?.SetCounterValue(count);
            PackedColors?.SetCounterValue(count);
            Wetness?.SetCounterValue(count);
        }

        public void Release()
        {
            Block0?.Release();
            Block1?.Release();
            PackedColors?.Release();
            Wetness?.Release();
            Block0 = null;
            Block1 = null;
            PackedColors = null;
            Wetness = null;
        }
    }
}
