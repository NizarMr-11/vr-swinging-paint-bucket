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
    public class SpatialHashGridPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpatialHash_AfterFrame_KeysSortedAndRangesValid()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 512, autoRun: false);
            pipeline.SetContainerFluidEnabled(true);
            pipeline.SetBucketVolume(new HarmonicBucketVolume
            {
                center = Vector3.zero,
                radius = 1f,
                floorY = 0f,
                rimY = 2f
            });

            const int count = 32;
            var particles = new FluidParticle[count];
            for (int i = 0; i < count; i++)
            {
                float t = i * 0.05f;
                particles[i] = new FluidParticle
                {
                    Position = new Unity.Mathematics.float3(t, 0.5f, 0f),
                    Velocity = Unity.Mathematics.float3.zero,
                    Density = 1000f,
                    Pressure = 0f,
                    PackedColorRGBA = FluidParticleFactory.WhiteRGBA
                };
            }

            pipeline.SetSimulationActive(true);
            pipeline.AppendParticles(particles, count);
            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            Assert.IsTrue(
                pipeline.TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize));
            Assert.Greater(sortSize, 0);

            var keys = new GridKeyPair[sortSize];
            gridKeys.GetData(keys);

            for (int i = 1; i < sortSize; i++)
            {
                if (keys[i].CellHash == 0xFFFFFFFFu)
                {
                    continue;
                }

                Assert.LessOrEqual(keys[i - 1].CellHash, keys[i].CellHash, $"Grid keys not sorted at index {i}");
            }

            var ranges = new HashCellGridRange[sortSize];
            cellRanges.GetData(ranges);

            int populatedCells = 0;
            for (int h = 0; h < sortSize; h++)
            {
                HashCellGridRange range = ranges[h];
                if (range.StartIndex < 0)
                {
                    continue;
                }

                populatedCells++;
                Assert.GreaterOrEqual(range.EndIndex, range.StartIndex);
                Assert.Less(range.StartIndex, sortSize);
                Assert.Less(range.EndIndex, sortSize);
            }

            Assert.Greater(populatedCells, 0, "Expected at least one populated hash cell");

            uint active = pipeline.GetActiveParticleCount();
            Assert.Greater(active, 0u);
            Assert.LessOrEqual(active, (uint)count);

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
