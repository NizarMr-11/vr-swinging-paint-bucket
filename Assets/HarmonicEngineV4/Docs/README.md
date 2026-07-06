# HarmonicEngineV4 Documentation

Complete documentation for the V4 GPU fluid engine. Read in order if you are new; use individual pages as reference once familiar.

## What is V4?

HarmonicEngineV4 is a **GPU Position-Based Fluids (PBF)** engine for simulating paint-like liquid in a swinging bucket. Particles live in a Structure-of-Arrays (SOA) on the GPU. Each frame the CPU orchestrates compute passes: classify → forces → predict → spatial hash → PBF iterations → finalize → compaction → canvas splats → render.

**Location:** `Assets/HarmonicEngineV4/`

**Lab scene:** `Assets/Scenes/HarmonicEngineLab2.unity`

---

## Documentation index

| # | Document | What you'll learn |
|---|----------|-------------------|
| 1 | [Overview](01-overview.md) | Goals, design principles, V3 vs V4, glossary |
| 2 | [Architecture](02-architecture.md) | Modules, assemblies, manifest executor, buffer registry |
| 3 | [Frame pipeline](03-frame-pipeline.md) | Exact per-frame pass order and init sequence |
| 4 | [Data model](04-data-model.md) | Particle SOA, flags, counters, profiles, holes, canvas grid |
| 5 | [Bucket & zones](05-bucket-and-zones.md) | Geometry, classification, containment, escape |
| 6 | [PBF solver](06-pbf-solver.md) | Density, lambda, deltas, finalize, compaction |
| 7 | [External forces](07-external-forces.md) | Gravity, carry, hole zones, top-band EMA loss |
| 8 | [Canvas & splats](08-canvas-and-splats.md) | Paint grid, impact absorption, slow settle |
| 9 | [GPU shaders](09-gpu-shaders.md) | Compute kernels, HLSL includes, buffer bindings |
| 10 | [Rendering](10-rendering.md) | Debug points, screen-space fluid, canvas quad |
| 11 | [Configuration](11-configuration.md) | Liquid profiles, scene wiring, tuning guide |
| 12 | [API reference](12-api-reference.md) | Public C# types and entry points |
| 13 | [Testing](13-testing.md) | EditMode, PlayMode, parity, containment tests |
| 14 | [Invariants & parity](14-invariants-and-parity.md) | Rules that must never break; CPU↔GPU pairs |
| 15 | [Getting started](15-getting-started.md) | First run, scene setup, common tasks |
| 16 | [Source index](16-source-index.md) | Complete file/type listing |

---

## Quick reference

```
HarmonicEngineV4/
├── Docs/                    ← you are here
├── Editor/                  Inspector tools (manifest validation)
├── Profiles/                Default liquid profile asset
├── Runtime/
│   ├── Bake/                Spawn lattice, hole rings, canvas grid math
│   ├── Core/                Geometry, zones, manifest, buffers, splat math
│   ├── Logging/             V4Log, run recorder, file sinks
│   ├── Profiles/            V4LiquidProfile ScriptableObject
│   ├── Rendering/           Debug points, SSFR, canvas, bucket mesh
│   ├── Resources/           Compute shaders + HLSL includes
│   └── Simulation/          V4PipelineRoot, bucket, canvas, spatial hash
└── Tests/
    ├── EditMode/            Pure CPU unit tests
    └── PlayMode/            GPU integration + parity tests
```

**Central orchestrator:** `V4PipelineRoot` (`Runtime/Simulation/V4PipelineRoot.cs`)

**Shaders:** loaded from `Resources/HarmonicEngineV4/` via `V4ShaderLibrary` — no manual inspector wiring.

---

## Related project docs

Older docs under `docs/` describe **AdvancedHarmonicEngine_V3** and the game layer. For V4-specific work, prefer this folder.

- [`docs/testing-strategy.md`](../../../docs/testing-strategy.md) — includes a V4 section
- [`docs/engine-overview.md`](../../../docs/engine-overview.md) — project-wide overview (V3-centric)
