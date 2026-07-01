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
                if (existing.wcsphDensityShader == null || existing.wcsphIntegrationShader == null)
                {
                    existing.wcsphDensityShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                        "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/WcsphDensity.compute");
                    existing.wcsphIntegrationShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                        "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/WcsphIntegration.compute");
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                }

                return;
            }

            var settings = ScriptableObject.CreateInstance<HarmonicPipelineTestSettings>();
            settings.argumentUtilityShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ArgumentUtility.compute");
            settings.spatialHashGridShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/SpatialHashGridIndirect.compute");
            settings.radixSortShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/RadixSort.compute");
            settings.wcsphDensityShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/WcsphDensity.compute");
            settings.wcsphIntegrationShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/WcsphIntegration.compute");
            settings.pbfSolverShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/PbfSolver.compute");
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
