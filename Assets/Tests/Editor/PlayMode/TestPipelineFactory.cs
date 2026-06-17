using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using UnityEngine;

namespace HarmonicEngine.Tests.PlayMode
{
    internal static class TestPipelineFactory
    {
        public static PipelineExecutionController CreatePipeline(int capacity = 8192, bool autoRun = false)
        {
            var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
            AssertSettings(settings);

            var go = new GameObject("TestHarmonicPipeline");
            var pipeline = go.AddComponent<PipelineExecutionController>();
            pipeline.ConfigureAndInitialize(
                settings.argumentUtilityShader,
                settings.spatialHashGridShader,
                settings.streamCompactionShader,
                settings.dataCompactionShader,
                capacity > 0 ? capacity : settings.testCapacity,
                externalIngestion: true,
                autoRun: autoRun,
                fallingShader: settings.fallingFluidWorldShader,
                eulerianShader: settings.eulerianDragGridShader);
            return pipeline;
        }

        private static void AssertSettings(HarmonicPipelineTestSettings settings)
        {
            if (settings == null
                || settings.argumentUtilityShader == null
                || settings.spatialHashGridShader == null
                || settings.streamCompactionShader == null
                || settings.dataCompactionShader == null)
            {
                throw new MissingReferenceException(
                    "Create Resources/HarmonicPipelineTestSettings via HarmonicEngine/Test Pipeline Settings menu or run editor bootstrap.");
            }
        }
    }
}
