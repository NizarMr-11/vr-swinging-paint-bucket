using System.Linq;
using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;

namespace HarmonicEngine.Tests
{
    public class RadixSortTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RadixSort_MatchesCpuReference_Random(int seed)
        {
            const int n = 4096;
            var rng = new System.Random(seed);
            var inputKeys = new uint[n];
            var inputValues = new uint[n];
            for (int i = 0; i < n; i++)
            {
                inputKeys[i] = (uint)rng.Next(0, 1 << 20);
                inputValues[i] = (uint)i;
            }

            var pairs = inputKeys.Zip(inputValues, (k, v) => (k, v)).OrderBy(p => p.k).ToArray();
            var expectedKeys = pairs.Select(p => p.k).ToArray();
            var expectedValues = pairs.Select(p => p.v).ToArray();

            var (gpuKeys, gpuValues) = RadixSortTestUtility.RunGpuRadixSortWithValues(inputKeys, inputValues, n);

            CollectionAssert.AreEqual(expectedKeys, gpuKeys);
            CollectionAssert.AreEqual(expectedValues, gpuValues);
        }

        [Test]
        public void RadixSort_OutputIsMonotonicallyNonDecreasing()
        {
            const int n = 16384;
            var rng = new System.Random(42);
            var keys = new uint[n];
            var values = new uint[n];
            for (int i = 0; i < n; i++)
            {
                keys[i] = (uint)rng.Next();
                values[i] = (uint)i;
            }

            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, n);

            for (int i = 1; i < n; i++)
            {
                Assert.IsTrue(sorted[i] >= sorted[i - 1],
                    $"Not monotonic at index {i}: {sorted[i - 1]} > {sorted[i]}");
            }
        }

        [Test]
        public void RadixSort_ValuesFollowKeys()
        {
            const int n = 4096;
            var keys = new uint[n];
            var values = new uint[n];
            for (int i = 0; i < n; i++)
            {
                keys[i] = (uint)(i * 7 % 100);
                values[i] = (uint)i;
            }

            var (sortedKeys, sortedValues) = RadixSortTestUtility.RunGpuRadixSortWithValues(keys, values, n);

            for (int i = 0; i < n; i++)
            {
                uint originalIndex = sortedValues[i];
                Assert.AreEqual(keys[originalIndex], sortedKeys[i],
                    $"Value {originalIndex} detached from key at position {i}");
            }
        }

        [Test]
        public void RadixSort_SingleElement_NoOp()
        {
            var keys = new uint[] { 42 };
            var values = new uint[] { 0 };
            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, 1);
            Assert.AreEqual(42u, sorted[0]);
        }

        [Test]
        public void RadixSort_AllSameKey_StableAndContiguous()
        {
            const int n = 1024;
            var keys = Enumerable.Repeat(123u, n).ToArray();
            var values = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();

            var (sortedKeys, sortedValues) = RadixSortTestUtility.RunGpuRadixSortWithValues(keys, values, n);

            Assert.IsTrue(sortedKeys.All(k => k == 123));
            CollectionAssert.AreEqual(values, sortedValues);
        }

        [Test]
        public void RadixSort_AlreadySorted_RemainsSorted()
        {
            const int n = 4096;
            var keys = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();
            var values = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();
            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, n);
            CollectionAssert.AreEqual(keys, sorted);
        }

        [Test]
        public void RadixSort_ReverseSorted_BecomesSorted()
        {
            const int n = 4096;
            var keys = Enumerable.Range(0, n).Reverse().Select(i => (uint)i).ToArray();
            var values = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();
            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, n);
            for (int i = 1; i < n; i++)
            {
                Assert.IsTrue(sorted[i] >= sorted[i - 1]);
            }
        }

        [Test]
        public void RadixSort_MaxUintKeys_NoOverflow()
        {
            const int n = 256;
            var keys = Enumerable.Repeat(uint.MaxValue, n / 2)
                .Concat(Enumerable.Repeat(0u, n / 2))
                .ToArray();
            var values = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();
            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, n);

            Assert.IsTrue(sorted.Take(n / 2).All(k => k == 0));
            Assert.IsTrue(sorted.Skip(n / 2).All(k => k == uint.MaxValue));
        }

        [Test]
        public void RadixSort_PreservesMultiset()
        {
            const int n = 8192;
            var rng = new System.Random(7);
            var keys = new uint[n];
            var values = Enumerable.Range(0, n).Select(i => (uint)i).ToArray();
            for (int i = 0; i < n; i++)
            {
                keys[i] = (uint)rng.Next(0, 1000);
            }

            uint[] sorted = RadixSortTestUtility.RunGpuRadixSort(keys, values, n);

            var inputCounts = keys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            var sortedCounts = sorted.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());

            Assert.AreEqual(inputCounts.Count, sortedCounts.Count);
            foreach (var kv in inputCounts)
            {
                Assert.AreEqual(kv.Value, sortedCounts[kv.Key]);
            }
        }

        [TestCase(4096)]
        [TestCase(32768)]
        [TestCase(65536)]
        public void RadixSort_DispatchCount_Is26_RegardlessOfN(int n)
        {
            int dispatches = RadixSortTestUtility.CountDispatches(n);
            Assert.AreEqual(GpuRadixSort.DispatchesPerFullSort, dispatches, $"N={n}");
        }
    }
}
