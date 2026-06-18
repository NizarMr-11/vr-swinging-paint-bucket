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
    public class CanvasPerHitColorPlayModeTests
    {
        [UnityTest]
        public IEnumerator FallingParticleHit_CarriesPackedColorInCanvasHitBuffer()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            const float planeY = 0f;
            uint green = FluidParticleFactory.PackColor(Color.green);

            var pipeline = TestPipelineFactory.CreatePipeline(capacity: 64, autoRun: false);
            pipeline.SetWorldFallingOnly(true);
            pipeline.SetCanvasSurface(new HarmonicCanvasSurface
            {
                planeY = planeY,
                cullIntoCanvas = true,
                paintAbsorbEnabled = false
            });
            pipeline.SetSimulationActive(true);

            pipeline.AppendParticles(new[]
            {
                FluidParticleFactory.FromWorldPosition(
                    new Vector3(0f, 0.5f, 0f),
                    Vector3.down * 2f,
                    1000f,
                    green)
            }, 1);

            pipeline.ExecutePipelineFrame(0.05f);
            yield return null;

            Assert.IsTrue(pipeline.TryGetCanvasHitBuffer(out ComputeBuffer hitBuffer, out uint hitCount));
            Assert.Greater(hitCount, 0u, "Expected at least one canvas hit");

            var hits = new CanvasPaintHit[hitCount];
            hitBuffer.GetData(hits, 0, 0, (int)hitCount);
            Assert.AreEqual(green, hits[0].PackedColorRGBA);

            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
