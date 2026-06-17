using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Tests.PlayMode;
using NUnit.Framework;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("Stress")]
    public class HarmonicPipelineStressPlayModeTests
    {
        [UnityTest]
        public System.Collections.IEnumerator Pipeline_100kParticles_CompletesFramesUnderBudget()
        {
            yield return PlayModeTestUtility.EnsurePlayMode();

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 100_000, autoRun: false);
            var particles = new FluidParticle[4096];
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i] = new FluidParticle
                {
                    Position = Unity.Mathematics.float3.zero,
                    Velocity = Unity.Mathematics.float3.zero,
                    Density = 1000f,
                    Pressure = 0f
                };
            }

            pipeline.SetSimulationActive(true);
            pipeline.AppendParticles(particles, particles.Length);

            var stopwatch = Stopwatch.StartNew();
            for (int frame = 0; frame < 30; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            stopwatch.Stop();
            Assert.Less(stopwatch.ElapsedMilliseconds, 30_000, "100k stress frames exceeded manual budget.");

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
