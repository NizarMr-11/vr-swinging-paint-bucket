using System;
using System.Collections.Generic;
using HarmonicEngineV4.Bake;
using HarmonicEngineV4.Core;
using HarmonicEngineV4.Logging;
using HarmonicEngineV4.Profiles;
using UnityEngine;

namespace HarmonicEngineV4.Simulation
{
    /// <summary>
    /// Pipeline orchestrator (spec section 1): owns spawn zones, global density, the
    /// bucket/canvas references, every compute buffer (via the registry), drives the
    /// bake phase once at Start and the per-frame GPU pass sequence afterwards.
    /// </summary>
    public sealed class V4PipelineRoot : MonoBehaviour
    {
        [Header("Scene objects")]
        public V4Bucket bucket;
        public V4Canvas canvas;

        [Tooltip("Spawn zones. Empty = auto-discover in children.")]
        public List<V4SpawnZone> spawnZones = new List<V4SpawnZone>();

        [Header("Spawn")]
        [Tooltip("Global particle number density (particles per cubic meter).")]
        [Min(1000f)] public float globalDensity = 200000f;

        [Tooltip("Fallback liquid profile for zones that do not specify one.")]
        public V4LiquidProfile globalProfile;

        [Tooltip("Drop spawn points outside the bucket cavity (used by lab scenes). Tests that need free-fall spawns can disable this.")]
        public bool restrictSpawnToBucketCavity = true;

        [Header("Simulation")]
        public bool autoRun = true;
        [Range(1, 4)] public int pbfIterations = 2;
        [Min(0f)] public float carryRate = 10f;
        [Min(0f)] public float downwardScale = 1f;
        [Range(0f, 1f)] public float emaSmoothing = 0.1f;
        public float pbfEpsilon = 50f;
        [Range(0f, 1f)]
        [Tooltip("Scales mirror boundary density and gradient in PBF (floor/wall ghosts).")]
        public float boundaryGhostWeight = 0f;
        [Min(0.001f)] public float maxDeltaTime = 1f / 50f;
        public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        [Min(16)] public int maxSplatEvents = 1024;

        [Header("Debug instrumentation")]
        [Tooltip("Log per-tracked-particle boundary-pressure probes and Y-bin aggregates each frame.")]
        public bool debugLogBoundaryPressure = false;
        [Min(1)] public int debugBoundaryPressureBinCount = 5;
        [Tooltip("Log particles where ComputeContainInside disagrees with geometric footprint (max 8/frame).")]
        public bool debugLogContainInsideMismatch = false;
        [Tooltip("Log false escape latches, rim vs wall exits, beyond-outer-shell removals, and frame aggregates.")]
        public bool debugLogWallEscapeForensics = false;
        [Tooltip("Emit per-frame CPU/GPU performance summary to performance.log.")]
        public bool debugLogPerformance = false;
        [Tooltip("When performance logging is on: fence-sync after each GPU dispatch for per-pass GPU ms (stalls pipeline).")]
        public bool debugLogPerformanceGpuSync = false;

        private const int DebugBoundaryPressureFloorBinCount = 4;
        private const int MaxContainInsideMismatchLogsPerFrame = 8;
        private const int MaxWallEscapeDetailLogsPerFrame = 16;

        private struct ForensicsCompactionPair
        {
            public uint SortKey;
            public uint SourceIndex;
        }

        // --- Runtime state ---
        private V4BufferRegistry _registry;
        private V4PassExecutor _executor;
        private V4ParticleSoa _soa;
        private V4SpatialHashGrid _grid;
        private V4GpuRadixSort _compactionSort;
        private V4EmaLossModel _emaModel;

        private ComputeShader _classificationShader;
        private ComputeShader _forcesShader;
        private ComputeShader _pbfShader;
        private ComputeShader _canvasShader;

        private V4GpuBucketDriver _gpuBucketDriver;
        private int _classifyKernel;
        private int _externalForcesKernel;
        private int _predictKernel;
        private int _densityKernel;
        private int _lambdaKernel;
        private int _solveDeltaKernel;
        private int _applyDeltaKernel;
        private int _finalizeKernel;

        private ComputeBuffer _holesBuffer;
        private ComputeBuffer _layersBuffer;
        private ComputeBuffer _profilesBuffer;
        private ComputeBuffer _countersBuffer;
        private ComputeBuffer _predictedBuffer;
        private ComputeBuffer _densitiesBuffer;
        private ComputeBuffer _lambdasBuffer;
        private ComputeBuffer _deltasBuffer;
        private ComputeBuffer _blendedColorsBuffer;
        private ComputeBuffer _compactionPairsBuffer;
        private ComputeBuffer _splatEventsBuffer;
        private ComputeBuffer _canvasGridBuffer;
        private ComputeBuffer _sortKeysBuffer;
        private ComputeBuffer _sortValuesBuffer;
        private ComputeBuffer _sortTempKeysBuffer;
        private ComputeBuffer _sortTempValuesBuffer;

        private V4BakedHole[] _bakedHoles;
        private V4BakedLayer[] _bakedLayers;
        private readonly List<V4LiquidProfile> _profileTable = new List<V4LiquidProfile>();
        private readonly uint[] _countersReadback = new uint[V4Counters.SlotCount];
        private readonly int[] _ejectedScratch = new int[V4ParticleFlags.MaxHoles];
        private Vector4[] _debugApplyDeltaScratchDeltas;
        private Vector4[] _debugApplyDeltaScratchPredicted;

        public bool Initialized { get; private set; }
        public int Capacity { get; private set; }
        public int ActiveParticleCount { get; private set; }
        public int SpawnedTotal { get; private set; }
        public int EscapedTotal { get; private set; }
        public int SettledTotal { get; private set; }
        public int TopBandCountLastFrame { get; private set; }
        public float TotalExpectedLoss => _emaModel?.TotalExpectedLoss ?? 0f;
        public float ParticleRadius { get; private set; }
        public float SmoothingRadius { get; private set; }
        public Vector2Int CanvasGridSize { get; private set; }
        public float CanvasCellSize { get; private set; }
        public bool BakeSucceeded { get; private set; }
        public long FrameIndex { get; private set; }

        /// <summary>Test instrumentation: when true, PBF skips boundary mirror ghost density/gradient.</summary>
        public bool DebugDisableBoundaryGhosts { get; set; }

        /// <summary>Test instrumentation: clamp wCorrNum/wCorrDenom ratio before pow (0 = off).</summary>
        public float DebugScorrRatioMax { get; set; }

        /// <summary>Test instrumentation: 0=normal, 1=divide scorr by pbfIterations, 2=final PBF iter only.</summary>
        public int DebugScorrApplyMode { get; set; }

        /// <summary>Test instrumentation: saturate pow(wRatio, nCorr) before applying kCorr.</summary>
        public bool DebugScorrSaturatePow { get; set; }

        /// <summary>Test instrumentation: ApplyDelta maxCorrection scale. 0=default 0.2h, &gt;0=scale*h, &lt;0=uncapped.</summary>
        public float DebugMaxCorrectionScale { get; set; }

        /// <summary>Test instrumentation: invoked after SolveDelta with raw deltas and predicted positions.</summary>
        public Action<int, Vector4[], Vector4[]> DebugBeforeApplyDelta { get; set; }

        /// <summary>Test instrumentation: particle indices to probe in FinalizeTermsProbeKernel (max 8).</summary>
        public int[] DebugTrackParticleIndices { get; set; }

        /// <summary>Test instrumentation: invoked after FinalizeTermsProbeKernel each frame.</summary>
        public Action<V4FinalizeTermsProbe[]> DebugAfterFinalizeTermsProbe { get; set; }

        public struct V4FinalizeTermsProbe
        {
            public int ParticleIndex;
            public float Density;
            public int NeighborCount;
            public Vector3 CohesionDelta;
            public Vector3 XsphDelta;
            public float Y;
            public float R;
            public float RestDensity;
            public float C;
            public float Lambda;
            public float DeltaPreClamp;
            public float DeltaPostClamp;
            public bool Clamped;
            public uint Zone;
            public bool Inside;
            public bool ContainInside;
        }

        private const int MaxDebugFinalizeTrackSlots = 8;
        private const int DebugFinalizeProbeRowsPerSlot = 6;
        private ComputeBuffer _debugFinalizeProbeBuffer;
        private ComputeBuffer _debugBoundaryPressureNeighborsBuffer;
        private float _calibratedRestDensity;
        private bool _calibratedRestDensityLogged;
        private readonly int[] _debugTrackIndexScratch = new int[MaxDebugFinalizeTrackSlots];
        private readonly Vector4[] _debugFinalizeProbeScratch = new Vector4[MaxDebugFinalizeTrackSlots * DebugFinalizeProbeRowsPerSlot];
        private float[] _boundaryPressureDensitiesScratch;
        private Vector4[] _boundaryPressurePredictedScratch;
        private Vector4[] _boundaryPressureDeltasScratch;
        private Vector4[] _boundaryPressureBlock0Scratch;
        private uint[] _boundaryPressureFlagsScratch;
        private uint[] _boundaryPressureNeighborsScratch;
        private Vector4[] _forensicsPrevPosScratch;
        private uint[] _forensicsPrevFlagsScratch;
        private int _forensicsPrevActiveCount;
        private Vector4[] _forensicsFinalizePosScratch;
        private uint[] _forensicsFinalizeFlagsScratch;
        private ForensicsCompactionPair[] _forensicsCompactionPairsScratch;
        private int _forensicsRemovedThisFrame;

        /// <summary>Max per-iteration position correction distance (matches ApplyDeltaKernel).</summary>
        public float MaxCorrectionDistance()
        {
            if (DebugMaxCorrectionScale < 0f)
            {
                return float.MaxValue;
            }

            float scale = DebugMaxCorrectionScale > 0f ? DebugMaxCorrectionScale : 0.2f;
            return scale * SmoothingRadius;
        }

        public void ReadDeltas(Vector4[] destination)
        {
            if (_deltasBuffer == null || destination == null || destination.Length == 0 || ActiveParticleCount <= 0)
            {
                return;
            }

            _deltasBuffer.GetData(destination, 0, 0, Mathf.Min(destination.Length, ActiveParticleCount));
        }

        public void ReadPredicted(Vector4[] destination)
        {
            if (_predictedBuffer == null || destination == null || destination.Length == 0 || ActiveParticleCount <= 0)
            {
                return;
            }

            _predictedBuffer.GetData(destination, 0, 0, Mathf.Min(destination.Length, ActiveParticleCount));
        }

        public V4ParticleSoa Soa => _soa;
        public ComputeBuffer CanvasGridBuffer => _canvasGridBuffer;
        public V4GpuBucketDriver GpuBucketDriver => _gpuBucketDriver;

