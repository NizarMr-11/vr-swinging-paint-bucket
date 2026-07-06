# 2 — Architecture

## Module layout

```
Assets/HarmonicEngineV4/
├── Docs/                         Documentation (this folder)
├── Editor/                       Editor-only validation tools
├── Profiles/                     Default V4Liquid_Default.asset
├── Runtime/
│   ├── Bake/                     Deterministic bake math (CPU only)
│   ├── Core/                     References, manifest, executor, registry
│   ├── Logging/                  V4Log, sinks, run recorder
│   ├── Profiles/                 V4LiquidProfile, V4GpuProfileData
│   ├── Rendering/                Visual output components
│   ├── Resources/HarmonicEngineV4/   Shaders (loaded via Resources.Load)
│   └── Simulation/               Pipeline, bucket, canvas, spatial hash
└── Tests/
    ├── EditMode/                 CPU unit tests
    └── PlayMode/                 GPU integration + parity
```

---

## Assemblies

| Assembly | Path | References | Platform |
|----------|------|------------|----------|
| `HarmonicEngineV4.Runtime` | `Runtime/HarmonicEngineV4.Runtime.asmdef` | Input System | All |
| `HarmonicEngineV4.Editor` | `Editor/HarmonicEngineV4.Editor.asmdef` | Runtime | Editor |
| `HarmonicEngineV4.Tests.EditMode` | `Tests/EditMode/` | Runtime, Editor, TestRunner | Editor |
| `HarmonicEngineV4.Tests.PlayMode` | `Tests/PlayMode/` | Runtime, TestRunner | All |

Game code references `HarmonicEngineV4.Runtime` only. Editor tools are optional.

---

## Core infrastructure

### V4PipelineRoot

MonoBehaviour orchestrator. Owns:

- Scene references (`V4Bucket`, `V4Canvas`, spawn zones)
- All `ComputeBuffer` instances (via registry)
- Pass manifest + executor
- Per-frame `Step(float deltaTime)` and one-time `Initialize()`

Public readouts: `ActiveParticleCount`, `SpawnedTotal`, `EscapedTotal`, `SettledTotal`, `FrameIndex`, `Soa`, `CanvasTexture`.

### V4PassManifest + V4PassDef

Data-driven list of GPU passes. Each entry specifies:

- `passId`, `shader`, `kernelName`
- `readWriteBuffers[]` — bound as UAV
- `readOnlyBuffers[]` — bound as SRV

Built programmatically in `V4PipelineRoot.BuildManifest()`. Two variants exist (`Split` / `Combined`) selected by `V4DeviceCaps.DetectTier()` — currently identical pass lists; infrastructure supports future divergence.

### V4PassExecutor

- Resolves kernel indices once at init
- Binds buffers by name from `V4BufferRegistry`
- Dispatches with thread-group sizing (`DispatchThreads(count)` → `(count+63)/64` groups of 64)
- Wraps each pass in `V4Log.BeginPass(passId)` for timing/category logs

### V4BufferRegistry

Named buffer table with lifetimes:

| Lifetime | When created | Examples |
|----------|--------------|----------|
| `StaticBaked` | Init, from bake | `_Holes`, profile table |
| `Persistent` | Init | SOA read/write pairs, canvas grid |
| `PerFrame` | Init | `_Predicted`, `_Deltas`, sort temps |

Supports **aliases** (e.g. `_GatherBlock0` → same buffer as `_Block0` read set).

### V4ShaderLibrary

Loads compute shaders from `Resources/HarmonicEngineV4/`:

- `V4Classification`
- `V4ExternalForces`
- `V4PbfSolver`
- `V4CanvasSplat`
- `V4SpatialHash`
- `V4RadixSort`

---

## CPU reference layer

Pure C# functions mirrored in HLSL. **Never change one side without the other.**

| C# | HLSL |
|----|------|
| `V4BucketGeometry` | `V4BucketZones.hlsl` |
| `V4ZoneMath` | `V4BucketZones.hlsl` (zone section) |
| `V4ParticleFlags` | `V4Common.hlsl` (flag macros) |
| `V4SpatialHashMath` | `V4Common.hlsl` |
| `V4CanvasSplatMath` | `V4CanvasSplat.compute` |
| `V4GpuProfileData` | `V4Profiles.hlsl` |

See [Invariants & parity](14-invariants-and-parity.md).

---

## Bake layer (init only)

Runs once at `Initialize()`:

| Component | Role |
|-----------|------|
| `V4BucketBake` | Validate holes, compute d0/d1/d2 rings, outward normals |
| `V4SpawnMath` | Density → particle radius; cubic lattice sphere fill |
| `V4CanvasGridMath` | Cell size = particle radius; grid dimensions |
| `V4PipelineRoot.BakeSpawnZones()` | Merge zones, cavity filter, dedupe overlaps |

Bake failures disable the pipeline and log via `V4Log`.

---

## Logging

`V4Log` static facade with categories (`General`, `Bake`, `Pass`, `CanvasSettle`, …) and levels. Sinks:

- `V4ConsoleLogSink` — Unity console
- `V4ChannelFileSink` — per-category files (optional)

`V4RunRecorder` writes JSONL frame samples for regression/debug sessions.

---

## Rendering (decoupled from simulation)

Render components read **live SOA buffers** and/or `CanvasTexture` each frame. They do not dispatch simulation passes. See [Rendering](10-rendering.md).

---

## Device tiers

`V4DeviceCaps.DetectTier()` checks shader model, VRAM, integrated GPU heuristics → `V4DeviceTier.Split` or `Combined`. Affects manifest selection only today.

---

## Related

- [Frame pipeline](03-frame-pipeline.md)
- [Data model](04-data-model.md)
- [GPU shaders](09-gpu-shaders.md)
