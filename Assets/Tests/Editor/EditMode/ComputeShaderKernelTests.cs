using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class ComputeShaderKernelTests
    {
        [Test]
        public void ArgumentUtility_HasCalculateGridArgsKernel()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.ArgumentUtility);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "CalculateGridArgsKernel");
        }

        [Test]
        public void SpatialHashGrid_HasAllStage2To4Kernels()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.SpatialHashGrid);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ClearGridCellsKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "GenerateGridKeysKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "BitonicSortStepKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "BuildCellRangesKernel");
        }

        [Test]
        public void WcsphDensity_HasDensityPass()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.WcsphDensity);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteSphDensityPass");
        }

        [Test]
        public void WcsphIntegration_HasIntegrationPasses()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.WcsphIntegration);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteInternalFluidIntegration");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteContainerFluidIntegration");
        }

        [Test]
        public void PbfSolver_HasAllKernels()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/PbfSolver.compute");
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "PredictPositionsKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ComputeDensityKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ComputeLambdaKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "SolvePositionsKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ApplyPositionsKernel");
        }

        [Test]
        public void DataCompaction_HasQuantizeKernel()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.DataCompaction);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "QuantizeFallingParticlesKernel");
        }

        [Test]
        public void FallingFluidWorld_HasIntegrationKernel()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.FallingFluidWorld);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteFallingFluidIntegration");
        }

        [Test]
        public void EulerianDragGrid_HasClearAndApplyKernels()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.EulerianDragGrid);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ClearDragGridKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "AdvectDragGridKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ScatterParticleToGridKernel");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ApplyDragFromGridKernel");
        }
    }
}