        /// <summary>Test instrumentation: read Poly6 densities from the last PBF density pass.</summary>
        public void ReadDensities(float[] destination)
        {
            if (_densitiesBuffer == null || destination == null || destination.Length == 0 || ActiveParticleCount <= 0)
            {
                return;
            }

            _densitiesBuffer.GetData(destination, 0, 0, Mathf.Min(destination.Length, ActiveParticleCount));
        }

        /// <summary>Networking: read the current read-side particle SOA into CPU arrays.</summary>
        public void ReadParticleSnapshot(Vector4[] block0, Vector4[] block1, uint[] colors, uint[] flags, int count)
        {
            if (_soa == null || count <= 0)
            {
                return;
            }

            int n = Mathf.Min(count, ActiveParticleCount);
            if (block0 != null && block0.Length >= n)
            {
                _soa.ReadBlock0.GetData(block0, 0, 0, n);
            }

            if (block1 != null && block1.Length >= n)
            {
                _soa.ReadBlock1.GetData(block1, 0, 0, n);
            }

            if (colors != null && colors.Length >= n)
            {
                _soa.ReadColors.GetData(colors, 0, 0, n);
            }

            if (flags != null && flags.Length >= n)
            {
                _soa.ReadFlags.GetData(flags, 0, 0, n);
            }
        }

        /// <summary>Networking: replace live particles from an authoritative CPU snapshot.</summary>
        public void ApplyNetworkParticleSnapshot(Vector4[] block0, Vector4[] block1, uint[] colors, uint[] flags, int count)
        {
            if (_soa == null)
            {
                return;
            }

            if (count < 0 || count > Capacity)
            {
                throw new ArgumentException($"count {count} out of range (capacity {Capacity})");
            }

            _soa.Upload(block0, block1, colors, flags, count);
            ActiveParticleCount = count;
            _soa.RegisterBuffers(_registry);
        }

        /// <summary>Networking: align sim frame counter with remote authority.</summary>
        public void SetNetworkFrameIndex(uint frameIndex) => FrameIndex = frameIndex;

        /// <summary>Networking: read the full canvas paint grid into a CPU array (rgb + depth).</summary>
        public void ReadCanvasGridSnapshot(Vector4[] grid)
        {
            if (_canvasGridBuffer == null || grid == null)
            {
                return;
            }

            int count = CanvasGridSize.x * CanvasGridSize.y;
            if (grid.Length < count)
            {
                throw new ArgumentException($"grid length {grid.Length} < canvas cell count {count}");
            }

            _canvasGridBuffer.GetData(grid, 0, 0, count);
        }

        /// <summary>Networking: replace the canvas paint grid and refresh the display texture.</summary>
        public void ApplyNetworkCanvasSnapshot(Vector4[] grid)
        {
            if (_canvasGridBuffer == null)
            {
                return;
            }

            int count = CanvasGridSize.x * CanvasGridSize.y;
            if (grid == null || grid.Length < count)
            {
                throw new ArgumentException($"grid length must be at least {count}");
            }

            _canvasGridBuffer.SetData(grid, 0, 0, count);
            BlitCanvasToTexture();
        }

        /// <summary>Networking: clear canvas paint to the authored base color.</summary>
        public void ResetCanvasGridForNetwork()
        {
            if (_canvasGridBuffer == null)
            {
                return;
            }

            SetCanvasUniforms();
            _executor.DispatchThreads("ClearCanvas", CanvasGridSize.x * CanvasGridSize.y);
            BlitCanvasToTexture();
        }

        /// <summary>Networking: replace baked hole data and upload to the GPU holes buffer.</summary>
        public void ApplyNetworkBakedHoles(V4BakedHole[] holes)
        {
            if (!Initialized || _holesBuffer == null)
            {
                return;
            }

            holes ??= Array.Empty<V4BakedHole>();
            _bakedHoles = (V4BakedHole[])holes.Clone();

            if (bucket != null)
            {
                bucket.holes.Clear();
                for (int i = 0; i < _bakedHoles.Length; i++)
                {
                    V4BakedHole hole = _bakedHoles[i];
                    bucket.holes.Add(new V4HoleDef
                    {
                        localPosition = hole.localPosition,
                        radius = hole.radius,
                        outwardNormal = hole.outwardNormal
                    });
                }
            }

            if (_bakedHoles.Length > 0)
            {
                if (_bakedHoles.Length > _holesBuffer.count)
                {
                    V4Log.Warning(V4LogCategory.General,
                        $"network hole count {_bakedHoles.Length} exceeds holes buffer capacity {_holesBuffer.count}; truncating upload");
                }

                int uploadCount = Mathf.Min(_bakedHoles.Length, _holesBuffer.count);
                _holesBuffer.SetData(_bakedHoles, 0, 0, uploadCount);
            }
        }

        /// <summary>GPU rest density target (lattice-calibrated) for profile index 0.</summary>
        public float GpuRestDensity(int profileIndex = 0)
        {
            float latticeRho0 = ComputeLatticeRestDensity(ParticleRadius * 2f, SmoothingRadius);
            if (_profileTable.Count == 0)
            {
                return latticeRho0;
            }

            profileIndex = Mathf.Clamp(profileIndex, 0, _profileTable.Count - 1);
            return latticeRho0 * (_profileTable[profileIndex].restDensity / 1000f);
        }
        public ComputeBuffer CountersBuffer => _countersBuffer;
        public RenderTexture CanvasTexture { get; private set; }
        public V4PassManifest ActiveManifest => _executor?.Manifest;
        public IReadOnlyList<V4BakedHole> BakedHoles => _bakedHoles;
        public IReadOnlyList<V4BakedLayer> BakedLayers => _bakedLayers;
        public IReadOnlyList<V4LiquidProfile> ProfileTable => _profileTable;

        /// <summary>Raised after every simulation step with (frameIndex, live, escaped, settled).</summary>
        public event System.Action<long, int, int, int> FrameCompleted;

        private void Start()
        {
            if (!Initialized)
            {
                Initialize();
            }
        }

