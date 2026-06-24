using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using Unity.Profiling;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    public partial class PipelineExecutionController
    {
        private bool _stencilNeighborCountLogged;
        private float _hashLogAccumulator;
        private int _hashRebuildsThisSecond;
        private int _hashSubstepsThisSecond;
        private int _hashFramesThisSecond;
        private float _sortLogAccumulator;
        private int _sortOpsThisSecond;
        private long _sortDispatchSumThisSecond;
        private uint _sortActiveCountLast;
        private bool _radixSortAnnounced;
        private bool _bitonicSortAnnounced;

        private const int MaxSubstepsBetweenHashRebuilds = 64;

        public void RebuildSpatialHashForVerification()
        {
            if (!AreShadersReady() || _pingPong == null)
            {
                return;
            }

            uint activeCount = SanitizeAndRepairCount(_pingPong.ReadSet);
            _cachedInternalCount = activeCount;
            if (activeCount == 0)
            {
                return;
            }

            ComputeFrameSortSize(activeCount);
            BuildSpatialHashGrid(_pingPong.ReadSet, activeCount);
        }

        public bool TryLogStencilNeighborCountOnce(int particleIndex = 0)
        {
            if (!TryCountStencilNeighbors(particleIndex, out int stencilCount, out int bruteForceCount))
            {
                return false;
            }

            string message =
                $"[SPH stencil probe] particle={particleIndex} "
                + $"stencilNeighbors={stencilCount} bruteForce2h={bruteForceCount} "
                + $"(h={SmoothingRadius:F4}m cell={CellSize:F4}m)";
            LogSphTelemetry(message);
            return true;
        }

        public bool TryCountStencilNeighbors(
            int particleIndex,
            out int stencilCount,
            out int bruteForceCount)
        {
            stencilCount = 0;
            bruteForceCount = 0;

            if (!TryGetInternalParticleSoa(out ParticleSoaBuffers particleSoa, out uint activeCount)
                || !TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize)
                || activeCount == 0
                || sortSize <= 0)
            {
                return false;
            }

            var particles = GpuParticleReadbackUtility.ReadParticles(particleSoa, (int)activeCount);
            var keys = ReadSortedGridPairs(sortSize);
            var ranges = new HashCellGridRange[sortSize];
            cellRanges.GetData(ranges);

            float smoothingRadius = SmoothingRadius;
            stencilCount = SphNeighborQueryCpuMirror.CountStencilNeighbors(
                particleIndex,
                particles,
                activeCount,
                keys,
                ranges,
                CellSize,
                smoothingRadius,
                sortSize);
            bruteForceCount = SphNeighborQueryCpuMirror.CountBruteForceNeighbors(
                particleIndex,
                particles,
                activeCount,
                smoothingRadius);
            return true;
        }

        private void BuildSpatialHashGrid(ParticleSoaBuffers read, uint activeCount)
        {
            BuildSpatialHashGrid(read.Block0, activeCount);
        }

        private void BuildSpatialHashGrid(ComputeBuffer positionBlock0, uint activeCount)
        {
            ComputeBuffer.CopyCount(_pingPong.ReadSet.CounterBuffer, _indirectArgsBuffer, sizeof(int) * 3);
            DispatchIndirectArgsSetup();

            bool radixActive = UseRadixSortActive;
            int generateGroups = Mathf.CeilToInt(_frameSortSize / 64f);
            int clearGroups = Mathf.CeilToInt(_frameSortSize / 256f);

            using (MarkerGrid.Auto())
            {
                spatialHashGridShader.SetBuffer(_kernelGridClear, CellStartEndBufferId, _cellStartEndBuffer);
                spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
                spatialHashGridShader.SetInt(GridResolutionId, _frameSortSize);
                spatialHashGridShader.Dispatch(_kernelGridClear, clearGroups, 1, 1);

                spatialHashGridShader.SetBuffer(_kernelGridGenerate, Block0Id, positionBlock0);
                spatialHashGridShader.SetBuffer(_kernelGridGenerate, GridKeyValueBufferId, _gridKeyValueBuffer);
                spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
                spatialHashGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
                spatialHashGridShader.SetInt(GridResolutionId, _frameSortSize);
                spatialHashGridShader.SetFloat(CellSizeId, cellSize);
                spatialHashGridShader.Dispatch(_kernelGridGenerate, generateGroups, 1, 1);
            }

            if (radixActive)
            {
                using (MarkerRadixSort.Auto())
                {
                    RunRadixSort();
                }
            }
            else
            {
                using (MarkerSort.Auto())
                {
                    RunBitonicSort();
                }
            }

            MaybeLogSortDiagnostic(activeCount);

            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, CellStartEndBufferId, _cellStartEndBuffer);
            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, GridKeyValueBufferId, _gridKeyValueBuffer);
            spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
            using (MarkerBuildRanges.Auto())
            {
                spatialHashGridShader.Dispatch(_kernelGridBuildRanges, generateGroups, 1, 1);
            }
        }

        private void RunRadixSort()
        {
            _gpuRadixSort.SortPairBuffers(
                _sortKeysBuffer,
                _sortValuesBuffer,
                _sortTempKeysBuffer,
                _sortTempValuesBuffer,
                _frameSortSize,
                _gridKeyValueBuffer);
        }

        private void RunBitonicSort()
        {
            int bitonicGroups = Mathf.CeilToInt(_frameSortSize / 256f);
            for (int level = 2; level <= _frameSortSize; level <<= 1)
            {
                for (int levelMask = level >> 1; levelMask > 0; levelMask >>= 1)
                {
                    spatialHashGridShader.SetBuffer(_kernelGridBitonic, GridKeyValueBufferId, _gridKeyValueBuffer);
                    spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
                    spatialHashGridShader.SetInt(BitonicLevelId, level);
                    spatialHashGridShader.SetInt(BitonicLevelMaskId, levelMask);
                    spatialHashGridShader.SetInt(BitonicWidthId, _frameSortSize);
                    spatialHashGridShader.Dispatch(_kernelGridBitonic, bitonicGroups, 1, 1);
                }
            }
        }

        private void MaybeLogSortDiagnostic(uint activeCount)
        {
            bool usingRadix = useRadixSort && _gpuRadixSort != null;
            string channel = usePBF ? "PBF" : "SPH";
            if (usingRadix && !_radixSortAnnounced)
            {
                _radixSortAnnounced = true;
                LogSortTelemetry(
                    $"[{channel} RADIX] enabled passes={GpuRadixSort.PassCount} radixBits={GpuRadixSort.RadixBits} "
                    + $"dispatchesPerSort={GpuRadixSort.DispatchesPerFullSort}");
            }
            else if (!usingRadix && !_bitonicSortAnnounced)
            {
                _bitonicSortAnnounced = true;
                LogSortTelemetry(
                    $"[{channel} BITONIC] fallback sort active (useRadixSort=false or shader missing)");
            }

            int dispatches = usingRadix
                ? _gpuRadixSort.LastDispatchCount
                : CountBitonicDispatches(_frameSortSize);
            _sortOpsThisSecond++;
            _sortDispatchSumThisSecond += dispatches;
            _sortActiveCountLast = activeCount;

            if (usePBF)
            {
                return;
            }

            _sortLogAccumulator += Time.unscaledDeltaTime;
            if (_sortLogAccumulator < 1f)
            {
                return;
            }

            FlushSortTelemetry(channel);
            _sortLogAccumulator = 0f;
        }

        private void FlushSortTelemetry(string channel)
        {
            if (_sortOpsThisSecond <= 0)
            {
                return;
            }

            bool usingRadix = useRadixSort && _gpuRadixSort != null;
            int threadGroups = Mathf.CeilToInt(_frameSortSize / (float)GpuRadixSort.BucketCount);
            float avgDispatches = _sortDispatchSumThisSecond / (float)_sortOpsThisSecond;
            string tag = usingRadix ? "RADIX" : "BITONIC";
            LogSortTelemetry(
                $"[{channel} {tag}] sortsInWindow={_sortOpsThisSecond} sortSize={_frameSortSize} padded={_paddedSortSize} "
                + $"active={_sortActiveCountLast} dispatches/sort={avgDispatches:F0} threadGroups={threadGroups}");

            _sortOpsThisSecond = 0;
            _sortDispatchSumThisSecond = 0;
        }

        private void LogSortTelemetry(string message, bool warning = false)
        {
            if (usePBF)
            {
                LogPbfTelemetry(message, warning);
            }
            else
            {
                LogSphTelemetry(message, warning);
            }
        }

        private static int CountBitonicDispatches(int sortSize)
        {
            if (sortSize < 2)
            {
                return 0;
            }

            int count = 0;
            for (int level = 2; level <= sortSize; level <<= 1)
            {
                for (int levelMask = level >> 1; levelMask > 0; levelMask >>= 1)
                {
                    count++;
                }
            }

            return count;
        }

        private GridKeyPair[] ReadSortedGridPairs(int sortSize)
        {
            var pairs = new GridKeyPair[sortSize];
            _gridKeyValueBuffer.GetData(pairs);
            return pairs;
        }

        private void MaybeLogStencilNeighborCount()
        {
            if (!logStencilNeighborCount || _stencilNeighborCountLogged)
            {
                return;
            }

            if (TryLogStencilNeighborCountOnce(0))
            {
                _stencilNeighborCountLogged = true;
            }
        }

        private static int ComputeMaxSubstepsBetweenHashRebuilds(float vMax, float subDt, float gridCellSize)
        {
            float safeSubDt = Mathf.Max(subDt, 1e-6f);
            float safeVMax = Mathf.Max(vMax, 1e-4f);
            int maxSubsteps = Mathf.FloorToInt(0.5f * gridCellSize / (safeVMax * safeSubDt));
            return Mathf.Clamp(maxSubsteps, 1, MaxSubstepsBetweenHashRebuilds);
        }

        private void MaybeLogHashRebuildDiagnostic(int rebuildsThisFrame, int substepsThisFrame, float frameDeltaTime)
        {
            _hashRebuildsThisSecond += rebuildsThisFrame;
            _hashSubstepsThisSecond += substepsThisFrame;
            _hashFramesThisSecond++;
            _hashLogAccumulator += frameDeltaTime;
            if (_hashLogAccumulator < 1f)
            {
                return;
            }

            float avgRebuilds = _hashRebuildsThisSecond / (float)Mathf.Max(1, _hashFramesThisSecond);
            float avgSaved = (_hashSubstepsThisSecond - _hashRebuildsThisSecond) / (float)Mathf.Max(1, _hashFramesThisSecond);
            LogSphTelemetry($"[SPH HASH] rebuilds/frame: {avgRebuilds:F1} (saved {avgSaved:F1})");

            _hashLogAccumulator = 0f;
            _hashRebuildsThisSecond = 0;
            _hashSubstepsThisSecond = 0;
            _hashFramesThisSecond = 0;
        }
    }
}
