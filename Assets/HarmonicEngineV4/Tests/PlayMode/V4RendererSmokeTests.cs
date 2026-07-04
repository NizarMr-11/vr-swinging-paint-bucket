using System.Collections;
using HarmonicEngineV4.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Renderer smoke tests (plan Phase 6): visual code cannot corrupt simulation state,
    /// so it only needs to initialize, draw frames without exceptions, and tear down.
    /// </summary>
    public sealed class V4RendererSmokeTests
    {
        [UnityTest]
        public IEnumerator DebugPointRenderer_DrawsFrames_WithoutErrors()
        {
            using var rig = V4TestRig.Create();
            var cameraGo = new GameObject("V4TestCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0.5f, -2f);

            var rendererGo = new GameObject("V4TestDebugRenderer");
            var renderer = rendererGo.AddComponent<V4DebugPointRenderer>();
            renderer.pipeline = rig.Root;

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    rig.Step(1);
                    yield return null;
                }

                Assert.IsTrue(renderer.enabled, "debug renderer disabled itself (shader missing?)");
            }
            finally
            {
                Object.DestroyImmediate(rendererGo);
                Object.DestroyImmediate(cameraGo);
            }
        }

        [UnityTest]
        public IEnumerator ScreenSpaceFluidRenderer_DrawsFrames_WithoutErrors()
        {
            using var rig = V4TestRig.Create();
            var cameraGo = new GameObject("V4TestSsfrCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0.5f, -2f);
            var renderer = cameraGo.AddComponent<V4ScreenSpaceFluidRenderer>();
            renderer.SetPipeline(rig.Root);

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    rig.Step(1);
                    yield return null;
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
            }
        }

        [UnityTest]
        public IEnumerator CanvasPaintRenderer_CreatesQuad_WithCanvasTexture()
        {
            using var rig = V4TestRig.Create();
            var rendererGo = new GameObject("V4TestCanvasRenderer");
            var renderer = rendererGo.AddComponent<V4CanvasPaintRenderer>();
            renderer.pipeline = rig.Root;

            try
            {
                yield return null;
                yield return null;

                Transform quad = rendererGo.transform.Find("V4CanvasPaintQuad");
                Assert.IsNotNull(quad, "canvas paint quad was not created");
                var meshRenderer = quad.GetComponent<MeshRenderer>();
                Assert.AreEqual(rig.Root.CanvasTexture, meshRenderer.sharedMaterial.mainTexture);
            }
            finally
            {
                Object.DestroyImmediate(rendererGo);
            }
        }
    }
}
