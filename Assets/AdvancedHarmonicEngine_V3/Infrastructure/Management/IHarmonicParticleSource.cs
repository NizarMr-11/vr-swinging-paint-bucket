using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Read-facing contract for the GPU particle pipeline. Consumers (debug renderers,
    /// overlays, bake recorders, tests) should depend on this instead of the concrete
    /// <see cref="PipelineExecutionController"/> so they only see the safe read surface.
    ///
    /// All buffer accessors return the post-frame read buffer plus a cached active count
    /// from the last completed <c>ExecutePipelineFrame</c>; read them after the pipeline's
    /// Update (e.g. in LateUpdate / OnRenderObject).
    /// </summary>
    public interface IHarmonicParticleSource
    {
        /// <summary>Maximum particles the GPU buffers were allocated for.</summary>
        int MaxCapacity { get; }

        /// <summary>Spatial-hash cell size (world units).</summary>
        float CellSize { get; }

        /// <summary>SPH smoothing radius (h) derived from the solver and cell size.</summary>
        float SmoothingRadius { get; }

        /// <summary>Power-of-two sort/grid size used this frame (dynamic when enabled).</summary>
        int FrameSortSize { get; }

        /// <summary>True when the world-space cylinder container path is active.</summary>
        bool ContainerFluidEnabled { get; }

        /// <summary>True when the world-falling-only path is active (no bucket SPH).</summary>
        bool WorldFallingOnly { get; }

        /// <summary>Live active particle count (CPU-visible counter).</summary>
        uint GetActiveParticleCount();

        /// <summary>Internal/container SPH particle buffer + count for this frame.</summary>
        bool TryGetInternalParticleBuffer(out ComputeBuffer buffer, out uint count);

        /// <summary>World-space falling particle buffer + count for this frame.</summary>
        bool TryGetFallingParticleBuffer(out ComputeBuffer buffer, out uint count);
    }

    /// <summary>
    /// Push-side frame summary raised by <see cref="PipelineExecutionController.FrameCompleted"/>
    /// at the end of every simulated frame, so listeners can react without polling.
    /// </summary>
    public readonly struct HarmonicFrameInfo
    {
        public readonly uint ActiveParticleCount;
        public readonly uint CanvasHitCount;
        public readonly int FrameSortSize;
        public readonly bool ContainerFluidEnabled;
        public readonly bool WorldFallingOnly;

        public HarmonicFrameInfo(
            uint activeParticleCount,
            uint canvasHitCount,
            int frameSortSize,
            bool containerFluidEnabled,
            bool worldFallingOnly)
        {
            ActiveParticleCount = activeParticleCount;
            CanvasHitCount = canvasHitCount;
            FrameSortSize = frameSortSize;
            ContainerFluidEnabled = containerFluidEnabled;
            WorldFallingOnly = worldFallingOnly;
        }
    }
}
