# Architecture Divergences (V3.1 Spec vs Code)

Reconciles [`architecure.md`](architecure.md) with the current implementation. Approved deviations are also in [`spec-deviations.md`](spec-deviations.md).

---

## Naming and structure

| Spec (`architecure.md`) | Codebase |
|-------------------------|----------|
| `FluidPhysicsSolverV3.compute` | [`StreamCompactionPingPong.compute`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/StreamCompactionPingPong.compute) |
| `IntegratedPipelineControllerV3` | [`PipelineExecutionController`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/PipelineExecutionController.cs) (~1,100 lines) |
| `HarmonicEngine.Core.Types` | `HarmonicEngine.Domain.Models` |
| 6-stage pipeline only | + container fluid, world-falling, Eulerian drag, bake/playback, diagnostics |

---

## Algorithmic deviations

| Topic | Spec | Implementation | Ref |
|-------|------|----------------|-----|
| Stream compaction | `ConsumeStructuredBuffer.Consume()` | Indexed read + `AppendStructuredBuffer` | DEV-001 |
| Quantized bake layout | Exact 16-byte field order in spec | Project layout (validated by tests) | DEV-002 |
| Eulerian drag | Core pillar | Optional, partial | DEV-004 |
| Capacity target | 1M–5M | Lab scene capped at **30k** (`HarmonicEngineLimits`) | Lab-only |

---

## Canvas and bucket (API vs collision)

| Spec implication | Reality |
|------------------|---------|
| Impasto height-mapped canvas | **Plane at `planeY`**, not mesh collision. UV mapping on a quad via `CanvasController`. Impasto is a separate height stamp pass. |
| Bucket rigid body + fluid | **Analytic** collision: nozzle SDF + rim (bucket) or open-top **cylinder** (container). Visible bucket mesh is cosmetic. |

The new [`configuration-api.md`](configuration-api.md) makes this explicit: you configure `HarmonicCanvasSurface` (plane) and `HarmonicBucketVolume` (cylinder/SDF), not mesh colliders.

---

## Neighbor queries

| Spec | Code |
|------|------|
| Pseudocode in §5.3 | Fully implemented in GPU; extracted to [`SphNeighborQuery.hlsl`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/Include/SphNeighborQuery.hlsl) |
| No C# query API | By design — queries are GPU-only; see [`neighbor-queries-and-spatial-hashing.md`](neighbor-queries-and-spatial-hashing.md) |

---

## What matches the spec well

- Ping-pong A/B buffers
- Unified indirect args + `CalculateGridArgsKernel`
- Two-pass SPH (density then integration)
- Fixed power-of-two sort buffer with `0xFFFFFFFF` padding
- FP16 quantization path for bake readback
- 32-byte `FluidParticle`, 8-byte grid structs (see `StructLayoutTests`)

---

## Lab vs production scenes

| Scene | Role |
|-------|------|
| `HarmonicEngineLab` | Container fluid, 30k cap, rain director — primary dev target |
| `ClassicPaintSimulation` / `MainSimulation` | Full bucket + canvas paint flow |
| Quality tiers | Still 100k–5M presets; lab overrides `maxCapacity` in scene |
