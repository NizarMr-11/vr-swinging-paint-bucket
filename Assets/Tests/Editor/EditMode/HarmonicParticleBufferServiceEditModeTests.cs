using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class HarmonicParticleBufferServiceEditModeTests
    {
        [Test]
        public void AppendParticles_RespectsCapacity()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
            }

            var a = new ComputeBuffer(8, 32, ComputeBufferType.Append);
            var b = new ComputeBuffer(8, 32, ComputeBufferType.Append);
            var counter = new ComputeBuffer(1, 4, ComputeBufferType.Raw);
            var pingPong = new PingPongCounterManager(a, b);
            var service = new HarmonicParticleBufferService(a, b, pingPong, maxCapacity: 4, counter);

            var batch = new[]
            {
                FluidParticleFactory.FromLocalSpawn(Unity.Mathematics.float3.zero, Unity.Mathematics.float3.zero, 1000f),
                FluidParticleFactory.FromLocalSpawn(new Unity.Mathematics.float3(0.1f, 0, 0), Unity.Mathematics.float3.zero, 1000f),
                FluidParticleFactory.FromLocalSpawn(new Unity.Mathematics.float3(0.2f, 0, 0), Unity.Mathematics.float3.zero, 1000f)
            };

            Assert.AreEqual(3, service.AppendParticles(batch, 3));
            Assert.AreEqual(1, service.AppendParticles(batch, 1));
            Assert.AreEqual(0, service.AppendParticles(batch, 1));
            Assert.AreEqual(4u, service.GetActiveCount());

            service.ClearAll();
            Assert.AreEqual(0u, service.GetActiveCount());

            a.Release();
            b.Release();
            counter.Release();
        }
    }
}
