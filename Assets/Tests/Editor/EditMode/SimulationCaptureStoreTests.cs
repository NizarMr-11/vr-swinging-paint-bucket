using System.IO;
using HarmonicEngine.Infrastructure.PlaybackStreaming;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace HarmonicEngine.Tests.EditMode
{
    public class SimulationCaptureStoreTests
    {
        [Test]
        public void AddFrame_TracksCountTimeAndMax()
        {
            var store = new SimulationCaptureStore(30f);
            store.AddFrame(new[] { new float3(1f, 2f, 3f), new float3(4f, 5f, 6f) }, 2, 0f);
            store.AddFrame(new[] { new float3(7f, 8f, 9f) }, 1, 0.0333f);

            Assert.AreEqual(2, store.FrameCount);
            Assert.AreEqual(2, store.GetFrameParticleCount(0));
            Assert.AreEqual(1, store.GetFrameParticleCount(1));
            Assert.AreEqual(2, store.MaxParticleCount);
        }

        [Test]
        public void FrameIndexForTime_ClampsToRange()
        {
            var store = new SimulationCaptureStore(10f);
            for (int i = 0; i < 5; i++)
            {
                store.AddFrame(new[] { float3.zero }, 1, i / 10f);
            }

            Assert.AreEqual(0, store.FrameIndexForTime(-1f));
            Assert.AreEqual(2, store.FrameIndexForTime(0.2f));
            Assert.AreEqual(4, store.FrameIndexForTime(99f));
        }

        [Test]
        public void SaveThenLoad_RoundTripsFramesExactly()
        {
            var store = new SimulationCaptureStore(24f);
            store.AddFrame(new[] { new float3(1f, 2f, 3f), new float3(-4f, 5.5f, 6f) }, 2, 0f);
            store.AddFrame(new[] { new float3(10f, 11f, 12f) }, 1, 0.0417f);

            string path = Path.Combine(Application.temporaryCachePath, "capture_roundtrip_test.harmonicbake");
            store.SaveToFile(path);
            SimulationCaptureStore loaded = SimulationCaptureStore.LoadFromFile(path);
            File.Delete(path);

            Assert.AreEqual(store.FrameCount, loaded.FrameCount);
            Assert.AreEqual(24f, loaded.CaptureFps, 1e-4f);
            Assert.AreEqual(2, loaded.GetFrameParticleCount(0));
            float3 first = loaded.GetFramePositions(0)[0];
            Assert.AreEqual(1f, first.x, 1e-4f);
            Assert.AreEqual(2f, first.y, 1e-4f);
            Assert.AreEqual(3f, first.z, 1e-4f);
            float3 second = loaded.GetFramePositions(1)[0];
            Assert.AreEqual(10f, second.x, 1e-4f);
        }
    }
}
