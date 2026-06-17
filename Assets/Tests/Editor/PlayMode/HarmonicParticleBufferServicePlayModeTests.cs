using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    public class HarmonicParticleBufferServicePlayModeTests
    {
        [UnityTest]
        public IEnumerator AppendParticles_IncreasesActiveCount()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline();
            yield return null;

            var batch = new FluidParticle[4];
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i] = FluidParticleFactory.FromLocalSpawn(
                    new Unity.Mathematics.float3(i * 0.05f, 0f, 0f),
                    Unity.Mathematics.float3.zero,
                    1000f);
            }

            int appended = pipeline.AppendParticles(batch, batch.Length);
            Assert.AreEqual(4, appended);
            Assert.AreEqual(4u, pipeline.GetActiveParticleCount());

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
