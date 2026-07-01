using System;
using System.IO;
using HarmonicEngine.Infrastructure.Management;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    public static class HarmonicGoldenFrameBaselineMenu
    {
        private const string BaselineRelativePath = "Tests/PlayMode/HarmonicGoldenFrameBaseline.json";

        [MenuItem("HarmonicEngine/Tests/Generate Golden Frame Baseline")]
        public static void GenerateBaseline()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode before generating the golden frame baseline.");
                return;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("Compute shaders not supported on this machine.");
                return;
            }

            HarmonicPipelineController pipeline = null;
            try
            {
                pipeline = HarmonicGoldenFrameCapture.CreateLabEquivalentPipeline();
                HarmonicGoldenFrameCapture.RunSimulationFrames(pipeline);

                HarmonicGoldenFrameMetrics metrics = HarmonicGoldenFrameCapture.Capture(pipeline);
                string path = Path.Combine(Application.dataPath, BaselineRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, JsonUtility.ToJson(metrics, true));
                AssetDatabase.Refresh();
                Debug.Log(
                    $"Golden frame baseline written to Assets/{BaselineRelativePath}: " +
                    $"avgC={metrics.avgC:F6} avgSpeed={metrics.avgSpeed:F6} maxY={metrics.maxY:F6} activeCount={metrics.activeCount}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Golden frame baseline generation failed: {ex}");
            }
            finally
            {
                if (pipeline != null)
                {
                    UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
                }
            }
        }
    }
}
