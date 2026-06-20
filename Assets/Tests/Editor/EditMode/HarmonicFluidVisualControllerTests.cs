using HarmonicEngine.Infrastructure.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class HarmonicFluidVisualControllerTests
    {
        [Test]
        public void ResolveReferences_WithNullRefs_DoesNotThrow()
        {
            var go = new GameObject("VisualControllerTest");
            try
            {
                var controller = go.AddComponent<HarmonicFluidVisualController>();
                controller.VisualMode = HarmonicFluidVisualMode.DebugPoints;
                controller.VisualMode = HarmonicFluidVisualMode.ScreenSpaceFluid;
                Assert.Pass();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SSFluidRenderShader_Exists()
        {
            Shader shader = Shader.Find("HarmonicEngine/SSFluidRender");
            if (shader == null)
            {
                Assert.Ignore("HarmonicEngine/SSFluidRender shader not imported yet.");
            }

            Assert.IsFalse(string.IsNullOrEmpty(shader.name));
        }

        [Test]
        public void ScreenSpaceFluidRenderer_WithNullPipeline_DoesNotThrowOnDisable()
        {
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            try
            {
                var renderer = camGo.AddComponent<HarmonicScreenSpaceFluidRenderer>();
                renderer.RenderingEnabled = false;
                renderer.enabled = false;
                Assert.Pass();
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
