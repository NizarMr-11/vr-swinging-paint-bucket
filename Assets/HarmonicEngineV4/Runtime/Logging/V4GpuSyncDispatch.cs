using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarmonicEngineV4.Logging
{
    /// <summary>
    /// Optional fence-sync wrapper for compute dispatches outside V4PassExecutor.
    /// </summary>
    public static class V4GpuSyncDispatch
    {
        public static void Dispatch(
            ComputeShader shader,
            int kernel,
            string scopeName,
            int groupsX,
            int groupsY = 1,
            int groupsZ = 1)
        {
            bool gpuSync = V4FramePerformanceCollector.Enabled && V4FramePerformanceCollector.GpuSyncEnabled;
            Stopwatch gpuSyncWatch = gpuSync ? Stopwatch.StartNew() : null;

            shader.Dispatch(kernel, groupsX, groupsY, groupsZ);

            if (gpuSync)
            {
                WaitForGpu();
                gpuSyncWatch.Stop();
                V4FramePerformanceCollector.RecordGpuSyncScope(scopeName, gpuSyncWatch.Elapsed.TotalMilliseconds);
            }
        }

        public static void WaitForGpu()
        {
            GraphicsFence fence = Graphics.CreateGraphicsFence(
                GraphicsFenceType.AsyncQueueSynchronisation,
                SynchronisationStageFlags.AllGPUOperations);
            Graphics.WaitOnAsyncGraphicsFence(fence);
        }
    }
}
