using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Tests.PlayMode;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class PipelineStabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExecutePipeline_100Frames_CountStaysWithinCapacity()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const int capacity = 4096;
            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: capacity, autoRun: false);
            yield return null;

            var batch = new FluidParticle[64];
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
            pipeline.SetSimulationActive(true);

            uint maxSeen = 0;
            for (int frame = 0; frame < 100; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                uint count = pipeline.GetActiveParticleCount();
                maxSeen = (uint)Mathf.Max(maxSeen, count);
                Assert.LessOrEqual(count, (uint)capacity);
                yield return null;
            }

            Assert.Greater(maxSeen, 0u);
            Object.DestroyImmediate(pipeline.gameObject);
            yield return PlayModeTestUtility.ExitPlayModeIfNeeded();
        }
    }
}
