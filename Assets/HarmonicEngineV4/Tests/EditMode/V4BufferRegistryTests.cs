using System;
using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    public sealed class V4BufferRegistryTests
    {
        [Test]
        public void Create_RegistersBufferWithMetadata()
        {
            using var registry = new V4BufferRegistry();
            ComputeBuffer buffer = registry.Create("_TestBuffer", 16, sizeof(float), V4BufferLifetime.Persistent);

            Assert.IsNotNull(buffer);
            Assert.IsTrue(registry.Contains("_TestBuffer"));
            Assert.AreSame(buffer, registry.Get("_TestBuffer"));

            V4BufferRegistry.BufferInfo info = registry.GetInfo("_TestBuffer");
            Assert.AreEqual(16, info.Count);
            Assert.AreEqual(sizeof(float), info.Stride);
            Assert.AreEqual(V4BufferLifetime.Persistent, info.Lifetime);
            Assert.AreEqual(16 * sizeof(float), info.Bytes);
        }

        [Test]
        public void Create_DuplicateName_Throws()
        {
            using var registry = new V4BufferRegistry();
            registry.Create("_Dup", 4, 4, V4BufferLifetime.PerFrame);
            Assert.Throws<InvalidOperationException>(() => registry.Create("_Dup", 4, 4, V4BufferLifetime.PerFrame));
        }

        [Test]
        public void Get_UnknownName_Throws()
        {
            using var registry = new V4BufferRegistry();
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => registry.Get("_Missing"));
        }

        [Test]
        public void Assign_AllowsRetargetingAlias()
        {
            using var registry = new V4BufferRegistry();
            var bufferA = new ComputeBuffer(8, 4);
            var bufferB = new ComputeBuffer(8, 4);
            try
            {
                registry.Assign("_ReadAlias", bufferA, 8, 4, V4BufferLifetime.Persistent);
                Assert.AreSame(bufferA, registry.Get("_ReadAlias"));

                registry.Assign("_ReadAlias", bufferB, 8, 4, V4BufferLifetime.Persistent);
                Assert.AreSame(bufferB, registry.Get("_ReadAlias"));
            }
            finally
            {
                bufferA.Release();
                bufferB.Release();
            }
        }

        [Test]
        public void Assign_OverOwnedBuffer_Throws()
        {
            using var registry = new V4BufferRegistry();
            registry.Create("_Owned", 4, 4, V4BufferLifetime.Persistent);
            var external = new ComputeBuffer(4, 4);
            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => registry.Assign("_Owned", external, 4, 4, V4BufferLifetime.Persistent));
            }
            finally
            {
                external.Release();
            }
        }

        [Test]
        public void Dispose_ReleasesOwnedBuffers_AndBlocksFurtherUse()
        {
            var registry = new V4BufferRegistry();
            ComputeBuffer owned = registry.Create("_Owned", 4, 4, V4BufferLifetime.Persistent);
            registry.Dispose();

            Assert.IsFalse(owned.IsValid());
            Assert.Throws<ObjectDisposedException>(() => registry.Get("_Owned"));
            registry.Dispose();
        }

        [Test]
        public void Dispose_DoesNotReleaseExternalBuffers()
        {
            var registry = new V4BufferRegistry();
            var external = new ComputeBuffer(4, 4);
            registry.Assign("_External", external, 4, 4, V4BufferLifetime.Persistent);
            registry.Dispose();

            Assert.IsTrue(external.IsValid());
            external.Release();
        }

        [Test]
        public void TotalOwnedBytes_CountsOnlyOwnedBuffers()
        {
            using var registry = new V4BufferRegistry();
            registry.Create("_A", 10, 8, V4BufferLifetime.Persistent);
            var external = new ComputeBuffer(100, 16);
            try
            {
                registry.Assign("_B", external, 100, 16, V4BufferLifetime.Persistent);
                Assert.AreEqual(10 * 8, registry.TotalOwnedBytes);
            }
            finally
            {
                external.Release();
            }
        }
    }
}
