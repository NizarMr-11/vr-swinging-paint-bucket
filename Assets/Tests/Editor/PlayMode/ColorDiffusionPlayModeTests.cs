using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class ColorDiffusionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ZeroDiffusionRate_KeepsDistinctColorGroups()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            ClusterAverages averages = default;
            yield return MeasureClusters(0f, result => averages = result);

            Assert.Greater(averages.LeftAvgR, 0.55f, "Left cluster should stay red-dominant");
            Assert.Less(averages.LeftAvgB, 0.25f);
            Assert.Greater(averages.RightAvgB, 0.55f, "Right cluster should stay blue-dominant");
            Assert.Less(averages.RightAvgR, 0.25f);
        }

        [UnityTest]
        public IEnumerator HighDiffusionRate_BlendsColorsAcrossClusters()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            ClusterAverages zero = default;
            ClusterAverages high = default;
            yield return MeasureClusters(0f, result => zero = result);
            yield return MeasureClusters(4f, result => high = result);

            float zeroContrast = zero.LeftAvgR - zero.LeftAvgB;
            float highContrast = high.LeftAvgR - high.LeftAvgB;
            Assert.Less(highContrast, zeroContrast * 0.85f,
                "Higher diffusion should reduce red/blue dominance contrast on the left cluster");
        }

        private static IEnumerator MeasureClusters(float diffusionRate, System.Action<ClusterAverages> onComplete)
        {
            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 256, autoRun: false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetColorDiffusionRate(diffusionRate);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 0.8f,
                floorY = 0f,
                rimY = 1.5f
            });
            pipeline.SetSimulationActive(true);

            HarmonicParticleSpawner.Spawn(pipeline, new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(-0.12f, 0.45f, 0f),
                sphereRadius = 0.08f,
                particleCount = 16,
                spawnColor = Color.red,
                seed = 5u
            });

            HarmonicParticleSpawner.Spawn(pipeline, new HarmonicSpawnRegion
            {
                shape = ShapeVolumeType.Sphere,
                center = new Vector3(0.12f, 0.45f, 0f),
                sphereRadius = 0.08f,
                particleCount = 16,
                spawnColor = Color.blue,
                seed = 6u
            });

            const int frames = 30;
            for (int frame = 0; frame < frames; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            Assert.IsTrue(
                pipeline.TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count));
            Assert.Greater(count, 0u);

            var particles = new FluidParticle[count];
            buffer.GetData(particles, 0, 0, (int)count);

            var averages = new ClusterAverages();
            int leftCount = 0;
            int rightCount = 0;

            for (int i = 0; i < particles.Length; i++)
            {
                Color c = FluidParticleFactory.UnpackColor(particles[i].PackedColorRGBA);
                if (particles[i].Position.x < 0f)
                {
                    averages.LeftAvgR += c.r;
                    averages.LeftAvgB += c.b;
                    leftCount++;
                }
                else
                {
                    averages.RightAvgR += c.r;
                    averages.RightAvgB += c.b;
                    rightCount++;
                }
            }

            Assert.Greater(leftCount, 0);
            Assert.Greater(rightCount, 0);

            averages.LeftAvgR /= leftCount;
            averages.LeftAvgB /= leftCount;
            averages.RightAvgR /= rightCount;
            averages.RightAvgB /= rightCount;

            onComplete?.Invoke(averages);
            Object.DestroyImmediate(pipeline.gameObject);
        }

        private struct ClusterAverages
        {
            public float LeftAvgR;
            public float LeftAvgB;
            public float RightAvgR;
            public float RightAvgB;
        }
    }
}
