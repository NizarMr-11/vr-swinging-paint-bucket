using HarmonicEngine.Core.DataStructures;
using HarmonicEngine.Infrastructure.Management.Gpu;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.SimulationPasses
{
    internal sealed class SpatialHashBuildPass
    {
        private static readonly ProfilerMarker MarkerGrid = new("Harmonic.SpatialHashGrid");
        private static readonly ProfilerMarker MarkerSort = new("Harmonic.BitonicSort");
        private static readonly ProfilerMarker MarkerRadixSort = new("Harmonic.RadixSort");
        private static readonly ProfilerMarker MarkerBuildRanges = new("Harmonic.BuildRanges");

        public void Build(HarmonicPipelineController host, ParticleSoaBuffers read, uint activeCount)
        {
            Build(host, read.Block0, activeCount);
        }

        public void Build(HarmonicPipelineController host, ComputeBuffer positionBlock0, uint activeCount)
        {
            ComputeBuffer.CopyCount(host.PingPong.ReadSet.CounterBuffer, host.IndirectArgsBuffer, sizeof(int) * 3);
            host.PassDispatchIndirectArgsSetup();

            bool radixActive = host.UseRadixSortActive;
            int generateGroups = Mathf.CeilToInt(host.FrameSortSize / 64f);
            int clearGroups = Mathf.CeilToInt(host.FrameSortSize / 256f);

            using (MarkerGrid.Auto())
            {
                host.SpatialHashGridShader.SetBuffer(host.KernelGridClear, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
                host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.PaddedGridSize, host.FrameSortSize);
                host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.GridResolution, host.FrameSortSize);
                host.SpatialHashGridShader.Dispatch(host.KernelGridClear, clearGroups, 1, 1);

                host.SpatialHashGridShader.SetBuffer(host.KernelGridGenerate, HarmonicShaderPropertyIds.Block0, positionBlock0);
                host.SpatialHashGridShader.SetBuffer(host.KernelGridGenerate, HarmonicShaderPropertyIds.GridKeyValueBuffer, host.GridKeyValueBuffer);
                host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.PaddedGridSize, host.FrameSortSize);
                host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.ActiveParticleCount, (int)activeCount);
                host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.GridResolution, host.FrameSortSize);
                host.SpatialHashGridShader.SetFloat(HarmonicShaderPropertyIds.CellSize, host.CellSize);
                host.SpatialHashGridShader.Dispatch(host.KernelGridGenerate, generateGroups, 1, 1);
            }

            if (radixActive)
            {
                using (MarkerRadixSort.Auto())
                {
                    host.GpuRadixSort.SortPairBuffers(
                        host.SortKeysBuffer,
                        host.SortValuesBuffer,
                        host.SortTempKeysBuffer,
                        host.SortTempValuesBuffer,
                        host.FrameSortSize,
                        host.GridKeyValueBuffer);
                }
            }
            else
            {
                using (MarkerSort.Auto())
                {
                    RunBitonicSort(host);
                }
            }

            host.MaybeLogSortDiagnostic(activeCount);

            host.SpatialHashGridShader.SetBuffer(host.KernelGridBuildRanges, HarmonicShaderPropertyIds.CellStartEndBuffer, host.CellStartEndBuffer);
            host.SpatialHashGridShader.SetBuffer(host.KernelGridBuildRanges, HarmonicShaderPropertyIds.GridKeyValueBuffer, host.GridKeyValueBuffer);
            host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.PaddedGridSize, host.FrameSortSize);
            using (MarkerBuildRanges.Auto())
            {
                host.SpatialHashGridShader.Dispatch(host.KernelGridBuildRanges, generateGroups, 1, 1);
            }
        }

        private static void RunBitonicSort(HarmonicPipelineController host)
        {
            int bitonicGroups = Mathf.CeilToInt(host.FrameSortSize / 256f);
            for (int level = 2; level <= host.FrameSortSize; level <<= 1)
            {
                for (int levelMask = level >> 1; levelMask > 0; levelMask >>= 1)
                {
                    host.SpatialHashGridShader.SetBuffer(host.KernelGridBitonic, HarmonicShaderPropertyIds.GridKeyValueBuffer, host.GridKeyValueBuffer);
                    host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.PaddedGridSize, host.FrameSortSize);
                    host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.BitonicLevel, level);
                    host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.BitonicLevelMask, levelMask);
                    host.SpatialHashGridShader.SetInt(HarmonicShaderPropertyIds.BitonicWidth, host.FrameSortSize);
                    host.SpatialHashGridShader.Dispatch(host.KernelGridBitonic, bitonicGroups, 1, 1);
                }
            }
        }
    }
}
