using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class PingPongSoaManagerTests
    {
        [Test]
        public void Swap_TogglesReadWriteSets()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            var a = ParticleSoaBuffers.Create(16, ComputeBufferType.Append);
            var b = ParticleSoaBuffers.Create(16, ComputeBufferType.Append);
            var manager = new PingPongSoaManager(a, b);

            ParticleSoaBuffers firstRead = manager.ReadSet;
            ParticleSoaBuffers firstWrite = manager.WriteSet;
            manager.Swap();

            Assert.AreNotSame(firstRead, manager.ReadSet);
            Assert.AreNotSame(firstWrite, manager.WriteSet);
            Assert.AreSame(firstRead, manager.WriteSet);
            Assert.AreSame(firstWrite, manager.ReadSet);

            a.Release();
            b.Release();
        }
    }
}
