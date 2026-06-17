using HarmonicEngine.Infrastructure.PlaybackStreaming;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using SwingingPaintBucket.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    public class SimulationManagerResetPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResetSimulation_ClearsImpastoHeightMap()
        {
            yield return PlayModeTestUtility.EnsurePlayMode();

            var presenterGo = new GameObject("TestImpastoPresenter");
            var presenter = presenterGo.AddComponent<HighScaleFramePresenter>();
            presenter.StampImpastoAtUv(new Vector2(0.5f, 0.5f), radius: 12f, intensity: 0.8f);

            var managerGo = new GameObject("TestSimulationManager");
            var manager = managerGo.AddComponent<SimulationManager>();
            manager.ImpastoPresenter = presenter;

            manager.ResetSimulation();
            yield return null;

            var heightField = typeof(HighScaleFramePresenter)
                .GetField("_heightMap", BindingFlags.NonPublic | BindingFlags.Instance);
            var heightMap = heightField?.GetValue(presenter) as Texture2D;
            Assert.IsNotNull(heightMap);

            Color center = heightMap.GetPixel(heightMap.width / 2, heightMap.height / 2);
            Assert.Less(center.r, 0.01f);

            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(presenterGo);
        }
    }
}
