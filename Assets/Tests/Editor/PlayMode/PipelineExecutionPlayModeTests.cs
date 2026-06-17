using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    public class PipelineExecutionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExecutePipelineFrame_DoesNotThrow_WithParticles()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: 4096, autoRun: false);
            yield return null;

            var particle = FluidParticleFactory.FromLocalSpawn(
                new Unity.Mathematics.float3(0.1f, 0f, 0f),
                Unity.Mathematics.float3.zero,
                1000f);
            pipeline.AppendParticles(new[] { particle }, 1);

            Assert.DoesNotThrow(() => pipeline.ExecutePipelineFrame(0.016f));
            Assert.GreaterOrEqual(pipeline.GetActiveParticleCount(), 1u);

            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator ExecutePipelineFrame_RunsMultipleFrames_WithoutErrors()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: 4096, autoRun: false);
            yield return null;

            var batch = new FluidParticle[32];
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i] = FluidParticleFactory.FromLocalSpawn(
                    new Unity.Mathematics.float3(i * 0.02f, 0f, 0f),
                    Unity.Mathematics.float3.zero,
                    1000f);
            }

            pipeline.AppendParticles(batch, batch.Length);

            for (int frame = 0; frame < 5; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            Assert.Greater(pipeline.GetActiveParticleCount(), 0u);
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
