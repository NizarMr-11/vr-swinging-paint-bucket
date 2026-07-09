using System.Collections;
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Simulation;
using HarmonicEngineV4.UI.Lab2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngineV4.Tests.PlayMode
{
    public sealed class V4Lab2RuntimeUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator DeferredInitialize_RunScene_DoesNotProduceNaN()
        {
            var bucketGo = new GameObject("Bucket");
            var bucket = bucketGo.AddComponent<V4Bucket>();
            bucket.innerRadius = 0.3f;
            bucket.height = 0.6f;
            bucket.wallThickness = 0.05f;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.position = new Vector3(0f, -1f, 0f);
            var canvas = canvasGo.AddComponent<V4Canvas>();
            canvas.width = 3f;
            canvas.depth = 3f;

            var zoneGo = new GameObject("SpawnZone");
            zoneGo.transform.position = bucketGo.transform.TransformPoint(new Vector3(0f, 0.25f, 0f));
            var zone = zoneGo.AddComponent<V4SpawnZone>();
            zone.radius = 0.12f;
            zone.color = Color.red;

            var profile = ScriptableObject.CreateInstance<V4LiquidProfile>();
            var rootGo = new GameObject("Pipeline");
            var root = rootGo.AddComponent<V4PipelineRoot>();
            root.deferAutoInitialize = true;
            root.autoRun = false;
            root.bucket = bucket;
            root.canvas = canvas;
            root.globalProfile = profile;
            root.spawnZones.Add(zone);

            var uiGo = new GameObject("Lab2 UI Test");
            var setup = uiGo.AddComponent<V4Lab2RuntimeSetupController>();
            SerializedFieldInjector.Inject(setup, "pipeline", root);
            SerializedFieldInjector.Inject(setup, "skipStartupPrompt", true);

            yield return null;

            Assert.IsTrue(root.Initialized, "skipStartupPrompt should initialize on Start");
            Assert.IsTrue(root.autoRun);
            Assert.Greater(root.ActiveParticleCount, 0);

            for (int i = 0; i < 10; i++)
            {
                root.Step(1f / 60f);
            }

            var positions = new Vector4[root.ActiveParticleCount];
            root.Soa.ReadBlock0.GetData(positions, 0, 0, positions.Length);
            foreach (Vector4 p in positions)
            {
                Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z), "NaN after deferred startup run");
            }

            Object.Destroy(uiGo);
            Object.Destroy(rootGo);
            Object.Destroy(bucketGo);
            Object.Destroy(canvasGo);
            Object.Destroy(zoneGo);
            Object.Destroy(profile);
        }
    }

    internal static class SerializedFieldInjector
    {
        public static void Inject(Object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }
}