        private void Update()
        {
            // An in-play assembly reload preserves serializable state (including the
            // Initialized backing bool) but wipes GPU buffer wrappers. Detect the
            // half-dead state and rebuild instead of throwing every frame.
            if (Initialized && _soa == null)
            {
                V4Log.Warning(V4LogCategory.General, "assembly reload detected mid-run; reinitializing pipeline");
                Initialized = false;
                FrameIndex = 0;
                ActiveParticleCount = 0;
                SpawnedTotal = 0;
                EscapedTotal = 0;
                SettledTotal = 0;
                Initialize();
            }

            if (Initialized && autoRun)
            {
                Step(Mathf.Min(Time.deltaTime, maxDeltaTime));
            }
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        // ------------------------------------------------------------------ init

        public void Initialize()
        {
            if (Initialized)
            {
                return;
            }

            if (bucket == null || canvas == null)
            {
                V4Log.Error(V4LogCategory.General, "V4PipelineRoot requires bucket and canvas references.");
                enabled = false;
                return;
            }

            if (spawnZones.Count == 0)
            {
                spawnZones.AddRange(GetComponentsInChildren<V4SpawnZone>());
            }

            _classificationShader = V4ShaderLibrary.Load(V4ShaderLibrary.Classification);
            _forcesShader = V4ShaderLibrary.Load(V4ShaderLibrary.ExternalForces);
            _pbfShader = V4ShaderLibrary.Load(V4ShaderLibrary.PbfSolver);
            _canvasShader = V4ShaderLibrary.Load(V4ShaderLibrary.CanvasSplat);

            ParticleRadius = V4SpawnMath.ParticleRadiusFromDensity(globalDensity);
            SmoothingRadius = ParticleRadius * 4f;

            // Bake phase (spec section 2) - once, at runtime start.
            BakeSucceeded = BakeBucket() && BakeCanvas();
            if (!BakeSucceeded)
            {
                enabled = false;
                return;
            }

            SyncBucketPoseBeforeSpawnBake();
            List<SpawnedParticle> spawned = BakeSpawnZones();
            Capacity = Mathf.Max(1, spawned.Count);

            CreateBuffers();
            UploadProfiles();
            _calibratedRestDensity = GpuRestDensity(0);
            _calibratedRestDensityLogged = false;
            InitializeGpuBucketDriver();
            AlignSpawnedParticlesToBucketTransform(spawned);
            UploadParticles(spawned);
            BuildManifestAndExecutor();
            InitializeCanvasGrid();

            _emaModel = new V4EmaLossModel(_bakedHoles.Length, emaSmoothing);
            Initialized = true;

            V4Log.Info(V4LogCategory.General,
                $"pipeline initialized capacity={Capacity} particleRadius={ParticleRadius:F4} h={SmoothingRadius:F4} holes={_bakedHoles.Length} canvasGrid={CanvasGridSize.x}x{CanvasGridSize.y}");
        }

        private void SyncBucketPoseBeforeSpawnBake()
        {
            if (!UseGpuBucketPendulum())
            {
                return;
            }

            V4SphericalPendulumController pendulum = bucket.GetComponent<V4SphericalPendulumController>();
            if (pendulum == null)
            {
                return;
            }

            pendulum.CaptureRestPoseFromScene();
            pendulum.PreparePlayInitPose();
        }

        private void AlignSpawnedParticlesToBucketTransform(List<SpawnedParticle> spawned)
        {
            if (spawned == null || spawned.Count == 0)
            {
                return;
            }

            Matrix4x4 localToWorld = bucket.LocalToWorld;
            if (_spawnLocalPoints.Count == spawned.Count)
            {
                for (int i = 0; i < spawned.Count; i++)
                {
                    Vector3 local = _spawnLocalPoints[i];
                    SpawnedParticle particle = spawned[i];
                    particle.Position = localToWorld.MultiplyPoint3x4(local);
                    spawned[i] = particle;
                }

                return;
            }

            Matrix4x4 worldToLocal = bucket.WorldToLocal;
            for (int i = 0; i < spawned.Count; i++)
            {
                SpawnedParticle particle = spawned[i];
                Vector3 local = worldToLocal.MultiplyPoint3x4(particle.Position);
                particle.Position = localToWorld.MultiplyPoint3x4(local);
                spawned[i] = particle;
            }
        }

        private bool BakeBucket()
        {
            V4BucketBake.Result result = bucket.BakeBucket();
            foreach (string error in result.Errors)
            {
                V4Log.Error(V4LogCategory.Bake, $"bucket bake: {error}");
            }

            if (!result.SpacingOk)
            {
                return false;
            }

            _bakedHoles = result.Holes ?? System.Array.Empty<V4BakedHole>();
            _bakedLayers = result.Layers ?? System.Array.Empty<V4BakedLayer>();
            V4Log.Info(
                V4LogCategory.Bake,
                $"bucket baked holes={_bakedHoles.Length} layers={_bakedLayers.Length} spacingOk={result.SpacingOk}");
            return true;
        }

        private bool BakeCanvas()
        {
            CanvasCellSize = V4CanvasGridMath.CellSize(ParticleRadius);
            CanvasGridSize = canvas.GridSize(ParticleRadius);
            long cells = (long)CanvasGridSize.x * CanvasGridSize.y;
            if (cells > 16_000_000)
            {
                V4Log.Error(V4LogCategory.Bake, $"canvas grid too large: {CanvasGridSize.x}x{CanvasGridSize.y}");
                return false;
            }

            V4Log.Info(V4LogCategory.Bake, $"canvas baked grid={CanvasGridSize.x}x{CanvasGridSize.y} cell={CanvasCellSize:F4}");
            return true;
        }

        private struct SpawnedParticle
        {
            public Vector3 Position;
            public Vector3 LocalPosition;
            public uint Color;
            public int ProfileIndex;
        }

        private readonly List<Vector3> _spawnLocalPoints = new List<Vector3>();

        private List<SpawnedParticle> BakeSpawnZones()
        {
            _profileTable.Clear();
            if (globalProfile != null)
            {
                _profileTable.Add(globalProfile);
            }

            if (bucket.heightLayers != null && bucket.heightLayers.Count > 0)
            {
                return BakeHeightLayerSpawns();
            }

            return BakeSphereSpawnZones();
        }

        private List<SpawnedParticle> BakeHeightLayerSpawns()
        {
            var spawned = new List<SpawnedParticle>();
            _spawnLocalPoints.Clear();
            V4BakedLayer[] layers = _bakedLayers ?? V4BucketBake.BakeLayers(
                bucket.heightLayers,
                bucket.height,
                bucket.topBandHeight);
            int profileIndex = ResolveProfileIndex(globalProfile);

            float spawnRadius = bucket.innerRadius - ParticleRadius;
            float spawnYMin = ParticleRadius;
            float spawnYMax = V4BucketBake.ComputeAuthoredFillHeight(bucket.heightLayers, bucket.height) - ParticleRadius;
            if (spawnYMax <= spawnYMin + 1e-6f)
            {
                V4Log.Warning(V4LogCategory.Spawn, "height layers define no spawn volume; skipping spawn bake.");
                SpawnedTotal = 0;
                return spawned;
            }

            List<Vector3> localPoints = V4SpawnMath.LatticeFillCylinder(
                spawnRadius,
                spawnYMin,
                spawnYMax,
                globalDensity);

            float spawnVolume = V4SpawnMath.CylinderSlabVolume(spawnRadius, spawnYMin, spawnYMax);
            int expectedCount = V4SpawnMath.ExpectedCount(spawnVolume, globalDensity);
            V4Log.Info(
                V4LogCategory.Spawn,
                $"cylinder spawn r={spawnRadius:F3} y=[{spawnYMin:F3},{spawnYMax:F3}] volume={spawnVolume:F4}m³ density={globalDensity:F0} expected≈{expectedCount}");

            int[] perLayer = new int[layers.Length];
            Matrix4x4 localToWorld = bucket.LocalToWorld;
            foreach (Vector3 local in localPoints)
            {
                int layerIndex = V4ZoneMath.FindLayerIndex(local.y, layers, layers.Length);
                if (layerIndex < 0)
                {
                    continue;
                }

                Color layerColor = layerIndex < bucket.heightLayers.Count
                    ? bucket.heightLayers[layerIndex].color
                    : Color.white;

                spawned.Add(new SpawnedParticle
                {
                    Position = localToWorld.MultiplyPoint3x4(local),
                    LocalPosition = local,
                    Color = PackColor(layerColor),
                    ProfileIndex = profileIndex
                });
                _spawnLocalPoints.Add(local);
                perLayer[layerIndex]++;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                Color layerColor = i < bucket.heightLayers.Count ? bucket.heightLayers[i].color : Color.white;
                V4Log.Info(
                    V4LogCategory.Spawn,
                    $"layer {i} y=[{layers[i].yMin:F3},{layers[i].yMax:F3}] color=#{ColorUtility.ToHtmlStringRGB(layerColor)} spawned={perLayer[i]}");
            }

            EnsureProfileTableFallback();
            SpawnedTotal = spawned.Count;
            return spawned;
        }

        private List<SpawnedParticle> BakeSphereSpawnZones()
        {
            var spawned = new List<SpawnedParticle>();

            // Spawn hygiene: zones authored slightly too large or overlapping each other
            // must not create particles embedded in the bucket shell (they rain outside)
            // or doubled-density lattices (they detonate the pressure solver at t=0).
            float spacing = V4SpawnMath.SpacingFromDensity(globalDensity);
            float minSeparation = spacing * 0.7f;
            float minSeparationSq = minSeparation * minSeparation;
            var occupied = new Dictionary<Vector3Int, Vector3>();
            Matrix4x4 worldToBucket = bucket.WorldToLocal;

            foreach (V4SpawnZone zone in spawnZones)
            {
                V4LiquidProfile profile = zone.profile != null ? zone.profile : globalProfile;
                int profileIndex = 0;
                if (profile != null)
                {
                    profileIndex = _profileTable.IndexOf(profile);
                    if (profileIndex < 0)
                    {
                        _profileTable.Add(profile);
                        profileIndex = _profileTable.Count - 1;
                    }
                }

                uint packedColor = PackColor(zone.color);
                List<Vector3> points = V4SpawnMath.LatticeFillSphere(zone.transform.position, zone.radius, globalDensity);
                int culledOutside = 0;
                int culledOverlap = 0;
                int kept = 0;
                foreach (Vector3 point in points)
                {
                    Vector3 local = worldToBucket.MultiplyPoint3x4(point);
                    float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                    bool inCavity = local.y >= ParticleRadius
                        && local.y <= bucket.height - ParticleRadius
                        && r <= bucket.innerRadius - ParticleRadius;
                    if (restrictSpawnToBucketCavity && !inCavity)
                    {
                        culledOutside++;
                        continue;
                    }

                    if (IsSpawnCellOccupied(occupied, point, spacing, minSeparationSq))
                    {
                        culledOverlap++;
                        continue;
                    }

                    spawned.Add(new SpawnedParticle { Position = point, Color = packedColor, ProfileIndex = profileIndex });
                    kept++;
                }

                if (culledOutside > 0 || culledOverlap > 0)
                {
                    V4Log.Warning(V4LogCategory.Spawn,
                        $"zone '{zone.name}' culled {culledOutside} points outside the bucket cavity and {culledOverlap} overlapping another zone");
                }

                V4Log.Info(V4LogCategory.Spawn,
                    $"zone '{zone.name}' volume={zone.Volume:F4} spawned={kept} profile={(profile != null ? profile.profileName : "<default>")}");
            }

            EnsureProfileTableFallback();
            SpawnedTotal = spawned.Count;
            return spawned;
        }

        private int ResolveProfileIndex(V4LiquidProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int profileIndex = _profileTable.IndexOf(profile);
            if (profileIndex < 0)
            {
                _profileTable.Add(profile);
                profileIndex = _profileTable.Count - 1;
            }

            return profileIndex;
        }

        private void EnsureProfileTableFallback()
        {
            if (_profileTable.Count > 0)
            {
                return;
            }

            V4Log.Warning(V4LogCategory.Spawn, "no liquid profile assigned anywhere; using built-in defaults");
            var fallback = ScriptableObject.CreateInstance<V4LiquidProfile>();
            fallback.profileName = "RuntimeDefault";
            _profileTable.Add(fallback);
        }

        /// <summary>
        /// Registers the point in a spatial hash of already-spawned particles and reports
        /// whether another zone's particle already sits closer than the minimum separation
        /// (zone lattices are center-offset from each other, so overlap regions would
        /// otherwise double the local density).
        /// </summary>
        private static bool IsSpawnCellOccupied(
            Dictionary<Vector3Int, Vector3> occupied, Vector3 point, float cellSize, float minSeparationSq)
        {
            var cell = new Vector3Int(
                Mathf.FloorToInt(point.x / cellSize),
                Mathf.FloorToInt(point.y / cellSize),
                Mathf.FloorToInt(point.z / cellSize));

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var key = new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz);
                if (occupied.TryGetValue(key, out Vector3 other) && (other - point).sqrMagnitude < minSeparationSq)
                {
                    return true;
                }
            }

            occupied[cell] = point;
            return false;
        }

