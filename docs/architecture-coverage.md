# Architecture Coverage Report (`docs/architecure.md` V3.1)

**Last verified:** 2026-06-02  
**Branch:** `anas-sandbox`

**Related:** [Test & coverage TODO](architecture-coverage-todo.md) · [Unity API guide](harmonic-engine-api.md)

---

## Executive verdict

| Metric | Result |
|--------|--------|
| **Spec structure (§2–§7)** | ~**95%** implemented |
| **Literal spec match (§5.3 consume, §1 Eulerian, §1 5M proof)** | ~**85%** — see gaps |
| **Automated test coverage** | ~**70%** — 19 test files; many integration paths untested |
| **Runtime verified in Unity** | Partial — compiles; Play Mode smoke OK; full Test Runner pass pending locally |

**The engine is usable in Unity.** It is **not a 100% literal clone** of every spec detail. Use [`architecture-coverage-todo.md`](architecture-coverage-todo.md) as the backlog to reach full compliance + test proof.

---

## Verified in Unity (this session)

| Check | Result |
|-------|--------|
| Script compile | ✅ 0 errors after Burst/input fixes |
| Play Mode `MainSimulation` | ✅ Enters without console spam (after Input System fix) |
| Scene wiring | ✅ `HarmonicPipelineRoot`, `SimulationManager/Bucket` bridges, `Canvas` + impasto |
| Edit/Play Test Runner (MCP) | ⚠️ Jobs orphaned / init timeout — could not get full automated pass |
| 5M particle load test | ❌ Not executed on this machine |

---

## Spec section mapping

### §1 Executive pillars

| Pillar | Status | Notes |
|--------|--------|-------|
| Ping-pong A/B buffers | ✅ | `PingPongCounterManager`, append buffers |
| GPU indirect Option A | ✅ | `ArgumentUtility.compute`, `CopyCount` → slot 3 |
| Two-pass SPH | ✅ | Density + integration kernels |
| Quantized VRAM cache | ⚠️ | GPU quantize + async readback exist; full cinematic bake @ 5M not profiled |
| Eulerian voxel drag | ⚠️ | Clear/scatter/apply kernels; no advect/wind pass; off by default |
| Impasto canvas | ✅ | Shader + height map + GPU hit bridge |

### §2 Memory topology

| Item | Status |
|------|--------|
| `FluidParticle` 32 B | ✅ |
| `QuantizedBakeParticle` 16 B | ✅ (packed `uint2` layout, semantically equivalent) |
| `GridKeyPair`, `HashCellGridRange` | ✅ |
| VRAM buffer set | ✅ + extensions (`CanvasPaintHit`, drag grid, falling world) |

### §3 Six-stage GPU pipeline

| Stage | Status |
|-------|--------|
| 1 Grid clear | ✅ |
| 2 Hash generation | ✅ |
| 3 Bitonic sort | ✅ |
| 4 Cell mapping | ✅ |
| 5 SPH density | ✅ |
| 6 Integration + compaction | ⚠️ **Indexed read + append**, not spec `ConsumeStructuredBuffer` |

**Extra stages (beyond §3):** Stage 7 falling world, canvas hit append, optional Eulerian drag, quantize pass.

### §4 Trap mitigations

| Trap | Status |
|------|--------|
| 4.1 Thread group multiplier | ✅ |
| 4.2 Two-pass SPH | ✅ |
| 4.3 Sort isolation | ✅ |
| 4.4 PCIe / FP16 compaction | ✅ kernel + readback path |

### §5 Shader specs

| Asset (spec name) | Project file | Match |
|-------------------|--------------|-------|
| `ArgumentUtility.compute` | ✅ | Exact |
| `SpatialHashGridIndirect.compute` | ✅ | Exact |
| `FluidPhysicsSolverV3.compute` | `StreamCompactionPingPong.compute` | Renamed; logic equivalent except consume pattern |
| `DataCompactionPacker.compute` | ✅ | Exact |

### §6 Orchestrator

| Spec | Project | Match |
|------|---------|-------|
| `IntegratedPipelineControllerV3` | `PipelineExecutionController` | Renamed; superset |

### §7 Directory blueprint

All required folders/files present. Additions beyond spec: canvas hit bridge, debug renderer, quality presets, simulation modes, bake playback driver.

---

## Known gaps vs `architecure.md`

1. **`ConsumeStructuredBuffer` stream compaction (§5.3)** — uses indexed `_DensityWritableCache` + append output (parallel-safe deviation).
2. **Burst RK4 pendulum (§7)** — RK4 math implemented; Burst job wrapper not used (scalar integrator after Burst struct errors).
3. **5M particle validation (§1 target)** — capacity configurable; no successful 5M stress run recorded.
4. **Bake playback (§4.4)** — frames write to disk; playback driver logs counts, does not fully reconstruct GPU particle view.
5. **Eulerian field (§1)** — scatter/apply only; no advect/decay pass; disabled unless `enableEulerianDrag`.
6. **VR/XR interaction** — keyboard controls only; no headset grab/UI package integration.
7. **Automated CI test gate** — tests exist; MCP Test Runner did not complete reliably this session.

---

## Manual QA checklist

- [ ] Press Play → simulation auto-starts (`autoStartSimulationOnPlay` on `SimulationManager`)
- [ ] Bucket swings; paint appears on canvas
- [ ] Impasto height visible on canvas material
- [ ] Reset clears canvas + height map
- [ ] Run Test Runner locally (Edit Mode) — all green
- [ ] Optional: `Category=Stress` on target GPU

See [hardware-requirements.md](hardware-requirements.md).

---

## Next steps

1. Work through [`architecture-coverage-todo.md`](architecture-coverage-todo.md) (Sprints A→D).
2. Integrate using [`harmonic-engine-api.md`](harmonic-engine-api.md).
3. Update `ArchitectureManifest.FeatureMatrix` when a gap closes **and** its test lands.
