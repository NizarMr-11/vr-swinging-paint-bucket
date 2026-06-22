using HarmonicEngine.Domain.Adapters;
using NUnit.Framework;
using Unity.Mathematics;

namespace HarmonicEngine.Tests
{
    public class ShapeVolumeSamplerTests
    {
        [Test]
        public void SampleSphere_AllPointsInsideRadius()
        {
            const int count = 1000;
            const float radius = 1.5f;
            float3 center = new(2f, -1f, 3f);
            var outPositions = new float3[count];

            int n = ShapeVolumeSampler.SampleSphere(center, radius, count, 1234u, outPositions);

            Assert.AreEqual(count, n);
            for (int i = 0; i < n; i++)
            {
                float dist = math.length(outPositions[i] - center);
                Assert.LessOrEqual(dist, radius + 1e-3f, $"Point {i} outside sphere: dist={dist}");
            }
        }

        [Test]
        public void SampleBox_AllPointsInsideExtents()
        {
            const int count = 1000;
            float3 center = new(0f, 5f, 0f);
            float3 size = new(2f, 4f, 1f);
            var outPositions = new float3[count];

            int n = ShapeVolumeSampler.SampleBox(center, size, quaternion.identity, count, 77u, outPositions);

            Assert.AreEqual(count, n);
            float3 half = size * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float3 local = outPositions[i] - center;
                Assert.LessOrEqual(math.abs(local.x), half.x + 1e-3f);
                Assert.LessOrEqual(math.abs(local.y), half.y + 1e-3f);
                Assert.LessOrEqual(math.abs(local.z), half.z + 1e-3f);
            }
        }

        [Test]
        public void SampleCapsule_AllPointsInsideVolume()
        {
            const int count = 1000;
            const float radius = 0.5f;
            const float height = 3f;
            float3 center = float3.zero;
            var outPositions = new float3[count];

            int n = ShapeVolumeSampler.SampleCapsule(
                center, radius, height, quaternion.identity, count, 555u, outPositions);

            Assert.AreEqual(count, n);
            float cylinderHalf = math.max(0f, height - 2f * radius) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float3 p = outPositions[i];
                float clampedY = math.clamp(p.y, -cylinderHalf, cylinderHalf);
                float3 axisPoint = new(0f, clampedY, 0f);
                float distToAxis = math.length(p - axisPoint);
                Assert.LessOrEqual(distToAxis, radius + 1e-3f, $"Point {i} outside capsule: dist={distToAxis}");
            }
        }

        [Test]
        public void SampleCylinder_AllPointsInsideVolume()
        {
            const int count = 1000;
            const float radius = 0.5f;
            const float height = 2f;
            float3 center = new(1f, 2f, 3f);
            quaternion rotation = quaternion.Euler(math.radians(15f), math.radians(30f), 0f);
            var outPositions = new float3[count];

            int n = ShapeVolumeSampler.SampleCylinder(
                center, radius, height, rotation, count, 888u, outPositions);

            Assert.AreEqual(count, n);
            float halfHeight = height * 0.5f;
            quaternion inverseRotation = math.inverse(rotation);
            for (int i = 0; i < n; i++)
            {
                float3 local = math.mul(inverseRotation, outPositions[i] - center);
                Assert.LessOrEqual(math.abs(local.y), halfHeight + 1e-3f);
                float radial = math.length(new float2(local.x, local.z));
                Assert.LessOrEqual(radial, radius + 1e-3f, $"Point {i} outside cylinder: radial={radial}");
            }
        }

        [Test]
        public void SampleSphere_IsDeterministicForSameSeed()
        {
            var a = new float3[64];
            var b = new float3[64];

            ShapeVolumeSampler.SampleSphere(float3.zero, 1f, 64, 42u, a);
            ShapeVolumeSampler.SampleSphere(float3.zero, 1f, 64, 42u, b);

            for (int i = 0; i < 64; i++)
            {
                Assert.AreEqual(a[i].x, b[i].x, 1e-6f);
                Assert.AreEqual(a[i].y, b[i].y, 1e-6f);
                Assert.AreEqual(a[i].z, b[i].z, 1e-6f);
            }
        }

        [Test]
        public void Sample_ClampsToOutputBufferLength()
        {
            var outPositions = new float3[10];
            int n = ShapeVolumeSampler.SampleSphere(float3.zero, 1f, 1000, 1u, outPositions);
            Assert.AreEqual(10, n);
        }
    }
}
