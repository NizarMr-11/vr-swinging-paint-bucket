#if UNITY_EDITOR
using HarmonicEngine.Testing;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests.EditorSupport
{
    public static class HarmonicTestAssetBootstrap
    {
        [MenuItem("HarmonicEngine/Testing/Create Test Pipeline Settings Asset")]
        public static void CreateTestSettingsAsset()
        {
            const string resourcePath = "Assets/Tests/Resources/HarmonicPipelineTestSettings.asset";

            if (!AssetDatabase.IsValidFolder("Assets/Tests"))
            {
                AssetDatabase.CreateFolder("Assets", "Tests");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Tests/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/Tests", "Resources");
            }

            var existing = AssetDatabase.LoadAssetAtPath<HarmonicPipelineTestSettings>(resourcePath);
            if (existing != null)
            {
                return;
            }

            var settings = ScriptableObject.CreateInstance<HarmonicPipelineTestSettings>();
            settings.argumentUtilityShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ArgumentUtility.compute");
            settings.spatialHashGridShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/SpatialHashGridIndirect.compute");
            settings.streamCompactionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/StreamCompactionPingPong.compute");
            settings.dataCompactionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/DataCompactionPacker.compute");
            settings.fallingFluidWorldShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/FallingFluidWorld.compute");
            settings.eulerianDragGridShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/EulerianDragGrid.compute");

            AssetDatabase.CreateAsset(settings, resourcePath);
            AssetDatabase.SaveAssets();
        }
    }

    [InitializeOnLoad]
    internal static class HarmonicTestAssetAutoBootstrap
    {
        static HarmonicTestAssetAutoBootstrap()
        {
            EditorApplication.delayCall += HarmonicTestAssetBootstrap.CreateTestSettingsAsset;
        }
    }
}
#endif
