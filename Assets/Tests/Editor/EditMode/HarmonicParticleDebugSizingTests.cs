using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class HarmonicParticleDebugSizingTests
    {
        private sealed class StubSource : IHarmonicParticleSource
        {
            public StubSource(float smoothingRadius) => SmoothingRadius = smoothingRadius;

            public int MaxCapacity => 30_000;
            public float CellSize => 0.1f;
            public float SmoothingRadius { get; }
            public int FrameSortSize => 256;
            public bool ContainerFluidEnabled => true;
            public bool WorldFallingOnly => false;

            public uint GetActiveParticleCount() => 0;

            public bool TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count)
            {
                buffer = null;
                count = 0;
                return false;
            }

            public bool TryGetFallingParticleBuffer(out ComputeBuffer buffer, out uint count)
            {
                buffer = null;
                count = 0;
                return false;
            }
        }

        [Test]
        public void ResolvePointRadius_UsesSmoothingRadiusWhenAutoSizeEnabled()
        {
            var source = new StubSource(smoothingRadius: 0.2f);
            float radius = HarmonicParticleDebugSizing.ResolvePointRadius(source, autoSizeFromSph: true, 1.05f, 0.18f);
            Assert.AreEqual(0.21f, radius, 1e-4f);
        }

        [Test]
        public void ResolvePointRadius_UsesManualSizeWhenAutoSizeDisabled()
        {
            var source = new StubSource(smoothingRadius: 0.2f);
            float radius = HarmonicParticleDebugSizing.ResolvePointRadius(source, autoSizeFromSph: false, 1.05f, 0.18f);
            Assert.AreEqual(0.18f, radius, 1e-4f);
        }

        [Test]
        public void ResolvePointRadius_ReturnsManualWhenSourceNull()
        {
            float radius = HarmonicParticleDebugSizing.ResolvePointRadius(null, autoSizeFromSph: true, 1.05f, 0.25f);
            Assert.AreEqual(0.25f, radius, 1e-4f);
        }
    }
}
