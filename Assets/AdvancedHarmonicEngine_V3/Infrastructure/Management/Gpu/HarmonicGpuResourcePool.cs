using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management.Gpu
{
    public sealed class HarmonicGpuResourcePool
    {
        public ParticleSoaBuffers SoaInternalA { get; private set; }
        public ParticleSoaBuffers SoaInternalB { get; private set; }
        public ParticleSoaBuffers SoaFalling { get; private set; }
        public ParticleSoaBuffers SoaFallingWorld { get; private set; }
        public ParticleSoaBuffers SoaDragScratch { get; private set; }
        public ComputeBuffer DensityCacheDensities { get; private set; }
        public ComputeBuffer DensityCachePressures { get; private set; }
        public ComputeBuffer BufferFalling { get; private set; }
        public ComputeBuffer BufferFallingWorld { get; private set; }
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
            SoaFalling = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Append);
            SoaFallingWorld = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Append);
            SoaDragScratch = ParticleSoaBuffers.Create(config.MaxCapacity, ComputeBufferType.Structured);

            DensityCacheDensities = new ComputeBuffer(config.MaxCapacity, sizeof(float), ComputeBufferType.Structured);
            DensityCachePressures = new ComputeBuffer(config.MaxCapacity, sizeof(float), ComputeBufferType.Structured);
            PbfScratch = PbfScratchBuffers.Create(config.MaxCapacity);

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
            SoaFalling.SetCounterValue(0);
            SoaFallingWorld.SetCounterValue(0);
            CanvasHits.SetCounterValue(0);

            BufferFalling = SoaFalling.CounterBuffer;
            BufferFallingWorld = SoaFallingWorld.CounterBuffer;

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
            SoaFalling?.Release();
            SoaFallingWorld?.Release();
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

            SoaInternalA = null;
            SoaInternalB = null;
            SoaFalling = null;
            SoaFallingWorld = null;
            SoaDragScratch = null;
            DensityCacheDensities = null;
            DensityCachePressures = null;
            BufferFalling = null;
            BufferFallingWorld = null;
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
            PingPong = null;
            BufferService = null;
        }

        public void SetFrameSortSize(int sortSize) => FrameSortSize = sortSize;

        public void SwapFallingParticleBuffers()
        {
            (SoaFalling, SoaFallingWorld) = (SoaFallingWorld, SoaFalling);
            BufferFalling = SoaFalling.CounterBuffer;
            BufferFallingWorld = SoaFallingWorld.CounterBuffer;
        }
    }

    public sealed class HarmonicGpuResourcePoolConfig
    {
        public int MaxCapacity;
        public int DragGridVolume;
        public int MaxCanvasHitsPerFrame;
        public ComputeShader RadixSortShader;
    }
}
