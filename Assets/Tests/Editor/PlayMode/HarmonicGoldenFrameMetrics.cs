using System;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    [Serializable]
    public sealed class HarmonicGoldenFrameMetrics
    {
        public float avgC;
        public float avgSpeed;
        public float maxY;
        public uint activeCount;
        public int frameCount;
        public float deltaTime;
        public string capturedUtc;

        public static HarmonicGoldenFrameMetrics Create(
            float avgC,
            float avgSpeed,
            float maxY,
            uint activeCount,
            int frameCount,
            float deltaTime)
        {
            return new HarmonicGoldenFrameMetrics
            {
                avgC = avgC,
                avgSpeed = avgSpeed,
                maxY = maxY,
                activeCount = activeCount,
                frameCount = frameCount,
                deltaTime = deltaTime,
                capturedUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
