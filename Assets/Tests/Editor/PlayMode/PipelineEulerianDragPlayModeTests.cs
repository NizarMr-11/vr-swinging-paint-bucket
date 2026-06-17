using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class PipelineEulerianDragPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExecutePipeline_EulerianDragOnOff_BothStable()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const int capacity = 4096;
            var batch = new FluidParticle[64];
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i] = new FluidParticle
                {
                    Position = new Unity.Mathematics.float3(i * 0.01f, 0.05f, 0f),
                    Velocity = new Unity.Mathematics.float3(0.1f, -0.2f, 0f),
                    Density = 1000f,
                    Pressure = 0f
                };
            }

            foreach (bool dragEnabled in new[] { false, true })
            {
                PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: capacity, autoRun: false);
                pipeline.SetEnableEulerianDrag(dragEnabled);
                yield return null;

                pipeline.AppendParticles(batch, batch.Length);
                for (int frame = 0; frame < 10; frame++)
                {
                    pipeline.ExecutePipelineFrame(0.016f);
                    Assert.LessOrEqual(pipeline.GetActiveParticleCount(), (uint)capacity);
                    yield return null;
                }

                Object.DestroyImmediate(pipeline.gameObject);
            }

        }
    }
}
