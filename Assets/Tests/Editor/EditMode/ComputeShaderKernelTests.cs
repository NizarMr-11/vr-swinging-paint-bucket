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
        public void StreamCompaction_HasDensityPass()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.StreamCompaction);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteSphDensityPass");
        }

        [Test]
        public void StreamCompactionIntegrate_HasIntegrationPasses()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestComputeShaderPaths.StreamCompactionIntegrate);
            Assert.IsNotNull(shader);
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteInternalFluidIntegration");
            ComputeShaderTestUtility.AssertHasKernel(shader, "ExecuteContainerFluidIntegration");
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
