using System;
using System.Linq;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.PlayMode
{
    /// <summary>
    /// Quarantine gate for the ported radix sort (plan Phase 1): output must match
    /// CPU Array.Sort for random, sorted, reverse-sorted, and all-equal key sets,
    /// including non-power-of-two sizes. Nothing builds on the sort until this is green.
    /// </summary>
    public sealed class V4RadixSortTests
    {
        private static void RunSortAndAssert(uint[] keys, string label)
        {
            int n = keys.Length;
            uint[] values = new uint[n];
            for (uint i = 0; i < n; i++)
            {
                values[i] = i;
            }

            ComputeShader shader = V4ShaderLibrary.Load(V4ShaderLibrary.RadixSort);
            var sort = new V4GpuRadixSort(shader, n);
            var keysBuffer = new ComputeBuffer(n, sizeof(uint));
            var valuesBuffer = new ComputeBuffer(n, sizeof(uint));
            var tempKeys = new ComputeBuffer(n, sizeof(uint));
            var tempValues = new ComputeBuffer(n, sizeof(uint));
            var pairBuffer = new ComputeBuffer(n, sizeof(uint) * 2);

            try
            {
                var pairs = new uint[n * 2];
                for (int i = 0; i < n; i++)
                {
                    pairs[i * 2] = keys[i];
                    pairs[i * 2 + 1] = values[i];
                }

                pairBuffer.SetData(pairs);
                sort.SortPairBuffers(keysBuffer, valuesBuffer, tempKeys, tempValues, n, pairBuffer);

                var sortedPairs = new uint[n * 2];
                pairBuffer.GetData(sortedPairs);

                // CPU reference: stable sort by key. LSD radix sort is stable, so both
                // keys and values must match the stable reference exactly.
                var reference = Enumerable.Range(0, n)
                    .Select(i => (key: keys[i], value: values[i]))
                    .OrderBy(p => p.key)
                    .ToArray();

                for (int i = 0; i < n; i++)
                {
                    Assert.AreEqual(reference[i].key, sortedPairs[i * 2],
                        $"{label}: key mismatch at index {i}");
                    Assert.AreEqual(reference[i].value, sortedPairs[i * 2 + 1],
                        $"{label}: value mismatch at index {i} (stability violation)");
                }
            }
            finally
            {
                sort.Release();
                keysBuffer.Release();
                valuesBuffer.Release();
                tempKeys.Release();
                tempValues.Release();
                pairBuffer.Release();
            }
        }

        [Test]
        public void RandomKeys_MatchCpuSort([Values(64, 1000, 4096, 10000)] int size)
        {
            var rng = new System.Random(1234 + size);
            uint[] keys = new uint[size];
            for (int i = 0; i < size; i++)
            {
                keys[i] = (uint)rng.Next(int.MinValue, int.MaxValue);
            }

            RunSortAndAssert(keys, $"random n={size}");
        }

        [Test]
        public void AlreadySortedKeys_MatchCpuSort()
        {
            uint[] keys = Enumerable.Range(0, 2048).Select(i => (uint)(i * 3)).ToArray();
            RunSortAndAssert(keys, "already-sorted");
        }

        [Test]
        public void ReverseSortedKeys_MatchCpuSort()
        {
            uint[] keys = Enumerable.Range(0, 2048).Select(i => (uint)((2048 - i) * 7)).ToArray();
            RunSortAndAssert(keys, "reverse-sorted");
        }

        [Test]
        public void AllEqualKeys_MatchCpuSort_AndPreserveOrder()
        {
            uint[] keys = Enumerable.Repeat(42u, 3000).ToArray();
            RunSortAndAssert(keys, "all-equal");
        }

        [Test]
        public void NonPowerOfTwoSize_MatchCpuSort()
        {
            var rng = new System.Random(999);
            uint[] keys = new uint[777];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = (uint)rng.Next();
            }

            RunSortAndAssert(keys, "non-pow2 n=777");
        }

        [Test]
        public void SentinelMaxKeys_SortToEnd()
        {
            // The spatial hash pads unused slots with 0xFFFFFFFF; those must land at the tail.
            var rng = new System.Random(7);
            uint[] keys = new uint[512];
            for (int i = 0; i < 256; i++)
            {
                keys[i] = (uint)rng.Next(0, 1 << 20);
            }

            for (int i = 256; i < 512; i++)
            {
                keys[i] = 0xFFFFFFFFu;
            }

            RunSortAndAssert(keys, "sentinel-padded");
        }
    }
}
