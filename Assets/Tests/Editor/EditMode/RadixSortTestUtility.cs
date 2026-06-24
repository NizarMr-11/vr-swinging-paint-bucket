using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    internal static class RadixSortTestUtility
    {
        private const string ShaderPath =
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/RadixSort.compute";

        public static ComputeShader LoadShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.IsNotNull(shader, $"Missing radix sort shader at {ShaderPath}");
            return shader;
        }

        public static uint[] RunGpuRadixSort(uint[] keys, uint[] values, int sortSize)
        {
            var (sortedKeys, _) = RunGpuRadixSortWithValues(keys, values, sortSize);
            return sortedKeys;
        }

        public static (uint[] sortedKeys, uint[] sortedValues) RunGpuRadixSortWithValues(
            uint[] keys,
            uint[] values,
            int sortSize)
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders not supported on this machine.");
            }

            ComputeShader shader = LoadShader();
            var keysBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var valuesBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var tempKeysBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var tempValuesBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);

            try
            {
                keysBuffer.SetData(keys, 0, 0, sortSize);
                valuesBuffer.SetData(values, 0, 0, sortSize);

                var sorter = new GpuRadixSort(shader, sortSize);
                try
                {
                    sorter.SortPairBuffers(
                        keysBuffer,
                        valuesBuffer,
                        tempKeysBuffer,
                        tempValuesBuffer,
                        sortSize);
                }
                finally
                {
                    sorter.Release();
                }

                var sortedKeys = new uint[sortSize];
                var sortedValues = new uint[sortSize];
                keysBuffer.GetData(sortedKeys, 0, 0, sortSize);
                valuesBuffer.GetData(sortedValues, 0, 0, sortSize);
                return (sortedKeys, sortedValues);
            }
            finally
            {
                keysBuffer.Release();
                valuesBuffer.Release();
                tempKeysBuffer.Release();
                tempValuesBuffer.Release();
            }
        }

        public static int CountDispatches(int sortSize)
        {
            ComputeShader shader = LoadShader();
            var keysBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var valuesBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var tempKeysBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var tempValuesBuffer = new ComputeBuffer(sortSize, sizeof(uint), ComputeBufferType.Structured);
            var gridBuffer = new ComputeBuffer(sortSize, sizeof(uint) * 2, ComputeBufferType.Structured);

            try
            {
                var sorter = new GpuRadixSort(shader, sortSize);
                try
                {
                    sorter.SortPairBuffers(
                        keysBuffer,
                        valuesBuffer,
                        tempKeysBuffer,
                        tempValuesBuffer,
                        sortSize,
                        gridBuffer);
                    return sorter.LastDispatchCount;
                }
                finally
                {
                    sorter.Release();
                }
            }
            finally
            {
                keysBuffer.Release();
                valuesBuffer.Release();
                tempKeysBuffer.Release();
                tempValuesBuffer.Release();
                gridBuffer.Release();
            }
        }
    }
}
