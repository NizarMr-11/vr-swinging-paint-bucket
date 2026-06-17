using HarmonicEngine.Core.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class ArchitectureManifestCompletenessTests
    {
        private const string ComputeShaderRoot = "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/";

        [Test]
        public void RequiredComputeKernels_AllExistInProjectShaders()
        {
            foreach (string kernel in ArchitectureManifest.RequiredComputeKernels)
            {
                Assert.IsTrue(KernelExistsInAnyProjectShader(kernel), $"Missing kernel: {kernel}");
            }
        }

        [Test]
        public void RequiredBlueprintAssets_AllExistInProject()
        {
            foreach (string assetName in ArchitectureManifest.RequiredBlueprintAssets)
            {
                string[] guids = AssetDatabase.FindAssets($"{System.IO.Path.GetFileNameWithoutExtension(assetName)}");
                bool found = false;
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(assetName))
                    {
                        continue;
                    }

                    if (path.Contains("AdvancedHarmonicEngine_V3") || path.Contains("SwingingPaintBucket"))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, $"Missing blueprint asset: {assetName}");
            }
        }

        private static bool KernelExistsInAnyProjectShader(string kernelName)
        {
            string[] guids = AssetDatabase.FindAssets("t:ComputeShader", new[] { ComputeShaderRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                if (shader != null && HasKernel(shader, kernelName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasKernel(ComputeShader shader, string kernelName)
        {
            return ComputeShaderTestUtility.HasKernel(shader, kernelName);
        }
    }
}
