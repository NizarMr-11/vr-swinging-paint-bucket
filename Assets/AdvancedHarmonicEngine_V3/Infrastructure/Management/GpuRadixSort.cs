using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>6-bit LSD radix sort (Satish et al. 2009) with groupshared histograms.</summary>
    public sealed class GpuRadixSort
    {
        public const int RadixBits = 6;
        public const int BucketCount = 64;
        public const int PassCount = 6;
        public const int DispatchesPerPass = 4;
        public const int DispatchesPerFullSort = PassCount * DispatchesPerPass + 1;

        private static readonly int SortKeysId = Shader.PropertyToID("_SortKeys");
        private static readonly int SortValuesId = Shader.PropertyToID("_SortValues");
        private static readonly int TempKeysId = Shader.PropertyToID("_TempKeys");
        private static readonly int TempValuesId = Shader.PropertyToID("_TempValues");
        private static readonly int GlobalHistogramId = Shader.PropertyToID("_GlobalHistogram");
        private static readonly int GlobalPrefixId = Shader.PropertyToID("_GlobalPrefix");
        private static readonly int GroupHistogramId = Shader.PropertyToID("_GroupHistogram");
        private static readonly int GroupOffsetsId = Shader.PropertyToID("_GroupOffsets");
        private static readonly int GridKeyValueBufferId = Shader.PropertyToID("_GridKeyValueBuffer");
        private static readonly int BitShiftId = Shader.PropertyToID("_BitShift");
        private static readonly int SortSizeId = Shader.PropertyToID("_SortSize");
        private static readonly int NumGroupsId = Shader.PropertyToID("_NumGroups");

        private readonly ComputeShader _shader;
        private readonly int _kernelClear;
        private readonly int _kernelHistogram;
        private readonly int _kernelScan;
        private readonly int _kernelScatter;
        private readonly int _kernelPack;

        private ComputeBuffer _globalHistogram;
        private ComputeBuffer _globalPrefix;
        private ComputeBuffer _groupHistogram;
        private ComputeBuffer _groupOffsets;

        public int LastDispatchCount { get; private set; }

        public GpuRadixSort(ComputeShader shader, int maxSortSize)
        {
            _shader = shader;
            _kernelClear = shader.FindKernel("ClearHistogramKernel");
            _kernelHistogram = shader.FindKernel("HistogramKernel");
            _kernelScan = shader.FindKernel("ScanKernel");
            _kernelScatter = shader.FindKernel("ScatterKernel");
            _kernelPack = shader.FindKernel("PackGridKeysKernel");

            int maxGroups = Mathf.CeilToInt(maxSortSize / (float)BucketCount);
            int groupTableSize = maxGroups * BucketCount;
            _globalHistogram = new ComputeBuffer(BucketCount, sizeof(uint), ComputeBufferType.Structured);
            _globalPrefix = new ComputeBuffer(BucketCount, sizeof(uint), ComputeBufferType.Structured);
            _groupHistogram = new ComputeBuffer(groupTableSize, sizeof(uint), ComputeBufferType.Structured);
            _groupOffsets = new ComputeBuffer(groupTableSize, sizeof(uint), ComputeBufferType.Structured);
        }

        public void Release()
        {
            _globalHistogram?.Release();
            _globalPrefix?.Release();
            _groupHistogram?.Release();
            _groupOffsets?.Release();
            _globalHistogram = null;
            _globalPrefix = null;
            _groupHistogram = null;
            _groupOffsets = null;
        }

        public void SortPairBuffers(
            ComputeBuffer keys,
            ComputeBuffer values,
            ComputeBuffer tempKeys,
            ComputeBuffer tempValues,
            int sortSize,
            ComputeBuffer packGridKeyValueBuffer = null)
        {
            LastDispatchCount = 0;
            if (sortSize <= 0)
            {
                return;
            }

            int threadGroups = Mathf.CeilToInt(sortSize / (float)BucketCount);
            _shader.SetInt(SortSizeId, sortSize);
            _shader.SetInt(NumGroupsId, threadGroups);

            ComputeBuffer readKeys = keys;
            ComputeBuffer readValues = values;
            ComputeBuffer writeKeys = tempKeys;
            ComputeBuffer writeValues = tempValues;

            for (int pass = 0; pass < PassCount; pass++)
            {
                int bitShift = pass * RadixBits;
                _shader.SetInt(BitShiftId, bitShift);

                _shader.SetBuffer(_kernelClear, GlobalHistogramId, _globalHistogram);
                _shader.Dispatch(_kernelClear, 1, 1, 1);
                LastDispatchCount++;

                _shader.SetBuffer(_kernelHistogram, SortKeysId, readKeys);
                _shader.SetBuffer(_kernelHistogram, GlobalHistogramId, _globalHistogram);
                _shader.SetBuffer(_kernelHistogram, GroupHistogramId, _groupHistogram);
                _shader.Dispatch(_kernelHistogram, threadGroups, 1, 1);
                LastDispatchCount++;

                _shader.SetBuffer(_kernelScan, GlobalHistogramId, _globalHistogram);
                _shader.SetBuffer(_kernelScan, GlobalPrefixId, _globalPrefix);
                _shader.SetBuffer(_kernelScan, GroupHistogramId, _groupHistogram);
                _shader.SetBuffer(_kernelScan, GroupOffsetsId, _groupOffsets);
                _shader.Dispatch(_kernelScan, 1, 1, 1);
                LastDispatchCount++;

                _shader.SetBuffer(_kernelScatter, SortKeysId, readKeys);
                _shader.SetBuffer(_kernelScatter, SortValuesId, readValues);
                _shader.SetBuffer(_kernelScatter, TempKeysId, writeKeys);
                _shader.SetBuffer(_kernelScatter, TempValuesId, writeValues);
                _shader.SetBuffer(_kernelScatter, GroupOffsetsId, _groupOffsets);
                _shader.Dispatch(_kernelScatter, threadGroups, 1, 1);
                LastDispatchCount++;

                (readKeys, writeKeys) = (writeKeys, readKeys);
                (readValues, writeValues) = (writeValues, readValues);
            }

            if (packGridKeyValueBuffer != null)
            {
                _shader.SetBuffer(_kernelPack, GridKeyValueBufferId, packGridKeyValueBuffer);
                _shader.SetBuffer(_kernelPack, SortKeysId, readKeys);
                _shader.SetBuffer(_kernelPack, SortValuesId, readValues);
                _shader.Dispatch(_kernelPack, threadGroups, 1, 1);
                LastDispatchCount++;
            }
        }
    }
}
