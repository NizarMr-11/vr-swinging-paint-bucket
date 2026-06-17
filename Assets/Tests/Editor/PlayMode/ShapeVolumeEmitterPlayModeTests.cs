using HarmonicEngine.Domain.Adapters;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using SwingingPaintBucket.Scene;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    public class ShapeVolumeEmitterPlayModeTests
    {
        [UnityTest]
        public IEnumerator Emit_SphereShape_SendsAllParticlesToEngine()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: 8192, autoRun: false);
            yield return null;

            const int requested = 1024;
            var go = new GameObject("ShapeVolumeEmitter");
            go.transform.position = new Vector3(0f, 1f, 0f);
            var emitter = go.AddComponent<ShapeVolumeEmitter>();
            emitter.SetPipeline(pipeline);
            emitter.Configure(ShapeVolumeType.Sphere, requested, 7777u, emitOnStartValue: false);
            emitter.SetSphere(0.6f);
            yield return null;

            int appended = emitter.Emit();

            Assert.AreEqual(requested, appended, "All sampled particles should be appended within capacity.");
            Assert.AreEqual((uint)requested, pipeline.GetActiveParticleCount());

            // Initial state should survive integration frames without error.
            for (int frame = 0; frame < 5; frame++)
            {
                pipeline.ExecutePipelineFrame(0.016f);
                yield return null;
            }

            Assert.Greater(pipeline.GetActiveParticleCount(), 0u);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(pipeline.gameObject);
        }

        [UnityTest]
        public IEnumerator Emit_BoxShape_RespectsCapacity()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported.");
                yield break;
            }

            yield return PlayModeTestUtility.EnsurePlayMode();

            PipelineExecutionController pipeline = TestPipelineFactory.CreatePipeline(capacity: 2048, autoRun: false);
            yield return null;

            var go = new GameObject("ShapeVolumeEmitterBox");
            var emitter = go.AddComponent<ShapeVolumeEmitter>();
            emitter.SetPipeline(pipeline);
            emitter.Configure(ShapeVolumeType.Box, count: 100000, randomSeed: 24u, emitOnStartValue: false);
            emitter.SetBox(new Vector3(1f, 1f, 1f));
            yield return null;

            int appended = emitter.Emit();

            Assert.LessOrEqual(appended, 2048, "Should never exceed pipeline capacity.");
            Assert.AreEqual((uint)appended, pipeline.GetActiveParticleCount());

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(pipeline.gameObject);
        }
    }
}
