using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Gpu
{
    public sealed class HarmonicGpuResourcePool
    {
        public ParticleSoaBuffers SoaInternalA { get; private set; }
        public ParticleSoaBuffers SoaInternalB { get; private set; }
        public ParticleSoaBuffers SoaDragScratch { get; private set; }
        public ComputeBuffer DensityCacheDensities { get; private set; }
        public ComputeBuffer DensityCachePressures { get; private set; }
        public ComputeBuffer DragGrid { get; private set; }
        public ComputeBuffer GridKeyValueBuffer { get; private set; }
        public ComputeBuffer CellStartEndBuffer { get; private set; }
        public ComputeBuffer SortKeysBuffer { get; private set; }
        public ComputeBuffer SortTempKeysBuffer { get; private set; }
        public ComputeBuffer SortValuesBuffer { get; private set; }
        public ComputeBuffer SortTempValuesBuffer { get; private set; }
        public GpuRadixSort GpuRadixSort { get; private set; }
        public ComputeBuffer IndirectArgsBuffer { get; private set; }
        public ComputeBuffer QuantizedBakeBuffer { get; private set; }
        public ComputeBuffer CounterReadbackBuffer { get; private set; }
        public ComputeBuffer CanvasHits { get; private set; }
        public PbfScratchBuffers PbfScratch { get; private set; }
        /// <summary>Per-particle last rigid-carry rotational velocity contribution (world space). Not ping-ponged.</summary>
        public ComputeBuffer PrevCarryContribution { get; private set; }
        /// <summary>Per-particle "was inside-for-carry last frame" flag (0/1). Gates rigid-carry continuity. Not ping-ponged.</summary>
        public ComputeBuffer PrevInsideForCarry { get; private set; }
        public PingPongSoaManager PingPong { get; private set; }
        public HarmonicParticleBufferService BufferService { get; private set; }
        public int PaddedSortSize { get; private set; }
        public int FrameSortSize { get; private set; }

        public void Initialize(HarmonicGpuResourcePoolConfig config)
        {
            Release();

            int keyStride = sizeof(uint) * 2;
            int cellStride = sizeof(int) * 2;
            int quantizedStride = sizeof(ushort) * 8;

            SoaInternalA = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Append);
            SoaInternalB = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Append);
            SoaDragScratch = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Structured);

            DensityCacheDensities = new ComputeBuffer(config.MaxCapacity, sizeof(float), ComputeBufferType.Structured);
            DensityCachePressures = new ComputeBuffer(config.MaxCapacity, sizeof(float), ComputeBufferType.Structured);
            PbfScratch = PbfScratchBuffers.Create(config.MaxCapacity);

            int carryContributionStride = sizeof(float) * 3;
            PrevCarryContribution = new ComputeBuffer(config.MaxCapacity, carryContributionStride, ComputeBufferType.Structured);
            PrevInsideForCarry = new ComputeBuffer(config.MaxCapacity, sizeof(uint), ComputeBufferType.Structured);
            ClearPrevCarryContribution(config.MaxCapacity);

            int dragVolume = Mathf.Max(1, config.DragGridVolume);
            int dragStride = sizeof(float) * 4;
            DragGrid = new ComputeBuffer(dragVolume, dragStride, ComputeBufferType.Structured);

            int canvasHitStride = sizeof(float) * 8;
            int canvasHitCapacity = Mathf.Max(1, config.MaxCanvasHitsPerFrame);
            CanvasHits = new ComputeBuffer(canvasHitCapacity, canvasHitStride, ComputeBufferType.Append);

            PaddedSortSize = HarmonicEngineLimits.SortGridSizeForCapacity(config.MaxCapacity);
            FrameSortSize = PaddedSortSize;
            GridKeyValueBuffer = new ComputeBuffer(PaddedSortSize, keyStride, ComputeBufferType.Structured);
            CellStartEndBuffer = new ComputeBuffer(PaddedSortSize, cellStride, ComputeBufferType.Structured);
            SortKeysBuffer = new ComputeBuffer(PaddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            SortTempKeysBuffer = new ComputeBuffer(PaddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            SortValuesBuffer = new ComputeBuffer(PaddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            SortTempValuesBuffer = new ComputeBuffer(PaddedSortSize, sizeof(uint), ComputeBufferType.Structured);
            GpuRadixSort = config.RadixSortShader != null
                ? new GpuRadixSort(config.RadixSortShader, PaddedSortSize)
                : null;

            IndirectArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            QuantizedBakeBuffer = new ComputeBuffer(config.MaxCapacity, quantizedStride, ComputeBufferType.Structured);
            CounterReadbackBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            SoaInternalA.SetCounterValue(0);
            SoaInternalB.SetCounterValue(0);
            CanvasHits.SetCounterValue(0);

            PingPong = new PingPongSoaManager(SoaInternalA, SoaInternalB);
            BufferService = new HarmonicParticleBufferService(
                SoaInternalA,
                SoaInternalB,
                PingPong,
                config.MaxCapacity,
                CounterReadbackBuffer);
        }

        public void Release()
        {
            SoaInternalA?.Release();
            SoaInternalB?.Release();
            SoaDragScratch?.Release();
            DensityCacheDensities?.Release();
            DensityCachePressures?.Release();
            DragGrid?.Release();
            GridKeyValueBuffer?.Release();
            CellStartEndBuffer?.Release();
            SortKeysBuffer?.Release();
            SortTempKeysBuffer?.Release();
            SortValuesBuffer?.Release();
            SortTempValuesBuffer?.Release();
            GpuRadixSort?.Release();
            IndirectArgsBuffer?.Release();
            QuantizedBakeBuffer?.Release();
            CounterReadbackBuffer?.Release();
            CanvasHits?.Release();
            PbfScratch?.Release();
            PrevCarryContribution?.Release();
            PrevInsideForCarry?.Release();

            SoaInternalA = null;
            SoaInternalB = null;
            SoaDragScratch = null;
            DensityCacheDensities = null;
            DensityCachePressures = null;
            DragGrid = null;
            GridKeyValueBuffer = null;
            CellStartEndBuffer = null;
            SortKeysBuffer = null;
            SortTempKeysBuffer = null;
            SortValuesBuffer = null;
            SortTempValuesBuffer = null;
            GpuRadixSort = null;
            IndirectArgsBuffer = null;
            QuantizedBakeBuffer = null;
            CounterReadbackBuffer = null;
            CanvasHits = null;
            PbfScratch = null;
            PrevCarryContribution = null;
            PrevInsideForCarry = null;
            PingPong = null;
            BufferService = null;
        }

        public void ClearPrevCarryContribution(int capacity)
        {
            if (PrevCarryContribution == null || capacity <= 0)
            {
                return;
            }

            int count = Mathf.Min(capacity, PrevCarryContribution.count);
            var zeros = new Vector3[count];
            PrevCarryContribution.SetData(zeros);

            if (PrevInsideForCarry != null)
            {
                int flagCount = Mathf.Min(capacity, PrevInsideForCarry.count);
                var zeroFlags = new uint[flagCount];
                PrevInsideForCarry.SetData(zeroFlags);
            }
        }

        /// <summary>
        /// Marks a contiguous range of particles as carry-earned (entrained) from frame 0.
        /// Used for initial in-container lattice fill: those particles are genuine fluid, not
        /// swept debris, so they must skip the velocity-agreement earn gate to avoid a one-time
        /// settling pop when the container is already moving at spawn.
        /// </summary>
        public void SeedCarryEarned(int startIndex, int count)
        {
            if (PrevInsideForCarry == null || count <= 0 || startIndex < 0)
            {
                return;
            }

            int end = Mathf.Min(startIndex + count, PrevInsideForCarry.count);
            int seedCount = end - startIndex;
            if (seedCount <= 0)
            {
                return;
            }

            var ones = new uint[seedCount];
            for (int i = 0; i < seedCount; i++)
            {
                ones[i] = 1u;
            }

            PrevInsideForCarry.SetData(ones, 0, startIndex, seedCount);
        }

        public void SetFrameSortSize(int sortSize) => FrameSortSize = sortSize;
    }

    public sealed class HarmonicGpuResourcePoolConfig
    {
        public int MaxCapacity;
        public int DragGridVolume;
        public int MaxCanvasHitsPerFrame;
        public ComputeShader RadixSortShader;
    }
}
