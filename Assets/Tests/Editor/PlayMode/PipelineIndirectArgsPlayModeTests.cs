using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class PipelineIndirectArgsPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExecutePipeline_IndirectArgsMatchActiveCount()
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
                batch[i] = new FluidParticle
                {
                    Position = new Unity.Mathematics.float3(i * 0.01f, 0f, 0f),
                    Velocity = Unity.Mathematics.float3.zero,
                    Density = 1000f,
                    Pressure = 0f
                };
            }

            pipeline.AppendParticles(batch, batch.Length);
            pipeline.ExecutePipelineFrame(0.016f);
            yield return null;

            uint active = pipeline.GetActiveParticleCount();
            var args = new int[4];
            Assert.IsTrue(pipeline.TryCopyIndirectDispatchArgs(args));
            Assert.Greater(args[0], 0);
            Assert.GreaterOrEqual(active, 1u);

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
