using System;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Uniform-grid spatial hash (broad phase). Owns the grid + sort scratch buffers and
    /// runs clear -> generate keys -> radix sort -> build cell ranges. Consumer kernels
    /// bind CellStartEndBuffer + GridKeyValueBuffer and iterate via V4NeighborQuery.hlsl.
    /// </summary>
    public sealed class V4SpatialHashGrid : IDisposable
    {
        public const string CellStartEndName = "_CellStartEndBuffer";
        public const string GridKeyValueName = "_GridKeyValueBuffer";

        private static readonly int CellStartEndId = Shader.PropertyToID(CellStartEndName);
        private static readonly int GridKeyValueId = Shader.PropertyToID(GridKeyValueName);
        private static readonly int Block0Id = Shader.PropertyToID("_Block0");
        private static readonly int PaddedGridSizeId = Shader.PropertyToID("_PaddedGridSize");
        private static readonly int ActiveParticleCountId = Shader.PropertyToID("_ActiveParticleCount");
        private static readonly int GridResolutionId = Shader.PropertyToID("_GridResolution");
        private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");

        private readonly ComputeShader _hashShader;
        private readonly V4GpuRadixSort _radixSort;
        private readonly int _kernelClear;
        private readonly int _kernelGenerate;
        private readonly int _kernelRanges;

        private ComputeBuffer _cellStartEnd;
        private ComputeBuffer _gridKeyValue;
        private ComputeBuffer _sortKeys;
        private ComputeBuffer _sortValues;
        private ComputeBuffer _tempKeys;
        private ComputeBuffer _tempValues;

        public int GridResolution { get; }
        public int PaddedGridSize { get; }
        public ComputeBuffer CellStartEndBuffer => _cellStartEnd;
        public ComputeBuffer GridKeyValueBuffer => _gridKeyValue;

        public V4SpatialHashGrid(ComputeShader hashShader, ComputeShader radixShader, int capacity)
        {
            _hashShader = hashShader;
            _kernelClear = hashShader.FindKernel("ClearGridCellsKernel");
            _kernelGenerate = hashShader.FindKernel("GenerateGridKeysKernel");
            _kernelRanges = hashShader.FindKernel("BuildCellRangesKernel");

            GridResolution = Mathf.Clamp(Mathf.NextPowerOfTwo(capacity * 2), 1 << 12, 1 << 20);
            PaddedGridSize = Mathf.Max(GridResolution, Mathf.NextPowerOfTwo(capacity));

            _cellStartEnd = new ComputeBuffer(PaddedGridSize, sizeof(int) * 2, ComputeBufferType.Structured);
            _gridKeyValue = new ComputeBuffer(PaddedGridSize, sizeof(uint) * 2, ComputeBufferType.Structured);
            _sortKeys = new ComputeBuffer(PaddedGridSize, sizeof(uint), ComputeBufferType.Structured);
            _sortValues = new ComputeBuffer(PaddedGridSize, sizeof(uint), ComputeBufferType.Structured);
            _tempKeys = new ComputeBuffer(PaddedGridSize, sizeof(uint), ComputeBufferType.Structured);
            _tempValues = new ComputeBuffer(PaddedGridSize, sizeof(uint), ComputeBufferType.Structured);

            _radixSort = new V4GpuRadixSort(radixShader, PaddedGridSize);
        }

        /// <summary>Registers the grid output buffers so manifest passes can bind them by name.</summary>
        public void RegisterBuffers(V4BufferRegistry registry)
        {
            registry.Assign(CellStartEndName, _cellStartEnd, PaddedGridSize, sizeof(int) * 2, V4BufferLifetime.PerFrame);
            registry.Assign(GridKeyValueName, _gridKeyValue, PaddedGridSize, sizeof(uint) * 2, V4BufferLifetime.PerFrame);
        }

        /// <summary>Rebuilds the grid over the given position buffer (float4, xyz used).</summary>
        public void Build(ComputeBuffer positionsBlock0, int activeCount, float cellSize)
        {
            using (V4Log.BeginPass("SpatialHashBuild"))
            {
                _hashShader.SetInt(PaddedGridSizeId, PaddedGridSize);
                _hashShader.SetInt(ActiveParticleCountId, Mathf.Max(0, activeCount));
                _hashShader.SetInt(GridResolutionId, GridResolution);
                _hashShader.SetFloat(CellSizeId, cellSize);

                _hashShader.SetBuffer(_kernelClear, CellStartEndId, _cellStartEnd);
                _hashShader.Dispatch(_kernelClear, Mathf.CeilToInt(PaddedGridSize / 256f), 1, 1);

                _hashShader.SetBuffer(_kernelGenerate, GridKeyValueId, _gridKeyValue);
                _hashShader.SetBuffer(_kernelGenerate, Block0Id, positionsBlock0);
                _hashShader.Dispatch(_kernelGenerate, Mathf.CeilToInt(PaddedGridSize / 64f), 1, 1);

                _radixSort.SortPairBuffers(_sortKeys, _sortValues, _tempKeys, _tempValues, PaddedGridSize, _gridKeyValue);

                _hashShader.SetBuffer(_kernelRanges, GridKeyValueId, _gridKeyValue);
                _hashShader.SetBuffer(_kernelRanges, CellStartEndId, _cellStartEnd);
                _hashShader.Dispatch(_kernelRanges, Mathf.CeilToInt(PaddedGridSize / 64f), 1, 1);
            }
        }

        public void Dispose()
        {
            _radixSort.Release();
            _cellStartEnd?.Release();
            _gridKeyValue?.Release();
            _sortKeys?.Release();
            _sortValues?.Release();
            _tempKeys?.Release();
            _tempValues?.Release();
            _cellStartEnd = null;
            _gridKeyValue = null;
            _sortKeys = null;
            _sortValues = null;
            _tempKeys = null;
            _tempValues = null;
        }
    }
}
