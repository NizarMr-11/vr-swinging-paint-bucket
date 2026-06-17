using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class PingPongCounterManagerTests
    {
        [Test]
        public void Swap_TogglesReadWriteBuffers()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            var a = new ComputeBuffer(16, 32, ComputeBufferType.Append);
            var b = new ComputeBuffer(16, 32, ComputeBufferType.Append);
            var manager = new PingPongCounterManager(a, b);

            ComputeBuffer firstRead = manager.ReadBuffer;
            ComputeBuffer firstWrite = manager.WriteBuffer;
            manager.Swap();

            Assert.AreNotSame(firstRead, manager.ReadBuffer);
            Assert.AreNotSame(firstWrite, manager.WriteBuffer);
            Assert.AreSame(firstRead, manager.WriteBuffer);
            Assert.AreSame(firstWrite, manager.ReadBuffer);

            a.Release();
            b.Release();
        }
    }
}
