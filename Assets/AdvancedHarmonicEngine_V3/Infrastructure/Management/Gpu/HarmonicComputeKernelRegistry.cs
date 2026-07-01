using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Gpu
{
    /// <summary>Cached compute kernel indices for the harmonic pipeline shaders.</summary>
    public sealed class HarmonicComputeKernelRegistry
    {
        public int ArgSetup { get; private set; } = -1;
        public int GridClear { get; private set; } = -1;
        public int GridGenerate { get; private set; } = -1;
        public int GridBitonic { get; private set; } = -1;
        public int GridBuildRanges { get; private set; } = -1;
        public int Density { get; private set; } = -1;
        public int Integration { get; private set; } = -1;
        public int ContainerIntegration { get; private set; } = -1;
        public int Quantize { get; private set; } = -1;
        public int FallingWorld { get; private set; } = -1;
        public int DragClear { get; private set; } = -1;
        public int DragAdvect { get; private set; } = -1;
        public int DragScatter { get; private set; } = -1;
        public int DragApply { get; private set; } = -1;
        public int PbfPredict { get; private set; } = -1;
        public int PbfDensity { get; private set; } = -1;
        public int PbfLambda { get; private set; } = -1;
        public int PbfSolve { get; private set; } = -1;
        public int PbfApply { get; private set; } = -1;
        public int ContainerRigidCarry { get; private set; } = -1;

        public void CacheKernels(HarmonicComputeShaderSet shaders)
        {
            if (shaders.ArgumentUtility != null)
            {
                ArgSetup = shaders.ArgumentUtility.FindKernel("CalculateGridArgsKernel");
            }

            if (shaders.SpatialHashGrid != null)
            {
                GridClear = shaders.SpatialHashGrid.FindKernel("ClearGridCellsKernel");
                GridGenerate = shaders.SpatialHashGrid.FindKernel("GenerateGridKeysKernel");
                GridBitonic = shaders.SpatialHashGrid.FindKernel("BitonicSortStepKernel");
                GridBuildRanges = shaders.SpatialHashGrid.FindKernel("BuildCellRangesKernel");
            }

            if (shaders.StreamCompaction != null)
            {
                Density = shaders.StreamCompaction.FindKernel("ExecuteSphDensityPass");
            }

            if (shaders.StreamCompactionIntegrate != null)
            {
                Integration = shaders.StreamCompactionIntegrate.FindKernel("ExecuteInternalFluidIntegration");
                ContainerIntegration = shaders.StreamCompactionIntegrate.FindKernel("ExecuteContainerFluidIntegration");
            }

            if (shaders.DataCompaction != null)
            {
                Quantize = shaders.DataCompaction.FindKernel("QuantizeFallingParticlesKernel");
            }

            if (shaders.FallingFluidWorld != null)
            {
                FallingWorld = shaders.FallingFluidWorld.FindKernel("ExecuteFallingFluidIntegration");
            }

            if (shaders.EulerianDragGrid != null)
            {
                DragClear = shaders.EulerianDragGrid.FindKernel("ClearDragGridKernel");
                DragAdvect = shaders.EulerianDragGrid.FindKernel("AdvectDragGridKernel");
                DragScatter = shaders.EulerianDragGrid.FindKernel("ScatterParticleToGridKernel");
                DragApply = shaders.EulerianDragGrid.FindKernel("ApplyDragFromGridKernel");
            }

            if (shaders.PbfSolver != null)
            {
                PbfPredict = shaders.PbfSolver.FindKernel("PredictPositionsKernel");
                PbfDensity = shaders.PbfSolver.FindKernel("ComputeDensityKernel");
                PbfLambda = shaders.PbfSolver.FindKernel("ComputeLambdaKernel");
                PbfSolve = shaders.PbfSolver.FindKernel("SolvePositionsKernel");
                PbfApply = shaders.PbfSolver.FindKernel("ApplyPositionsKernel");
            }

            ContainerRigidCarry = shaders.ContainerRigidCarry != null
                ? shaders.ContainerRigidCarry.FindKernel("ContainerRigidCarryKernel")
                : -1;
        }
    }

    public sealed class HarmonicComputeShaderSet
    {
        public ComputeShader ArgumentUtility;
        public ComputeShader SpatialHashGrid;
        public ComputeShader StreamCompaction;
        public ComputeShader StreamCompactionIntegrate;
        public ComputeShader DataCompaction;
        public ComputeShader FallingFluidWorld;
        public ComputeShader EulerianDragGrid;
        public ComputeShader PbfSolver;
        public ComputeShader ContainerRigidCarry;
    }
}