        private void CreateBuffers()
        {
            _registry = new V4BufferRegistry();
            _soa = new V4ParticleSoa(Capacity);
            _grid = new V4SpatialHashGrid(
                V4ShaderLibrary.Load(V4ShaderLibrary.SpatialHash),
                V4ShaderLibrary.Load(V4ShaderLibrary.RadixSort),
                Capacity);
            _compactionSort = new V4GpuRadixSort(V4ShaderLibrary.Load(V4ShaderLibrary.RadixSort), Capacity);

            int holeCount = Mathf.Max(1, _bakedHoles.Length);
            int layerCount = Mathf.Max(1, _bakedLayers.Length);
            _holesBuffer = _registry.Create("_Holes", holeCount, 48, V4BufferLifetime.StaticBaked);
            _layersBuffer = _registry.Create("_Layers", layerCount, sizeof(float) * 4, V4BufferLifetime.StaticBaked);
            _profilesBuffer = _registry.Create("_Profiles", Mathf.Max(1, _profileTable.Count), V4GpuProfileData.Stride, V4BufferLifetime.StaticBaked);
            _countersBuffer = _registry.Create(V4Counters.BufferName, V4Counters.SlotCount, sizeof(uint), V4BufferLifetime.PerFrame);
            _predictedBuffer = _registry.Create("_Predicted", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _densitiesBuffer = _registry.Create("_Densities", Capacity, sizeof(float), V4BufferLifetime.PerFrame);
            _lambdasBuffer = _registry.Create("_Lambdas", Capacity, sizeof(float), V4BufferLifetime.PerFrame);
            _deltasBuffer = _registry.Create("_Deltas", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _blendedColorsBuffer = _registry.Create("_BlendedColors", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _compactionPairsBuffer = _registry.Create("_CompactionPairs", Capacity, sizeof(uint) * 2, V4BufferLifetime.PerFrame);
            _splatEventsBuffer = _registry.Create("_SplatEvents", Mathf.Max(16, maxSplatEvents), 32, V4BufferLifetime.PerFrame);
            _debugFinalizeProbeBuffer = _registry.Create(
                "_DebugFinalizeProbe",
                MaxDebugFinalizeTrackSlots * DebugFinalizeProbeRowsPerSlot,
                sizeof(float) * 4,
                V4BufferLifetime.PerFrame);
            _debugBoundaryPressureNeighborsBuffer = _registry.Create(
                "_DebugBoundaryPressureNeighbors",
                Capacity,
                sizeof(uint),
                V4BufferLifetime.PerFrame);
            _canvasGridBuffer = _registry.Create("_CanvasGrid", CanvasGridSize.x * CanvasGridSize.y, sizeof(float) * 4, V4BufferLifetime.Persistent);

            _sortKeysBuffer = new ComputeBuffer(Capacity, sizeof(uint));
            _sortValuesBuffer = new ComputeBuffer(Capacity, sizeof(uint));
            _sortTempKeysBuffer = new ComputeBuffer(Capacity, sizeof(uint));
            _sortTempValuesBuffer = new ComputeBuffer(Capacity, sizeof(uint));

            _soa.RegisterBuffers(_registry);
            _grid.RegisterBuffers(_registry);
            RegisterAliases();

            CanvasTexture = new RenderTexture(CanvasGridSize.x, CanvasGridSize.y, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            CanvasTexture.Create();
        }

        private void RegisterAliases()
        {
            int f4 = sizeof(float) * 4;
            // Read-only alias names declared in the shaders, targeting the same buffers.
            _registry.Assign("_PackedColorsRead", _soa.ReadColors, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            _registry.Assign("_PredictedRead", _predictedBuffer, Capacity, f4, V4BufferLifetime.PerFrame);
            _registry.Assign("_SortedPairs", _compactionPairsBuffer, Capacity, sizeof(uint) * 2, V4BufferLifetime.PerFrame);
            _registry.Assign("_StageBlock0", _soa.WriteBlock0, Capacity, f4, V4BufferLifetime.Persistent);
            _registry.Assign("_StageBlock1", _soa.WriteBlock1, Capacity, f4, V4BufferLifetime.Persistent);
            _registry.Assign("_StageColors", _soa.WriteColors, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            _registry.Assign("_StageFlags", _soa.WriteFlags, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            _registry.Assign("_GatherBlock0", _soa.ReadBlock0, Capacity, f4, V4BufferLifetime.Persistent);
            _registry.Assign("_GatherBlock1", _soa.ReadBlock1, Capacity, f4, V4BufferLifetime.Persistent);
            _registry.Assign("_GatherColors", _soa.ReadColors, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
            _registry.Assign("_GatherFlags", _soa.ReadFlags, Capacity, sizeof(uint), V4BufferLifetime.Persistent);
        }

        private void UploadProfiles()
        {
            float latticeRho0 = ComputeLatticeRestDensity(ParticleRadius * 2f, SmoothingRadius);
            var data = new V4GpuProfileData[Mathf.Max(1, _profileTable.Count)];
            for (int i = 0; i < _profileTable.Count; i++)
            {
                data[i] = V4GpuProfileData.From(_profileTable[i], latticeRho0);
            }

            _profilesBuffer.SetData(data);

            if (_bakedHoles.Length > 0)
            {
                _holesBuffer.SetData(_bakedHoles);
            }

            if (_bakedLayers.Length > 0)
            {
                _layersBuffer.SetData(_bakedLayers);
            }
        }

        /// <summary>Numerical rest density of a perfect cubic lattice under the Poly6 kernel.</summary>
        public static float ComputeLatticeRestDensity(float spacing, float h)
        {
            float density = 0f;
            int steps = Mathf.CeilToInt(h / spacing);
            for (int x = -steps; x <= steps; x++)
            for (int y = -steps; y <= steps; y++)
            for (int z = -steps; z <= steps; z++)
            {
                float r = new Vector3(x * spacing, y * spacing, z * spacing).magnitude;
                if (r <= h)
                {
                    density += Poly6(r, h);
                }
            }

            return density;
        }

        private static float Poly6(float r, float h)
        {
            if (r > h)
            {
                return 0f;
            }

            float h2 = h * h;
            float coeff = 315f / (64f * Mathf.PI * Mathf.Pow(h, 9f));
            float d = h2 - r * r;
            return coeff * d * d * d;
        }

        private void UploadParticles(List<SpawnedParticle> spawned)
        {
            int count = spawned.Count;
            var block0 = new Vector4[count];
            var block1 = new Vector4[count];
            var colors = new uint[count];
            var flags = new uint[count];

            bool seedPendulumVelocity = ShouldSeedPendulumSpawnVelocity();
            float torricelliSpeed = ResolveTorricelliExitSpeed();
            Vector3 bucketOrigin = bucket.transform.position;
            Vector3 downwardJet = Vector3.down * torricelliSpeed;

            for (int i = 0; i < count; i++)
            {
                SpawnedParticle p = spawned[i];
                block0[i] = new Vector4(p.Position.x, p.Position.y, p.Position.z, ParticleRadius);
                if (seedPendulumVelocity)
                {
                    Vector3 frameVel = bucket.LinearVelocity
                        + Vector3.Cross(bucket.AngularVelocity, p.Position - bucketOrigin);
                    Vector3 spawnVel = frameVel + downwardJet;
                    block1[i] = new Vector4(spawnVel.x, spawnVel.y, spawnVel.z, 0f);
                }
                else
                {
                    block1[i] = Vector4.zero;
                }

                colors[i] = p.Color;
                // Spawn state (spec section 3): Outside, hasEscaped = false.
                flags[i] = V4ParticleFlags.SetProfile(0u, p.ProfileIndex);
            }

            _soa.Upload(block0, block1, colors, flags, count);
            ActiveParticleCount = count;

            _countersBuffer.SetData(new uint[V4Counters.SlotCount]);
        }

        private bool ShouldSeedPendulumSpawnVelocity()
        {
            if (bucket == null)
            {
                return false;
            }

            V4BucketMotionSettings settings = bucket.GetComponent<V4BucketMotionSettings>();
            return settings != null && settings.IsPendulumMode;
        }

        private float ResolveTorricelliExitSpeed()
        {
            if (bucket == null)
            {
                return 0f;
            }

            V4TorricelliEjectionSettings torricelli = bucket.GetComponent<V4TorricelliEjectionSettings>();
            return torricelli != null ? torricelli.ExitSpeed : 0f;
        }

        private bool ShouldApplyNonInertialForces()
        {
            if (bucket == null)
            {
                return false;
            }

            V4BucketMotionSettings settings = bucket.GetComponent<V4BucketMotionSettings>();
            return settings != null && settings.IsPendulumMode;
        }

        private void BuildManifestAndExecutor()
        {
            V4PassManifest split = BuildManifest("v4-split", V4DeviceTier.Split);
            V4PassManifest combined = BuildManifest("v4-combined", V4DeviceTier.Combined);
            V4PassManifest chosen = V4DeviceCaps.ChooseManifest(split, combined);

            _executor = new V4PassExecutor(chosen, _registry);
            _executor.Initialize();
        }

        private V4PassManifest BuildManifest(string name, V4DeviceTier tier)
        {
            var manifest = ScriptableObject.CreateInstance<V4PassManifest>();
            manifest.manifestName = name;
            manifest.tier = tier;
            manifest.uavBudget = 8;
            manifest.passes = new List<V4PassDef>
            {
                new V4PassDef
                {
                    passId = "ClearFrameCounters",
                    shader = _classificationShader,
                    kernelName = "ClearFrameCountersKernel",
                    readWriteBuffers = new[] { V4Counters.BufferName },
                    readOnlyBuffers = new string[0]
                },
                new V4PassDef
                {
                    passId = "Classify",
                    shader = _classificationShader,
                    kernelName = "ClassifyKernel",
                    readWriteBuffers = new[] { "_Flags", V4Counters.BufferName },
                    readOnlyBuffers = new[] { "_Block0", "_Holes", "_Layers" }
                },
                new V4PassDef
                {
                    passId = "ExternalForces",
                    shader = _forcesShader,
                    kernelName = "ExternalForcesKernel",
                    readWriteBuffers = new[] { "_Block1" },
                    readOnlyBuffers = new[] { "_Block0", "_Flags", "_Holes", "_Layers", "_Profiles", V4Counters.BufferName }
                },
                new V4PassDef
                {
                    passId = "Predict",
                    shader = _pbfShader,
                    kernelName = "PredictKernel",
                    readWriteBuffers = new[] { "_Predicted" },
                    readOnlyBuffers = new[] { "_Block0", "_Block1", "_Flags" }
                },
                new V4PassDef
                {
                    passId = "Density",
                    shader = _pbfShader,
                    kernelName = "DensityKernel",
                    readWriteBuffers = new[] { "_Predicted", "_Densities", "_BlendedColors" },
                    readOnlyBuffers = new[] { "_Flags", "_PackedColorsRead", V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName }
                },
                new V4PassDef
                {
                    passId = "Lambda",
                    shader = _pbfShader,
                    kernelName = "LambdaKernel",
                    readWriteBuffers = new[] { "_Predicted", "_Densities", "_Lambdas" },
                    readOnlyBuffers = new[] { "_Flags", "_Profiles", V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName }
                },
                new V4PassDef
                {
                    passId = "SolveDelta",
                    shader = _pbfShader,
                    kernelName = "SolveDeltaKernel",
                    readWriteBuffers = new[] { "_Predicted", "_Lambdas", "_Deltas" },
                    readOnlyBuffers = new[] { "_Flags", "_Profiles", V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName }
                },
                new V4PassDef
                {
                    passId = "ApplyDelta",
                    shader = _pbfShader,
                    kernelName = "ApplyDeltaKernel",
                    readWriteBuffers = new[] { "_Predicted", "_Deltas", "_BlendedColors", "_PackedColors" },
                    // _Block0 = frame-start positions, the containment reference for the clamp.
                    readOnlyBuffers = new[] { "_Block0", "_Flags", "_Profiles" }
                },
                new V4PassDef
                {
                    passId = "FinalizeTermsProbe",
                    shader = _pbfShader,
                    kernelName = "FinalizeTermsProbeKernel",
                    readWriteBuffers = new[] { "_DebugFinalizeProbe" },
                    readOnlyBuffers = new[]
                    {
                        "_Block0", "_PredictedRead", "_Densities", "_Lambdas", "_Deltas", "_Flags", "_Profiles",
                        V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName
                    }
                },
                new V4PassDef
                {
                    passId = "BoundaryPressureNeighbor",
                    shader = _pbfShader,
                    kernelName = "BoundaryPressureNeighborKernel",
                    readWriteBuffers = new[] { "_DebugBoundaryPressureNeighbors" },
                    readOnlyBuffers = new[]
                    {
                        "_PredictedRead",
                        V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName
                    }
                },
                new V4PassDef
                {
                    passId = "Finalize",
                    shader = _pbfShader,
                    kernelName = "FinalizeKernel",
                    readWriteBuffers = new[]
                    {
                        V4ParticleSoa.WriteBlock0Name, V4ParticleSoa.WriteBlock1Name,
                        V4ParticleSoa.WriteColorsName, V4ParticleSoa.WriteFlagsName,
                        "_CompactionPairs", V4Counters.BufferName, "_SplatEvents"
                    },
                    readOnlyBuffers = new[]
                    {
                        "_Block0", "_Block1", "_Flags", "_PackedColorsRead", "_PredictedRead",
                        "_Profiles", "_Holes", V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName
                    }
                },
                new V4PassDef
                {
                    passId = "Gather",
                    shader = _pbfShader,
                    kernelName = "GatherKernel",
                    readWriteBuffers = new[] { "_GatherBlock0", "_GatherBlock1", "_GatherColors", "_GatherFlags" },
                    readOnlyBuffers = new[] { "_SortedPairs", "_StageBlock0", "_StageBlock1", "_StageColors", "_StageFlags" }
                },
                new V4PassDef
                {
                    passId = "SplatApply",
                    shader = _canvasShader,
                    kernelName = "SplatApplyKernel",
                    readWriteBuffers = new[] { "_CanvasGrid" },
                    readOnlyBuffers = new[] { "_SplatEvents", V4Counters.BufferName }
                },
                new V4PassDef
                {
                    passId = "ClearCanvas",
                    shader = _canvasShader,
                    kernelName = "ClearCanvasKernel",
                    readWriteBuffers = new[] { "_CanvasGrid" },
                    readOnlyBuffers = new string[0]
                }
            };

            return manifest;
        }

        private void InitializeCanvasGrid()
        {
            SetCanvasUniforms();
            _executor.DispatchThreads("ClearCanvas", CanvasGridSize.x * CanvasGridSize.y);
            BlitCanvasToTexture();
        }

        // ------------------------------------------------------------------ frame

        public void Step(float deltaTime)
        {
            if (!Initialized || ActiveParticleCount <= 0 || deltaTime <= 1e-6f)
            {
                return;
            }

            ConfigurePerformanceCollectorForFrame();

            RunGpuBucketFrameBegin(deltaTime);
            BindBucketStateBuffers();
            SetFrameUniforms(deltaTime);

            using (V4Log.BeginPerfPhase("PrePbf"))
            {
                _executor.Dispatch("ClearFrameCounters", 1);
                _executor.DispatchThreads("Classify", ActiveParticleCount);
                _executor.DispatchThreads("ExternalForces", ActiveParticleCount);
                _executor.DispatchThreads("Predict", ActiveParticleCount);
            }

            using (V4Log.BeginPerfPhase("SpatialHash"))
            {
                _grid.Build(_predictedBuffer, ActiveParticleCount, SmoothingRadius);
            }

            using (V4Log.BeginPerfPhase("PbfLoop"))
            {
                for (int iter = 0; iter < pbfIterations; iter++)
                {
                    _pbfShader.SetInt("_PbfIterationIndex", iter);
                    _pbfShader.SetInt("_PbfIterationCount", pbfIterations);
                    _executor.DispatchThreads("Density", ActiveParticleCount);
                    _executor.DispatchThreads("Lambda", ActiveParticleCount);
                    _executor.DispatchThreads("SolveDelta", ActiveParticleCount);
                    InvokeDebugBeforeApplyDelta(iter);
                    _executor.DispatchThreads("ApplyDelta", ActiveParticleCount);
                }
            }

            using (V4Log.BeginPerfPhase("DebugBoundaryPressure"))
            {
                InvokeFinalizeTermsProbe();
            }

            _executor.DispatchThreads("Finalize", ActiveParticleCount);

            if (debugLogWallEscapeForensics)
            {
                using (V4Log.BeginPerfPhase("DebugWallEscape"))
                {
                    CaptureWallEscapeFinalizeSnapshot();
                }
            }

            using (V4Log.BeginPerfPhase("PostFinalize"))
            {
                using (V4Log.BeginPass("CompactionSort"))
                {
                    _compactionSort.SortPairBuffers(
                        _sortKeysBuffer, _sortValuesBuffer, _sortTempKeysBuffer, _sortTempValuesBuffer,
                        ActiveParticleCount, _compactionPairsBuffer);
                }

                _executor.DispatchThreads("Gather", ActiveParticleCount);
                _executor.Dispatch("SplatApply", 1);
                BlitCanvasToTexture();
            }

            using (V4Log.BeginPerfPhase("ReadbackCounters"))
            {
                ReadBackCounters();
            }

            if (debugLogWallEscapeForensics)
            {
                using (V4Log.BeginPerfPhase("DebugWallEscape"))
                {
                    LogWallEscapeForensics();
                }
            }

            FinalizePerformanceCollectorForFrame();

            RunGpuBucketFrameEnd(deltaTime);

            FrameIndex++;
            FrameCompleted?.Invoke(FrameIndex, ActiveParticleCount, EscapedTotal, SettledTotal);
        }

        private void ConfigurePerformanceCollectorForFrame()
        {
            V4FramePerformanceCollector.Enabled = debugLogPerformance;
            V4FramePerformanceCollector.GpuSyncEnabled = debugLogPerformance && debugLogPerformanceGpuSync;
            V4FramePerformanceCollector.ProfilerMarkersEnabled = debugLogPerformance;
            if (!debugLogPerformance)
            {
                return;
            }

            V4FramePerformanceCollector.BeginFrame(FrameIndex, ActiveParticleCount);
            V4FramePerformanceCollector.SetDebugFlags(BuildActiveDebugFlagsSummary());
        }

        private void FinalizePerformanceCollectorForFrame()
        {
            if (!debugLogPerformance)
            {
                return;
            }

            CaptureFrameTimings();
            V4FramePerformanceCollector.EndFrame();
        }

        private void CaptureFrameTimings()
        {
            FrameTimingManager.CaptureFrameTimings();
            FrameTiming[] timings = new FrameTiming[1];
            uint sampleCount = FrameTimingManager.GetLatestTimings(1, timings);
            if (sampleCount == 0)
            {
                return;
            }

            V4FramePerformanceCollector.SetFrameTimings(
                timings[0].gpuFrameTime,
                timings[0].cpuFrameTime,
                timings[0].cpuMainThreadFrameTime);
        }

        private string BuildActiveDebugFlagsSummary()
        {
            var flags = new List<string>(4);
            if (debugLogBoundaryPressure)
            {
                flags.Add("boundaryPressure");
            }

            if (debugLogContainInsideMismatch)
            {
                flags.Add("containMismatch");
            }

            if (debugLogWallEscapeForensics)
            {
                flags.Add("wallEscape");
            }

            if (debugLogPerformanceGpuSync)
            {
                flags.Add("gpuSync");
            }

            return flags.Count == 0 ? "none" : string.Join(",", flags);
        }

        private bool UseGpuBucketPendulum()
        {
            if (bucket == null)
            {
                return false;
            }

            V4BucketMotionSettings settings = bucket.GetComponent<V4BucketMotionSettings>();
            return settings != null && settings.UseGpuPendulum;
        }

        private float ComputeParticleMassForBucket()
        {
            float density = GpuRestDensity();
            return density * (4f / 3f * Mathf.PI * ParticleRadius * ParticleRadius * ParticleRadius);
        }

        private void InitializeGpuBucketDriver()
        {
            _gpuBucketDriver = bucket.GetComponent<V4GpuBucketDriver>();
            if (_gpuBucketDriver == null)
            {
                _gpuBucketDriver = bucket.gameObject.AddComponent<V4GpuBucketDriver>();
            }

            _gpuBucketDriver.Initialize(_registry);

            _classifyKernel = _classificationShader.FindKernel("ClassifyKernel");
            _externalForcesKernel = _forcesShader.FindKernel("ExternalForcesKernel");
            _predictKernel = _pbfShader.FindKernel("PredictKernel");
            _densityKernel = _pbfShader.FindKernel("DensityKernel");
            _lambdaKernel = _pbfShader.FindKernel("LambdaKernel");
            _solveDeltaKernel = _pbfShader.FindKernel("SolveDeltaKernel");
            _applyDeltaKernel = _pbfShader.FindKernel("ApplyDeltaKernel");
            _finalizeKernel = _pbfShader.FindKernel("FinalizeKernel");

            V4SphericalPendulumController pendulum = bucket.GetComponent<V4SphericalPendulumController>();
            if (UseGpuBucketPendulum() && pendulum != null)
            {
                gravity = new Vector3(0f, -Mathf.Abs(pendulum.gravity), 0f);
                _gpuBucketDriver.ResetFromPendulum(pendulum);
            }
            else
            {
                bucket.SampleKinematics(0.02f);
                _gpuBucketDriver.DispatchUploadFromCpu(bucket);
                _gpuBucketDriver.ApplyStateToBucket(bucket);
            }
            BindBucketStateBuffers();
            SetBucketTransformUniforms();
            _gpuBucketDriver.ApplyStateToBucket(bucket);
        }

        private void RunGpuBucketFrameBegin(float deltaTime)
        {
            if (_gpuBucketDriver == null || !_gpuBucketDriver.IsInitialized)
            {
                bucket.SampleKinematics(deltaTime);
                return;
            }

            if (UseGpuBucketPendulum())
            {
                _gpuBucketDriver.DispatchMassReduce(
                    _soa.ReadBlock0,
                    _soa.ReadBlock1,
                    _soa.ReadFlags,
                    ComputeParticleMassForBucket(),
                    ActiveParticleCount,
                    bucket.innerRadius,
                    bucket.height,
                    bucket.wallThickness);
                _gpuBucketDriver.DispatchApplyMassReduce();
                _gpuBucketDriver.DispatchIntegrate(deltaTime);

                V4SphericalPendulumController pendulum = bucket.GetComponent<V4SphericalPendulumController>();
                if (pendulum != null)
                {
                    _gpuBucketDriver.ReadStateToCpu();
                    pendulum.SyncStateFromGpu(_gpuBucketDriver.LatestState);
                }
                else
                {
                    _gpuBucketDriver.ApplyStateToBucket(bucket);
                }

                SetBucketTransformUniforms();
                return;
            }

            bucket.SampleKinematics(deltaTime);
            _gpuBucketDriver.DispatchUploadFromCpu(bucket);
            _gpuBucketDriver.ApplyStateToBucket(bucket);
        }

        private void RunGpuBucketFrameEnd(float deltaTime)
        {
            if (_gpuBucketDriver == null || !_gpuBucketDriver.IsInitialized)
            {
                return;
            }

            if (!UseGpuBucketPendulum())
            {
                return;
            }

            _gpuBucketDriver.SyncTransformIfRequested(bucket.transform);
        }

        private void RunGpuBucketPhysicsStep(float deltaTime)
        {
            RunGpuBucketFrameBegin(deltaTime);
            RunGpuBucketFrameEnd(deltaTime);
        }

        private void BindBucketStateBuffers()
        {
            if (_gpuBucketDriver == null || !_gpuBucketDriver.IsInitialized)
            {
                return;
            }

            _gpuBucketDriver.BindBucketStateToShader(
                _classificationShader,
                _classifyKernel);
            _gpuBucketDriver.BindBucketStateToShader(
                _forcesShader,
                _externalForcesKernel);
            _gpuBucketDriver.BindBucketStateToShader(
                _pbfShader,
                _predictKernel,
                _densityKernel,
                _lambdaKernel,
                _solveDeltaKernel,
                _applyDeltaKernel,
                _finalizeKernel);
        }

        private void SetBucketTransformUniforms()
        {
            if (bucket == null)
            {
                return;
            }

            foreach (ComputeShader shader in new[] { _classificationShader, _forcesShader, _pbfShader })
            {
                V4GpuBucketDriver.SetBucketTransformUniforms(shader, bucket);
            }

            if (_gpuBucketDriver != null)
            {
                _gpuBucketDriver.SetMassReduceTransformUniforms(bucket);
            }
        }

        private void SetFrameUniforms(float deltaTime)
        {
            SetBucketTransformUniforms();
            foreach (ComputeShader shader in new[] { _classificationShader, _forcesShader, _pbfShader })
            {
                shader.SetFloat("_BucketInnerRadius", bucket.innerRadius);
                shader.SetFloat("_BucketHeight", bucket.height);
                shader.SetFloat("_BucketWallThickness", bucket.wallThickness);
                shader.SetFloat("_TopBandHeight", bucket.topBandHeight);
                shader.SetInt("_HoleCount", _bakedHoles.Length);
                shader.SetInt("_LayerCount", _bakedLayers.Length);
                shader.SetInt("_ActiveParticleCount", ActiveParticleCount);
            }
            _forcesShader.SetVector("_Gravity", gravity);
            _forcesShader.SetFloat("_DeltaTime", deltaTime);
            _forcesShader.SetFloat("_CarryRate", carryRate);
            _forcesShader.SetFloat("_TotalExpectedLoss", TotalExpectedLoss);
            _forcesShader.SetFloat("_DownwardScale", downwardScale);
            _forcesShader.SetFloat("_ApplyNonInertialForces", ShouldApplyNonInertialForces() ? 1f : 0f);
            _forcesShader.SetFloat("_HoleExitSpeedOverride", ResolveTorricelliExitSpeed());
            _forcesShader.SetInt("_LayerCount", _bakedLayers.Length);

            _pbfShader.SetFloat("_DeltaTime", deltaTime);
            _pbfShader.SetFloat("_SmoothingRadius", SmoothingRadius);
            _pbfShader.SetFloat("_CellSize", SmoothingRadius);
            _pbfShader.SetInt("_GridResolution", _grid.GridResolution);
            _pbfShader.SetFloat("_PbfEpsilon", pbfEpsilon);
            _pbfShader.SetFloat("_CanvasPlaneY", canvas.PlaneY);
            _pbfShader.SetVector("_CanvasMinCorner", canvas.MinCorner);
            _pbfShader.SetVector("_CanvasSize", new Vector2(canvas.width, canvas.depth));
            _pbfShader.SetFloat("_SettleDistance", ParticleRadius * 2f);
            _pbfShader.SetInt("_MaxSplatEvents", maxSplatEvents);
            _pbfShader.SetFloat("_BoundaryGhostWeight", boundaryGhostWeight);
            _pbfShader.SetInt("_DebugDisableBoundaryGhosts", DebugDisableBoundaryGhosts ? 1 : 0);
            _pbfShader.SetFloat("_DebugScorrRatioMax", DebugScorrRatioMax);
            _pbfShader.SetInt("_DebugScorrApplyMode", DebugScorrApplyMode);
            _pbfShader.SetInt("_DebugScorrSaturatePow", DebugScorrSaturatePow ? 1 : 0);
            _pbfShader.SetFloat("_DebugMaxCorrectionScale", DebugMaxCorrectionScale);
            SetDebugTrackUniforms();

            SetCanvasUniforms();
        }

        private void SetDebugTrackUniforms()
        {
            int count = 0;
            if (DebugTrackParticleIndices != null)
            {
                count = Mathf.Min(DebugTrackParticleIndices.Length, MaxDebugFinalizeTrackSlots);
                for (int i = 0; i < MaxDebugFinalizeTrackSlots; i++)
                {
                    _debugTrackIndexScratch[i] = i < count ? DebugTrackParticleIndices[i] : 0;
                }
            }

            _pbfShader.SetInt("_DebugTrackCount", count);
            _pbfShader.SetInts("_DebugTrackIndices", _debugTrackIndexScratch);
        }

        private void InvokeFinalizeTermsProbe()
        {
            if (ActiveParticleCount <= 0)
            {
                return;
            }

            bool wantCallback = DebugAfterFinalizeTermsProbe != null
                && DebugTrackParticleIndices != null
                && DebugTrackParticleIndices.Length > 0;
            bool wantBoundaryLog = debugLogBoundaryPressure;
            bool hasTrackIndices = DebugTrackParticleIndices != null && DebugTrackParticleIndices.Length > 0;
            bool wantProbeDispatch = wantCallback || (wantBoundaryLog && hasTrackIndices);

            if (!wantProbeDispatch && !wantBoundaryLog)
            {
                return;
            }

            V4FinalizeTermsProbe[] probes = null;
            if (wantProbeDispatch)
            {
                _executor.DispatchThreads("FinalizeTermsProbe", ActiveParticleCount);

                int slotCount = Mathf.Min(DebugTrackParticleIndices.Length, MaxDebugFinalizeTrackSlots);
                _debugFinalizeProbeBuffer.GetData(_debugFinalizeProbeScratch, 0, 0, slotCount * DebugFinalizeProbeRowsPerSlot);
                probes = BuildFinalizeTermsProbes(slotCount);
            }

            if (wantCallback && probes != null)
            {
                DebugAfterFinalizeTermsProbe(probes);
            }

            if (wantBoundaryLog)
            {
                if (probes != null)
                {
                    LogBoundaryPressureParticles(probes);
                }

                _executor.DispatchThreads("BoundaryPressureNeighbor", ActiveParticleCount);
                LogBoundaryPressureBins();
            }

            if (debugLogContainInsideMismatch)
            {
                LogContainInsideMismatchParticles();
            }
        }

        private V4FinalizeTermsProbe[] BuildFinalizeTermsProbes(int slotCount)
        {
            var probes = new V4FinalizeTermsProbe[slotCount];
            for (int slot = 0; slot < slotCount; slot++)
            {
                int row = slot * DebugFinalizeProbeRowsPerSlot;
                Vector4 meta = _debugFinalizeProbeScratch[row];
                Vector4 cohesion = _debugFinalizeProbeScratch[row + 1];
                Vector4 xsph = _debugFinalizeProbeScratch[row + 2];
                Vector4 geo = _debugFinalizeProbeScratch[row + 3];
                Vector4 clampRow = _debugFinalizeProbeScratch[row + 4];
                Vector4 zoneRow = _debugFinalizeProbeScratch[row + 5];
                probes[slot] = new V4FinalizeTermsProbe
                {
                    ParticleIndex = DebugTrackParticleIndices[slot],
                    Density = meta.x,
                    NeighborCount = (int)meta.y,
                    RestDensity = meta.z,
                    C = meta.w,
                    CohesionDelta = new Vector3(cohesion.x, cohesion.y, cohesion.z),
                    XsphDelta = new Vector3(xsph.x, xsph.y, xsph.z),
                    Y = geo.x,
                    R = geo.y,
                    Lambda = geo.z,
                    DeltaPreClamp = geo.w,
                    DeltaPostClamp = clampRow.x,
                    Clamped = clampRow.y > 0.5f,
                    Inside = clampRow.z > 0.5f,
                    ContainInside = clampRow.w > 0.5f,
                    Zone = (uint)zoneRow.x
                };
            }

            return probes;
        }

        private void LogBoundaryPressureParticles(V4FinalizeTermsProbe[] probes)
        {
            for (int i = 0; i < probes.Length; i++)
            {
                V4FinalizeTermsProbe probe = probes[i];
                V4Log.Info(
                    V4LogCategory.BoundaryPressure,
                    $"frame={FrameIndex} idx={probe.ParticleIndex} y={probe.Y:F6} r={probe.R:F6} density={probe.Density:F6} restDensity={probe.RestDensity:F6} C={probe.C:F6} lambda={probe.Lambda:F6} deltaPreClamp={probe.DeltaPreClamp:F6} deltaPostClamp={probe.DeltaPostClamp:F6} clamped={probe.Clamped} zone={probe.Zone} inside={probe.Inside} containInside={probe.ContainInside} neighborCount={probe.NeighborCount}");
            }
        }

        private void LogCalibratedRestDensityOnce()
        {
            if (_calibratedRestDensityLogged)
            {
                return;
            }

            _calibratedRestDensityLogged = true;
            V4Log.Info(V4LogCategory.BoundaryPressure, $"calibratedRestDensity={_calibratedRestDensity:F6}");
        }

        private void LogBoundaryPressureBins()
        {
            int count = ActiveParticleCount;
            if (count <= 0 || bucket == null)
            {
                return;
            }

            LogCalibratedRestDensityOnce();

            EnsureBoundaryPressureScratch(count);

            ReadDensities(_boundaryPressureDensitiesScratch);
            ReadPredicted(_boundaryPressurePredictedScratch);
            ReadDeltas(_boundaryPressureDeltasScratch);
            _soa.ReadFlags.GetData(_boundaryPressureFlagsScratch, 0, 0, count);
            _soa.ReadBlock0.GetData(_boundaryPressureBlock0Scratch, 0, 0, count);
            _debugBoundaryPressureNeighborsBuffer.GetData(_boundaryPressureNeighborsScratch, 0, 0, count);

            float yMax = Mathf.Max(bucket.topBandHeight, 1e-6f);
            float floorEnd = Mathf.Min(2f * SmoothingRadius, yMax);
            int floorBinCount = DebugBoundaryPressureFloorBinCount;
            int coarseBinCount = Mathf.Max(1, debugBoundaryPressureBinCount);
            int totalBins = floorBinCount + (yMax > floorEnd + 1e-6f ? coarseBinCount : 0);
            float maxCorrection = MaxCorrectionDistance();
            Matrix4x4 worldToLocal = bucket.WorldToLocal;
            float innerRadius = bucket.innerRadius;
            float bucketHeight = bucket.height;
            float wallThickness = bucket.wallThickness;

            var binCounts = new int[totalBins];
            var densitySums = new double[totalBins];
            var maxDensities = new float[totalBins];
            var clampedCounts = new int[totalBins];
            var neighborSums = new long[totalBins];
            var escapeZone0Counts = new int[totalBins];
            var containInsideFalseCounts = new int[totalBins];

            for (int i = 0; i < count; i++)
            {
                uint flags = _boundaryPressureFlagsScratch[i];
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(_boundaryPressurePredictedScratch[i]);
                Vector3 refLocal = worldToLocal.MultiplyPoint3x4(_boundaryPressureBlock0Scratch[i]);
                float y = localPos.y;
                int bin = YToBoundaryPressureBin(y, yMax, floorEnd, floorBinCount, coarseBinCount);

                // Hole-zone escapes are latched in Classify at frame start; by post-PBF the
                // predicted position is usually below the floor and outside the Y bins.
                // Attribute escapes to the bin of their frame-start Y (hole / rim height).
                if (V4ParticleFlags.GetZone(flags) == V4Zone.HoleZone0)
                {
                    int escapeBin = YToBoundaryPressureBin(refLocal.y, yMax, floorEnd, floorBinCount, coarseBinCount);
                    if (escapeBin >= 0)
                    {
                        escapeZone0Counts[escapeBin]++;
                    }
                }

                if (bin < 0)
                {
                    continue;
                }

                binCounts[bin]++;
                float density = _boundaryPressureDensitiesScratch[i];
                densitySums[bin] += density;
                if (density > maxDensities[bin])
                {
                    maxDensities[bin] = density;
                }

                neighborSums[bin] += _boundaryPressureNeighborsScratch[i];

                float deltaLen = _boundaryPressureDeltasScratch[i].magnitude;
                if (deltaLen > maxCorrection + 1e-6f)
                {
                    clampedCounts[bin]++;
                }

                if (!V4BucketGeometry.ComputeContainInside(flags, localPos, refLocal, innerRadius, bucketHeight, wallThickness))
                {
                    containInsideFalseCounts[bin]++;
                }
            }

            for (int bin = 0; bin < totalBins; bin++)
            {
                GetBoundaryPressureBinRange(bin, yMax, floorEnd, floorBinCount, coarseBinCount, out float lo, out float hi);
                int n = binCounts[bin];
                float avgDensity = n > 0 ? (float)(densitySums[bin] / n) : 0f;
                float maxDensity = n > 0 ? maxDensities[bin] : 0f;
                float avgNeighbors = n > 0 ? (float)neighborSums[bin] / n : 0f;
                V4Log.Info(
                    V4LogCategory.BoundaryPressure,
                    $"frame={FrameIndex} yBin=[{lo:F6},{hi:F6}) count={n} avgDensity={avgDensity:F6} maxDensity={maxDensity:F6} clampedCount={clampedCounts[bin]} avgNeighbors={avgNeighbors:F3} escapeZone0Count={escapeZone0Counts[bin]} containInsideFalseCount={containInsideFalseCounts[bin]}");
            }
        }

        private void LogContainInsideMismatchParticles()
        {
            if (!debugLogContainInsideMismatch || ActiveParticleCount <= 0 || bucket == null)
            {
                return;
            }

            EnsureBoundaryPressureScratch(ActiveParticleCount);

            ReadPredicted(_boundaryPressurePredictedScratch);
            _soa.ReadBlock0.GetData(_boundaryPressureBlock0Scratch, 0, 0, ActiveParticleCount);
            _soa.ReadFlags.GetData(_boundaryPressureFlagsScratch, 0, 0, ActiveParticleCount);

            Matrix4x4 worldToLocal = bucket.WorldToLocal;
            float innerRadius = bucket.innerRadius;
            float bucketHeight = bucket.height;
            float wallThickness = bucket.wallThickness;
            float deltaTime = Mathf.Max(Time.deltaTime, 1e-6f);
            int logged = 0;

            for (int i = 0; i < ActiveParticleCount && logged < MaxContainInsideMismatchLogsPerFrame; i++)
            {
                uint flags = _boundaryPressureFlagsScratch[i];
                if (V4ParticleFlags.HasEscaped(flags))
                {
                    continue;
                }

                Vector3 worldPos = _boundaryPressurePredictedScratch[i];
                Vector3 refWorld = _boundaryPressureBlock0Scratch[i];
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);
                Vector3 refLocal = worldToLocal.MultiplyPoint3x4(refWorld);
                bool geometricInside = V4BucketGeometry.ShouldContainInside(localPos, innerRadius, bucketHeight, wallThickness);
                bool containInside = V4BucketGeometry.ComputeContainInside(flags, localPos, refLocal, innerRadius, bucketHeight, wallThickness);
                if (containInside == geometricInside)
                {
                    continue;
                }

                float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
                Vector3 worldVel = (worldPos - refWorld) / deltaTime;
                Vector3 localVel = worldToLocal.MultiplyVector(worldVel);
                Vector3 contactVelWorld = bucket.LinearVelocity + Vector3.Cross(bucket.AngularVelocity, worldPos - bucket.transform.position);
                Vector3 contactVelLocal = worldToLocal.MultiplyVector(contactVelWorld);
                Vector3 relVel = localVel - contactVelLocal;

                float safeR = Mathf.Max(r, 1e-6f);
                Vector3 radialDir = new Vector3(localPos.x, 0f, localPos.z) / safeR;
                float radialVelocity = Vector3.Dot(relVel, radialDir);
                float verticalVelocity = relVel.y;
                string collisionBranch = V4BucketGeometry.ClassifyResolveCollisionBranches(
                    localPos, innerRadius, bucketHeight, wallThickness, containInside);

                V4Log.Info(
                    V4LogCategory.BoundaryPressure,
                    $"frame={FrameIndex} idx={i} containMismatch r={r:F6} innerRadius={innerRadius:F6} y={localPos.y:F6} radialVelocity={radialVelocity:F6} verticalVelocity={verticalVelocity:F6} collisionBranch={collisionBranch} geometricInside={geometricInside} containInside={containInside} insideFlag={V4ParticleFlags.IsInside(flags)}");

                logged++;
            }
        }

        private static int YToBoundaryPressureBin(float y, float yMax, float floorEnd, int floorBinCount, int coarseBinCount)
        {
            if (y < 0f || y >= yMax)
            {
                return -1;
            }

            if (floorBinCount > 0 && y < floorEnd)
            {
                float floorBinWidth = floorEnd / floorBinCount;
                return Mathf.Min(floorBinCount - 1, Mathf.FloorToInt(y / floorBinWidth));
            }

            if (coarseBinCount <= 0 || yMax <= floorEnd + 1e-6f)
            {
                return -1;
            }

            float coarseBinWidth = (yMax - floorEnd) / coarseBinCount;
            int coarse = Mathf.FloorToInt((y - floorEnd) / coarseBinWidth);
            coarse = Mathf.Min(coarseBinCount - 1, coarse);
            return floorBinCount + coarse;
        }

        private static void GetBoundaryPressureBinRange(
            int bin, float yMax, float floorEnd, int floorBinCount, int coarseBinCount, out float lo, out float hi)
        {
            if (bin < floorBinCount)
            {
                float floorBinWidth = floorEnd / floorBinCount;
                lo = bin * floorBinWidth;
                hi = (bin + 1) * floorBinWidth;
                return;
            }

            int coarse = bin - floorBinCount;
            float coarseBinWidth = (yMax - floorEnd) / coarseBinCount;
            lo = floorEnd + coarse * coarseBinWidth;
            hi = floorEnd + (coarse + 1) * coarseBinWidth;
        }

        private void EnsureWallEscapeScratch(int count)
        {
            if (_forensicsPrevPosScratch == null || _forensicsPrevPosScratch.Length < count)
            {
                _forensicsPrevPosScratch = new Vector4[count];
                _forensicsPrevFlagsScratch = new uint[count];
                _forensicsFinalizePosScratch = new Vector4[count];
                _forensicsFinalizeFlagsScratch = new uint[count];
                _forensicsCompactionPairsScratch = new ForensicsCompactionPair[count];
            }
        }

        private void CaptureWallEscapeFinalizeSnapshot()
        {
            int count = ActiveParticleCount;
            if (count <= 0)
            {
                _forensicsRemovedThisFrame = 0;
                return;
            }

            EnsureWallEscapeScratch(count);
            _soa.WriteBlock0.GetData(_forensicsFinalizePosScratch, 0, 0, count);
            _soa.WriteFlags.GetData(_forensicsFinalizeFlagsScratch, 0, 0, count);
            _compactionPairsBuffer.GetData(_forensicsCompactionPairsScratch, 0, 0, count);

            int removed = 0;
            for (int i = 0; i < count; i++)
            {
                if (_forensicsCompactionPairsScratch[i].SortKey == 0xFFFFFFFFu)
                {
                    removed++;
                }
            }

            _forensicsRemovedThisFrame = removed;
        }

        private void LogWallEscapeForensics()
        {
            int count = ActiveParticleCount;
            if (count <= 0 || bucket == null || _bakedHoles == null)
            {
                return;
            }

            EnsureWallEscapeScratch(Mathf.Max(count, _forensicsPrevActiveCount));

            float innerRadius = bucket.innerRadius;
            float bucketHeight = bucket.height;
            float wallThickness = bucket.wallThickness;
            Matrix4x4 worldToLocal = bucket.WorldToLocal;
            var holes = _bakedHoles;

            int detailLogs = 0;
            int outsideLive = 0;
            int beyondOuter = 0;
            int overRim = 0;
            int wallBandOutside = 0;
            int newEscape = 0;
            int falseLatch = 0;
            int insideToOutside = 0;
            int containMismatch = 0;

            if (_forensicsPrevActiveCount > 0 && _forensicsFinalizePosScratch != null)
            {
                int preCount = _forensicsPrevActiveCount;
                for (int i = 0; i < preCount && detailLogs < MaxWallEscapeDetailLogsPerFrame; i++)
                {
                    if (_forensicsCompactionPairsScratch == null || _forensicsCompactionPairsScratch[i].SortKey != 0xFFFFFFFFu)
                    {
                        continue;
                    }

                    Vector3 worldPos = _forensicsFinalizePosScratch[i];
                    Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);
                    uint flags = _forensicsFinalizeFlagsScratch[i];
                    Vector3 prevWorld = i < _forensicsPrevPosScratch.Length ? _forensicsPrevPosScratch[i] : worldPos;
                    Vector3 prevLocal = worldToLocal.MultiplyPoint3x4(prevWorld);
                    V4WallEscapeExitClass exitClass = V4WallEscapeForensics.ClassifyLocal(
                        localPos, flags, innerRadius, bucketHeight, wallThickness);
                    V4WallEscapeExitClass prevClass = V4WallEscapeForensics.ClassifyLocal(
                        prevLocal, _forensicsPrevFlagsScratch[i], innerRadius, bucketHeight, wallThickness);
                    float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);

                    V4Log.Info(
                        V4LogCategory.WallEscapeForensics,
                        $"frame={FrameIndex} event=removed idx={i} exit={V4WallEscapeForensics.ExitClassLabel(exitClass)} " +
                        $"prevExit={V4WallEscapeForensics.ExitClassLabel(prevClass)} r={r:F6} y={localPos.y:F6} " +
                        $"escaped={V4ParticleFlags.HasEscaped(flags)} inside={V4ParticleFlags.IsInside(flags)}");
                    detailLogs++;
                }
            }

            Vector4[] curPos = new Vector4[count];
            uint[] curFlags = new uint[count];
            _soa.ReadBlock0.GetData(curPos, 0, 0, count);
            _soa.ReadFlags.GetData(curFlags, 0, 0, count);

            for (int i = 0; i < count; i++)
            {
                Vector3 worldPos = curPos[i];
                Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos);
                uint flags = curFlags[i];
                V4WallEscapeExitClass exitClass = V4WallEscapeForensics.ClassifyLocal(
                    localPos, flags, innerRadius, bucketHeight, wallThickness);

                if (V4ParticleFlags.HasEscaped(flags))
                {
                    continue;
                }

                if (!V4ParticleFlags.IsInside(flags))
                {
                    outsideLive++;
                    switch (exitClass)
                    {
                        case V4WallEscapeExitClass.BeyondOuterShell:
                            beyondOuter++;
                            break;
                        case V4WallEscapeExitClass.OverOpenRim:
                            overRim++;
                            break;
                        case V4WallEscapeExitClass.OutsideWallBand:
                            wallBandOutside++;
                            break;
                    }
                }

                if (_forensicsPrevActiveCount == count && i < _forensicsPrevFlagsScratch.Length)
                {
                    uint prevFlags = _forensicsPrevFlagsScratch[i];
                    Vector3 prevLocal = worldToLocal.MultiplyPoint3x4(_forensicsPrevPosScratch[i]);

                    if (!V4ParticleFlags.HasEscaped(prevFlags) && V4ParticleFlags.HasEscaped(flags))
                    {
                        newEscape++;
                        V4WallEscapeForensics.HoleProximity hole = V4WallEscapeForensics.NearestHole(prevLocal, holes);
                        bool isFalseLatch = !hole.WithinD2;
                        if (isFalseLatch)
                        {
                            falseLatch++;
                        }

                        if (detailLogs < MaxWallEscapeDetailLogsPerFrame)
                        {
                            V4Zone zone = V4ParticleFlags.GetZone(flags);
                            V4Log.Info(
                                V4LogCategory.WallEscapeForensics,
                                $"frame={FrameIndex} event=newEscape idx={i} falseLatch={isFalseLatch} zone={zone} " +
                                $"classifyR={Mathf.Sqrt(prevLocal.x * prevLocal.x + prevLocal.z * prevLocal.z):F6} " +
                                $"classifyY={prevLocal.y:F6} hole={hole.NearestHole} holeDist={hole.Distance:F6} d0={hole.D0:F6} d2={hole.D2:F6}");
                            detailLogs++;
                        }
                    }

                    if (V4ParticleFlags.IsInside(prevFlags) && !V4ParticleFlags.IsInside(flags) && !V4ParticleFlags.HasEscaped(flags))
                    {
                        insideToOutside++;
                        if (detailLogs < MaxWallEscapeDetailLogsPerFrame)
                        {
                            float r = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
                            V4Log.Info(
                                V4LogCategory.WallEscapeForensics,
                                $"frame={FrameIndex} event=insideToOutside idx={i} exit={V4WallEscapeForensics.ExitClassLabel(exitClass)} " +
                                $"r={r:F6} y={localPos.y:F6} outerR={innerRadius + wallThickness:F6}");
                            detailLogs++;
                        }
                    }

                    Vector3 refLocal = prevLocal;
                    bool geometricInside = V4BucketGeometry.ShouldContainInside(localPos, innerRadius, bucketHeight, wallThickness);
                    bool containInside = V4BucketGeometry.ComputeContainInside(flags, localPos, refLocal, innerRadius, bucketHeight, wallThickness);
                    if (containInside != geometricInside)
                    {
                        containMismatch++;
                    }
                }
            }

            V4Log.Info(
                V4LogCategory.WallEscapeForensics,
                $"frame={FrameIndex} summary live={count} removed={_forensicsRemovedThisFrame} escapedTot={EscapedTotal} " +
                $"outsideLive={outsideLive} beyondOuter={beyondOuter} overRim={overRim} wallBandOutside={wallBandOutside} " +
                $"newEscape={newEscape} falseLatch={falseLatch} insideToOutside={insideToOutside} containMismatch={containMismatch} " +
                $"bucketVel=({bucket.LinearVelocity.x:F3},{bucket.LinearVelocity.y:F3},{bucket.LinearVelocity.z:F3}) " +
                $"bucketAng=({bucket.AngularVelocity.x:F1},{bucket.AngularVelocity.y:F1},{bucket.AngularVelocity.z:F1})");

            _soa.ReadBlock0.GetData(_forensicsPrevPosScratch, 0, 0, count);
            _soa.ReadFlags.GetData(_forensicsPrevFlagsScratch, 0, 0, count);
            _forensicsPrevActiveCount = count;
        }

        private void EnsureBoundaryPressureScratch(int count)
        {
            if (_boundaryPressureDensitiesScratch == null || _boundaryPressureDensitiesScratch.Length < count)
            {
                _boundaryPressureDensitiesScratch = new float[count];
                _boundaryPressurePredictedScratch = new Vector4[count];
                _boundaryPressureDeltasScratch = new Vector4[count];
                _boundaryPressureBlock0Scratch = new Vector4[count];
                _boundaryPressureFlagsScratch = new uint[count];
                _boundaryPressureNeighborsScratch = new uint[count];
            }
        }

        private void InvokeDebugBeforeApplyDelta(int iter)
        {
            if (DebugBeforeApplyDelta == null || ActiveParticleCount <= 0)
            {
                return;
            }

            if (_debugApplyDeltaScratchDeltas == null || _debugApplyDeltaScratchDeltas.Length < ActiveParticleCount)
            {
                _debugApplyDeltaScratchDeltas = new Vector4[ActiveParticleCount];
                _debugApplyDeltaScratchPredicted = new Vector4[ActiveParticleCount];
            }

            ReadDeltas(_debugApplyDeltaScratchDeltas);
            ReadPredicted(_debugApplyDeltaScratchPredicted);
            DebugBeforeApplyDelta(iter, _debugApplyDeltaScratchDeltas, _debugApplyDeltaScratchPredicted);
        }

        private void SetCanvasUniforms()
        {
            _canvasShader.SetInts("_CanvasGridSize", CanvasGridSize.x, CanvasGridSize.y);
            _canvasShader.SetFloat("_CanvasCellSize", CanvasCellSize);
            float maxDepth = _profileTable.Count > 0 && _profileTable[0] != null ? _profileTable[0].canvasMaxDepth : 4f;
            _canvasShader.SetFloat("_CanvasMaxDepthDefault", maxDepth);
            _canvasShader.SetVector("_CanvasBaseColor", canvas.baseColor);
            _canvasShader.SetInt("_MaxSplatEvents", maxSplatEvents);
        }

        private void BlitCanvasToTexture()
        {
            using (V4Log.BeginPass("CanvasToTexture"))
            {
                int kernel = _canvasShader.FindKernel("CanvasToTextureKernel");
                _canvasShader.SetBuffer(kernel, "_CanvasGrid", _canvasGridBuffer);
                _canvasShader.SetTexture(kernel, "_CanvasTexture", CanvasTexture);
                V4GpuSyncDispatch.Dispatch(
                    _canvasShader,
                    kernel,
                    "CanvasToTexture",
                    Mathf.CeilToInt(CanvasGridSize.x / 8f),
                    Mathf.CeilToInt(CanvasGridSize.y / 8f),
                    1);
            }
        }

        private void ReadBackCounters()
        {
            // Small synchronous readback (40 uints): keeps live counts exact, which the
            // deterministic compaction depends on. Acceptable stall at lab scale.
            _countersBuffer.GetData(_countersReadback);

            int settledNow = (int)_countersReadback[V4Counters.SettledTotal];
            int removedThisFrame = settledNow - SettledTotal;
            int splatEventsThisFrame = (int)_countersReadback[V4Counters.SplatEventCount];
            SettledTotal = settledNow;
            EscapedTotal = (int)_countersReadback[V4Counters.EscapedTotal];
            TopBandCountLastFrame = (int)_countersReadback[V4Counters.TopBandCount];
            ActiveParticleCount = Mathf.Max(0, ActiveParticleCount - removedThisFrame);

            if (removedThisFrame > 0 || splatEventsThisFrame > 0)
            {
                V4Log.Info(V4LogCategory.CanvasSettle,
                    $"frame={FrameIndex} settled+={removedThisFrame} splatEvents={splatEventsThisFrame} live={ActiveParticleCount} escaped={EscapedTotal} settled={SettledTotal}");
            }

            for (int i = 0; i < _emaModel.HoleCount; i++)
            {
                _ejectedScratch[i] = (int)_countersReadback[V4Counters.EjectedPerHoleBase + i];
            }

            _emaModel.Update(_ejectedScratch);
        }

        private static uint PackColor(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            return r | (g << 8) | (b << 16) | (0xFFu << 24);
        }

        private void ReleaseAll()
        {
            _registry?.Dispose();
            _registry = null;
            _soa?.Dispose();
            _soa = null;
            _grid?.Dispose();
            _grid = null;
            _compactionSort?.Release();
            _compactionSort = null;
            _sortKeysBuffer?.Release();
            _sortValuesBuffer?.Release();
            _sortTempKeysBuffer?.Release();
            _sortTempValuesBuffer?.Release();
            _sortKeysBuffer = _sortValuesBuffer = _sortTempKeysBuffer = _sortTempValuesBuffer = null;
            if (CanvasTexture != null)
            {
                CanvasTexture.Release();
                CanvasTexture = null;
            }

            Initialized = false;
        }
    }
}
