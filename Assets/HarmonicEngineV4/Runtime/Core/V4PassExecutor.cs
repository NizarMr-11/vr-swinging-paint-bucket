using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonicEngineV4.Logging;
using UnityEngine;

namespace HarmonicEngineV4.Core
{
    /// <summary>
    /// The single execution loop every GPU pass goes through (spec sections 8 and 11).
    /// Binds buffers declared in the manifest by name, dispatches, and wraps each dispatch
    /// in a V4Log.BeginPass scope - the one cross-cutting instrumentation seam.
    /// Buffers are (re)bound on every dispatch so ping-pong alias swaps are always honored.
    /// </summary>
    public sealed class V4PassExecutor
    {
        private sealed class ResolvedPass
        {
            public V4PassDef Def;
            public int KernelIndex;
            public uint GroupSizeX;
        }

        private readonly V4PassManifest _manifest;
        private readonly V4BufferRegistry _registry;
        private readonly Dictionary<string, ResolvedPass> _passes = new Dictionary<string, ResolvedPass>();
        private bool _initialized;

        public V4PassManifest Manifest => _manifest;

        public V4PassExecutor(V4PassManifest manifest, V4BufferRegistry registry)
        {
            _manifest = manifest != null ? manifest : throw new ArgumentNullException(nameof(manifest));
            _registry = registry != null ? registry : throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>Resolves kernel indices and validates the manifest. Buffers may be registered later, up to first dispatch.</summary>
        public void Initialize()
        {
            if (!V4ManifestValidation.IsUsable(_manifest, out List<V4ManifestValidation.Issue> issues))
            {
                V4ManifestValidation.LogIssues(issues);
                throw new InvalidOperationException($"Pass manifest '{_manifest.manifestName}' failed validation.");
            }

            V4ManifestValidation.LogIssues(issues);

            _passes.Clear();
            foreach (V4PassDef def in _manifest.passes)
            {
                int kernelIndex = def.shader.FindKernel(def.kernelName);
                def.shader.GetKernelThreadGroupSizes(kernelIndex, out uint x, out _, out _);
                _passes[def.passId] = new ResolvedPass
                {
                    Def = def,
                    KernelIndex = kernelIndex,
                    GroupSizeX = Math.Max(1u, x)
                };
            }

            _initialized = true;
            V4Log.Info(V4LogCategory.PassExecution, $"executor initialized manifest='{_manifest.manifestName}' passes={_passes.Count}");
        }

        public ComputeShader GetShader(string passId) => GetPass(passId).Def.shader;

        public int GetKernelIndex(string passId) => GetPass(passId).KernelIndex;

        /// <summary>Dispatches with explicit thread group counts.</summary>
        public void Dispatch(string passId, int groupsX, int groupsY = 1, int groupsZ = 1)
        {
            ResolvedPass pass = GetPass(passId);
            using (V4Log.PassScope scope = V4Log.BeginPass(passId))
            {
                long bytes = BindBuffers(pass);
                scope.RecordBufferBytes(bytes);
                DispatchWithOptionalGpuSync(pass, passId, groupsX, groupsY, groupsZ);
            }
        }

        /// <summary>Dispatches enough X groups to cover <paramref name="totalThreads"/> items.</summary>
        public void DispatchThreads(string passId, int totalThreads)
        {
            if (totalThreads <= 0)
            {
                return;
            }

            ResolvedPass pass = GetPass(passId);
            int groups = Mathf.CeilToInt(totalThreads / (float)pass.GroupSizeX);
            Dispatch(passId, groups);
        }

        private static void DispatchWithOptionalGpuSync(
            ResolvedPass pass,
            string passId,
            int groupsX,
            int groupsY,
            int groupsZ)
        {
            bool gpuSync = V4FramePerformanceCollector.Enabled && V4FramePerformanceCollector.GpuSyncEnabled;
            Stopwatch gpuSyncWatch = gpuSync ? Stopwatch.StartNew() : null;

            pass.Def.shader.Dispatch(pass.KernelIndex, groupsX, groupsY, groupsZ);

            if (gpuSync)
            {
                V4GpuSyncDispatch.WaitForGpu();
                gpuSyncWatch.Stop();
                V4FramePerformanceCollector.RecordGpuSyncScope(passId, gpuSyncWatch.Elapsed.TotalMilliseconds);
            }
        }

        private long BindBuffers(ResolvedPass pass)
        {
            long totalBytes = 0;
            foreach (string bufferName in pass.Def.AllBuffers())
            {
                if (!_registry.TryGet(bufferName, out ComputeBuffer buffer) || buffer == null)
                {
                    throw new InvalidOperationException(
                        $"Pass '{pass.Def.passId}' requires buffer '{bufferName}' which is not registered.");
                }

                pass.Def.shader.SetBuffer(pass.KernelIndex, bufferName, buffer);
                totalBytes += _registry.GetInfo(bufferName).Bytes;
            }

            return totalBytes;
        }

        private ResolvedPass GetPass(string passId)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("V4PassExecutor.Initialize() has not been called.");
            }

            if (!_passes.TryGetValue(passId, out ResolvedPass pass))
            {
                throw new KeyNotFoundException($"Pass '{passId}' not found in manifest '{_manifest.manifestName}'.");
            }

            return pass;
        }
    }
}
