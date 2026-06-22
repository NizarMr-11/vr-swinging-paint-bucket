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

        private const int MaxSubstepsBetweenHashRebuilds = 64;

        /// <summary>
        /// Rebuilds the spatial hash for currently appended particles without SPH integration.
        /// Used by GPU verification tests to validate hash/sort invariants at frame 0.
        /// </summary>
        public void RebuildSpatialHashForVerification()
        {
            if (!AreShadersReady() || _pingPong == null)
            {
                return;
            }

            uint activeCount = SanitizeAndRepairCount(_pingPong.ReadBuffer);
            _cachedInternalCount = activeCount;
            if (activeCount == 0)
            {
                return;
            }

            ComputeFrameSortSize(activeCount);
            BuildSpatialHashGrid(_pingPong.ReadBuffer, activeCount);
        }

        /// <summary>
        /// One-frame stencil probe: counts neighbors for <paramref name="particleIndex"/> using the
        /// same 27-cell traversal as the GPU density pass. Logs once to the console.
        /// </summary>
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

            if (!TryGetInternalParticleBuffer(out ComputeBuffer particleBuffer, out uint activeCount)
                || !TryGetSpatialHashBuffers(out ComputeBuffer gridKeys, out ComputeBuffer cellRanges, out int sortSize)
                || activeCount == 0
                || sortSize <= 0)
            {
                return false;
            }

            var particles = GpuParticleReadbackUtility.ReadParticles(
                particleBuffer,
                (int)activeCount);
            var keys = new GridKeyPair[sortSize];
            var ranges = new HashCellGridRange[sortSize];
            gridKeys.GetData(keys);
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

        private void BuildSpatialHashGrid(ComputeBuffer positions, uint activeCount)
        {
            ComputeBuffer.CopyCount(positions, _indirectArgsBuffer, sizeof(int) * 3);
            DispatchIndirectArgsSetup();

            using (MarkerGrid.Auto())
            {
                spatialHashGridShader.SetBuffer(_kernelGridClear, CellStartEndBufferId, _cellStartEndBuffer);
                spatialHashGridShader.SetInt(PaddedGridSizeId, _paddedSortSize);
                spatialHashGridShader.SetInt(GridResolutionId, _frameSortSize);
                int clearGroups = Mathf.CeilToInt(_paddedSortSize / 256f);
                spatialHashGridShader.Dispatch(_kernelGridClear, clearGroups, 1, 1);

                spatialHashGridShader.SetBuffer(_kernelGridGenerate, GridKeyValueBufferId, _gridKeyValueBuffer);
                spatialHashGridShader.SetBuffer(_kernelGridGenerate, ReadOnlyParticlePositionsId, positions);
                spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
                spatialHashGridShader.SetInt(ActiveParticleCountId, (int)activeCount);
                spatialHashGridShader.SetInt(GridResolutionId, _frameSortSize);
                spatialHashGridShader.SetFloat(CellSizeId, cellSize);
                int generateGroups = Mathf.CeilToInt(_frameSortSize / 64f);
                spatialHashGridShader.Dispatch(_kernelGridGenerate, generateGroups, 1, 1);
            }

            using (MarkerSort.Auto())
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

            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, GridKeyValueBufferId, _gridKeyValueBuffer);
            spatialHashGridShader.SetBuffer(_kernelGridBuildRanges, CellStartEndBufferId, _cellStartEndBuffer);
            spatialHashGridShader.SetInt(PaddedGridSizeId, _frameSortSize);
            using (MarkerBuildRanges.Auto())
            {
                spatialHashGridShader.Dispatch(
                    _kernelGridBuildRanges,
                    Mathf.CeilToInt(_frameSortSize / 64f),
                    1,
                    1);
            }
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
