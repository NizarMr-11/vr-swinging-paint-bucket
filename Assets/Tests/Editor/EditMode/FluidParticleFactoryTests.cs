using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class FluidParticleFactoryTests
    {
        [Test]
        public void FromLocalSpawn_InitializesDensity()
        {
            FluidParticle p = FluidParticleFactory.FromLocalSpawn(
                new Unity.Mathematics.float3(0.1f, 0.2f, 0.3f),
                Unity.Mathematics.float3.zero,
                restDensity: 1000f);

            Assert.AreEqual(1000f, p.Density, 1e-3f);
            Assert.AreEqual(0f, p.Pressure, 1e-3f);
        }

        [Test]
        public void FromWorldSpawn_ConvertsToBucketLocalSpace()
        {
            var bucketGo = new GameObject("Bucket");
            bucketGo.transform.position = new Vector3(10f, 0f, 0f);

            FluidParticle p = FluidParticleFactory.FromWorldSpawn(
                new Vector3(10f, 1f, 0f),
                Vector3.right,
                bucketGo.transform,
                1000f,
                0.02f);

            Assert.AreEqual(0f, p.Position.x, 1e-3f);
            Assert.AreEqual(1f, p.Position.y, 1e-3f);
            Assert.AreEqual(0f, p.Position.z, 1e-3f);
            Object.DestroyImmediate(bucketGo);
        }
    }
}
