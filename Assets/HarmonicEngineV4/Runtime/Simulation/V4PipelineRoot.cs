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

        [Header("Simulation")]
        public bool autoRun = true;
        [Range(1, 4)] public int pbfIterations = 2;
        [Min(0f)] public float carryRate = 10f;
        [Min(0f)] public float downwardScale = 1f;
        [Range(0f, 1f)] public float emaSmoothing = 0.1f;
        public float pbfEpsilon = 100f;
        [Min(0.001f)] public float maxDeltaTime = 1f / 50f;
        public Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        [Min(16)] public int maxSplatEvents = 1024;

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

        private ComputeBuffer _holesBuffer;
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
        private readonly List<V4LiquidProfile> _profileTable = new List<V4LiquidProfile>();
        private readonly uint[] _countersReadback = new uint[V4Counters.SlotCount];
        private readonly int[] _ejectedScratch = new int[V4ParticleFlags.MaxHoles];

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

        public V4ParticleSoa Soa => _soa;
        public ComputeBuffer CanvasGridBuffer => _canvasGridBuffer;
        public ComputeBuffer CountersBuffer => _countersBuffer;
        public RenderTexture CanvasTexture { get; private set; }
        public V4PassManifest ActiveManifest => _executor?.Manifest;
        public IReadOnlyList<V4BakedHole> BakedHoles => _bakedHoles;
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

            List<SpawnedParticle> spawned = BakeSpawnZones();
            Capacity = Mathf.Max(1, spawned.Count);

            CreateBuffers();
            UploadProfiles();
            UploadParticles(spawned);
            BuildManifestAndExecutor();
            InitializeCanvasGrid();

            _emaModel = new V4EmaLossModel(_bakedHoles.Length, emaSmoothing);
            Initialized = true;

            V4Log.Info(V4LogCategory.General,
                $"pipeline initialized capacity={Capacity} particleRadius={ParticleRadius:F4} h={SmoothingRadius:F4} holes={_bakedHoles.Length} canvasGrid={CanvasGridSize.x}x{CanvasGridSize.y}");
        }

        private bool BakeBucket()
        {
            V4BucketBake.Result result = bucket.BakeHoles();
            foreach (string error in result.Errors)
            {
                V4Log.Error(V4LogCategory.Bake, $"bucket bake: {error}");
            }

            if (!result.SpacingOk)
            {
                return false;
            }

            if (result.RingsShrunk)
            {
                V4Log.Warning(V4LogCategory.Bake, "bucket bake: zone rings were auto-shrunk to satisfy hole spacing");
            }

            _bakedHoles = result.Holes;
            V4Log.Info(V4LogCategory.Bake, $"bucket baked holes={_bakedHoles.Length} spacingOk={result.SpacingOk}");
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
            public uint Color;
            public int ProfileIndex;
        }

        private List<SpawnedParticle> BakeSpawnZones()
        {
            _profileTable.Clear();
            if (globalProfile != null)
            {
                _profileTable.Add(globalProfile);
            }

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
                    if (!inCavity)
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

            if (_profileTable.Count == 0)
            {
                V4Log.Warning(V4LogCategory.Spawn, "no liquid profile assigned anywhere; using built-in defaults");
                var fallback = ScriptableObject.CreateInstance<V4LiquidProfile>();
                fallback.profileName = "RuntimeDefault";
                _profileTable.Add(fallback);
            }

            SpawnedTotal = spawned.Count;
            return spawned;
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
            _holesBuffer = _registry.Create("_Holes", holeCount, 48, V4BufferLifetime.StaticBaked);
            _profilesBuffer = _registry.Create("_Profiles", Mathf.Max(1, _profileTable.Count), V4GpuProfileData.Stride, V4BufferLifetime.StaticBaked);
            _countersBuffer = _registry.Create(V4Counters.BufferName, V4Counters.SlotCount, sizeof(uint), V4BufferLifetime.PerFrame);
            _predictedBuffer = _registry.Create("_Predicted", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _densitiesBuffer = _registry.Create("_Densities", Capacity, sizeof(float), V4BufferLifetime.PerFrame);
            _lambdasBuffer = _registry.Create("_Lambdas", Capacity, sizeof(float), V4BufferLifetime.PerFrame);
            _deltasBuffer = _registry.Create("_Deltas", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _blendedColorsBuffer = _registry.Create("_BlendedColors", Capacity, sizeof(float) * 4, V4BufferLifetime.PerFrame);
            _compactionPairsBuffer = _registry.Create("_CompactionPairs", Capacity, sizeof(uint) * 2, V4BufferLifetime.PerFrame);
            _splatEventsBuffer = _registry.Create("_SplatEvents", Mathf.Max(16, maxSplatEvents), 32, V4BufferLifetime.PerFrame);
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

            for (int i = 0; i < count; i++)
            {
                SpawnedParticle p = spawned[i];
                block0[i] = new Vector4(p.Position.x, p.Position.y, p.Position.z, ParticleRadius);
                block1[i] = Vector4.zero;
                colors[i] = p.Color;
                // Spawn state (spec section 3): Outside, hasEscaped = false.
                flags[i] = V4ParticleFlags.SetProfile(0u, p.ProfileIndex);
            }

            _soa.Upload(block0, block1, colors, flags, count);
            ActiveParticleCount = count;

            _countersBuffer.SetData(new uint[V4Counters.SlotCount]);
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
                    readOnlyBuffers = new[] { "_Block0", "_Holes" }
                },
                new V4PassDef
                {
                    passId = "ExternalForces",
                    shader = _forcesShader,
                    kernelName = "ExternalForcesKernel",
                    readWriteBuffers = new[] { "_Block1" },
                    readOnlyBuffers = new[] { "_Block0", "_Flags", "_Holes", "_Profiles", V4Counters.BufferName }
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
                    readOnlyBuffers = new[] { "_PackedColorsRead", V4SpatialHashGrid.CellStartEndName, V4SpatialHashGrid.GridKeyValueName }
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
                    readOnlyBuffers = new[] { "_Flags", "_Profiles" }
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

            bucket.SampleKinematics(deltaTime);
            SetFrameUniforms(deltaTime);

            _executor.Dispatch("ClearFrameCounters", 1);
            _executor.DispatchThreads("Classify", ActiveParticleCount);
            _executor.DispatchThreads("ExternalForces", ActiveParticleCount);
            _executor.DispatchThreads("Predict", ActiveParticleCount);

            _grid.Build(_predictedBuffer, ActiveParticleCount, SmoothingRadius);

            for (int iter = 0; iter < pbfIterations; iter++)
            {
                _executor.DispatchThreads("Density", ActiveParticleCount);
                _executor.DispatchThreads("Lambda", ActiveParticleCount);
                _executor.DispatchThreads("SolveDelta", ActiveParticleCount);
                _executor.DispatchThreads("ApplyDelta", ActiveParticleCount);
            }

            _executor.DispatchThreads("Finalize", ActiveParticleCount);

            using (V4Log.BeginPass("CompactionSort"))
            {
                _compactionSort.SortPairBuffers(
                    _sortKeysBuffer, _sortValuesBuffer, _sortTempKeysBuffer, _sortTempValuesBuffer,
                    ActiveParticleCount, _compactionPairsBuffer);
            }

            _executor.DispatchThreads("Gather", ActiveParticleCount);
            _executor.Dispatch("SplatApply", 1);
            BlitCanvasToTexture();

            ReadBackCounters();
            FrameIndex++;
            FrameCompleted?.Invoke(FrameIndex, ActiveParticleCount, EscapedTotal, SettledTotal);
        }

        private void SetFrameUniforms(float deltaTime)
        {
            Matrix4x4 worldToLocal = bucket.WorldToLocal;
            Matrix4x4 localToWorld = bucket.LocalToWorld;

            foreach (ComputeShader shader in new[] { _classificationShader, _forcesShader, _pbfShader })
            {
                shader.SetMatrix("_BucketWorldToLocal", worldToLocal);
                shader.SetMatrix("_BucketLocalToWorld", localToWorld);
                shader.SetFloat("_BucketInnerRadius", bucket.innerRadius);
                shader.SetFloat("_BucketHeight", bucket.height);
                shader.SetFloat("_BucketWallThickness", bucket.wallThickness);
                shader.SetFloat("_TopBandHeight", bucket.topBandHeight);
                shader.SetInt("_HoleCount", _bakedHoles.Length);
                shader.SetInt("_ActiveParticleCount", ActiveParticleCount);
            }

            _forcesShader.SetVector("_BucketLinearVelocity", bucket.LinearVelocity);
            _forcesShader.SetVector("_BucketAngularVelocity", bucket.AngularVelocity);
            _forcesShader.SetVector("_BucketWorldOrigin", bucket.transform.position);
            _forcesShader.SetVector("_Gravity", gravity);
            _forcesShader.SetFloat("_DeltaTime", deltaTime);
            _forcesShader.SetFloat("_CarryRate", carryRate);
            _forcesShader.SetFloat("_TotalExpectedLoss", TotalExpectedLoss);
            _forcesShader.SetFloat("_DownwardScale", downwardScale);

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

            SetCanvasUniforms();
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
                _canvasShader.Dispatch(
                    kernel,
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
