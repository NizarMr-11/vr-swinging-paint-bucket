using HarmonicEngine.Infrastructure.PlaybackStreaming;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class ImpastoPresenterEditModeTests
    {
        [Test]
        public void StampImpastoAtUv_IncreasesHeightAtCenter()
        {
            var go = new GameObject("ImpastoTest");
            var presenter = go.AddComponent<HighScaleFramePresenter>();

            presenter.StampImpastoAtUv(new Vector2(0.5f, 0.5f), radius: 10f, intensity: 0.5f);

            var heightField = typeof(HighScaleFramePresenter)
                .GetField("_heightMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var heightMap = heightField?.GetValue(presenter) as Texture2D;
            Assert.IsNotNull(heightMap);

            int cx = heightMap.width / 2;
            int cy = heightMap.height / 2;
            Color center = heightMap.GetPixel(cx, cy);
            Color corner = heightMap.GetPixel(0, 0);
            Assert.Greater(center.r, corner.r);

            Object.DestroyImmediate(go);
        }
    }
}
