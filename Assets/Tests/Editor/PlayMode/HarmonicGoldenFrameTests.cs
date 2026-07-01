using System.IO;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    [Category("GPU")]
    public class HarmonicGoldenFrameTests
    {
        private const string BaselineFileName = "HarmonicGoldenFrameBaseline.json";

        /// <summary>
        /// Set to true locally to regenerate HarmonicGoldenFrameBaseline.json after an intentional physics change.
        /// </summary>
        private const bool RegenerateBaseline = false;

        [Test]
        public void GoldenFrame_MatchesBaseline_After100Frames()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
                return;
            }

            if (Application.isPlaying)
            {
                Assert.Ignore("Golden frame test runs in Edit Mode only (matches baseline menu).");
                return;
            }

            HarmonicPipelineController pipeline = null;
            try
            {
                pipeline = HarmonicGoldenFrameCapture.CreateLabEquivalentPipeline();
                HarmonicGoldenFrameCapture.RunSimulationFrames(pipeline);

                HarmonicGoldenFrameMetrics actual = HarmonicGoldenFrameCapture.Capture(pipeline);
                Assert.Greater(actual.activeCount, 0u, "Golden frame capture requires active particles.");

                string baselinePath = GetBaselinePath();
                if (RegenerateBaseline || !File.Exists(baselinePath))
                {
                    WriteBaseline(baselinePath, actual);
                    Assert.Pass($"Baseline written to {baselinePath}");
                    return;
                }

                HarmonicGoldenFrameMetrics expected = ReadBaseline(baselinePath);
                HarmonicGoldenFrameCapture.AssertWithinTolerance(actual, expected);
            }
            finally
            {
                if (pipeline != null)
                {
                    Object.DestroyImmediate(pipeline.gameObject);
                }
            }
        }

        private static string GetBaselinePath()
        {
            return Path.Combine(
                Application.dataPath,
                "Tests",
                "PlayMode",
                BaselineFileName);
        }

        private static void WriteBaseline(string path, HarmonicGoldenFrameMetrics metrics)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonUtility.ToJson(metrics, true));
        }

        private static HarmonicGoldenFrameMetrics ReadBaseline(string path)
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<HarmonicGoldenFrameMetrics>(json);
        }
    }
}
