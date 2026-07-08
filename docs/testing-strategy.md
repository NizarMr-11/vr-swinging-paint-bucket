# Testing Strategy

How to run and extend tests for AdvancedHarmonicEngine_V3.

**See also:** [`architecture-coverage-todo.md`](architecture-coverage-todo.md) · [`harmonic-engine-api.md`](harmonic-engine-api.md) §15

---

## Test layout

```
Assets/Tests/
├── Resources/HarmonicPipelineTestSettings.asset   # Compute shader refs
├── Editor/
│   ├── EditMode/          # ~24 unit tests (no Play Mode required)
│   └── PlayMode/          # GPU integration tests ([UnityTest])
```

No custom `.asmdef` — tests compile into `Assembly-CSharp-Editor`.

| Mode | CI | GPU required |
|------|-----|--------------|
| Edit Mode | Yes (`.github/workflows/harmonic-editmode-tests.yml`) | Only for buffer tests that skip if no compute |
| Play Mode | Manual | **Yes** |
| Category `Stress` / `Scale` | Manual | **Yes** |

Bootstrap: menu **HarmonicEngine → Testing → Create Test Pipeline Settings Asset** or `HarmonicTestAssetBootstrap` on editor load.

---

## Play Mode factory

[`TestPipelineFactory.CreatePipeline(capacity, autoRun: false)`](../Assets/Tests/Editor/PlayMode/TestPipelineFactory.cs):

- Loads `HarmonicPipelineTestSettings` from Resources
- Real GPU dispatches via `ConfigureAndInitialize`
- Default capacity **8192**; override per test
- **`autoRun: false`** — tests call `ExecutePipelineFrame(0.016f)` explicitly

Always call `PlayModeTestUtility.EnsurePlayMode()` at the start of `[UnityTest]` methods.

Skip when unsupported:

```csharp
if (!SystemInfo.supportsComputeShaders)
    Assert.Ignore("Compute shaders not supported.");
```

---

## Scale matrix (10k / 20k / 50k)

[`HarmonicPipelineScalePlayModeTests`](../Assets/Tests/Editor/PlayMode/HarmonicPipelineScalePlayModeTests.cs) — Category **`Scale`**

| Particles | Capacity | Frames | Pass criteria |
|-----------|----------|--------|---------------|
| 10,000 | 10,000 | 20 | Active count ≥ 90% of spawned; positions finite; avg frame < 500 ms |
| 20,000 | 20,000 | 15 | Same, avg frame < 1000 ms |
| 50,000 | 50,000 | 10 | Same, avg frame < 2500 ms |

Uses **container fluid mode** with a small cylinder so SPH runs on all particles. Budgets are conservative for varied GPUs — tighten on your target hardware.

---

## Spatial-hash correctness

[`SpatialHashGridPlayModeTests`](../Assets/Tests/Editor/PlayMode/SpatialHashGridPlayModeTests.cs):

- Spawns clustered particles
- Runs one pipeline frame
- Readbacks `TryGetSpatialHashBuffers`
- Asserts keys sorted by hash, valid cell ranges, active slots not all sentinel

---

## GPU verification — Phase 1 & 2

Automated checklist for memory sanity and spatial-hash integrity before trusting SPH thermodynamics.

| Phase | Test class | What it proves |
|-------|------------|----------------|
| **1 — stride** | [`StructLayoutTests`](../Assets/Tests/Editor/EditMode/StructLayoutTests.cs), [`GpuPhase1PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase1PlayModeTests.cs) | `FluidParticle` = 48 bytes; GPU buffers use matching stride |
| **1 — lattice spawn** | [`GpuPhase1PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase1PlayModeTests.cs) | 1000 lattice particles read back on Frame 0: finite fields, zero velocity, correct color/density |
| **2 — hash keys** | [`GpuPhase2PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase2PlayModeTests.cs) | CPU `SphHashCpuMirror` matches GPU hash; neighbors in same cell share bucket |
| **2 — sort/ranges** | [`GpuPhase2PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase2PlayModeTests.cs), [`SpatialHashGridPlayModeTests`](../Assets/Tests/Editor/PlayMode/SpatialHashGridPlayModeTests.cs) | Bitonic sort monotonic; cell ranges contiguous for lattice block |

**Lattice spawner:** [`HarmonicLatticeSpawner`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/HarmonicLatticeSpawner.cs) + [`HarmonicLatticeSpawnSettings`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/HarmonicLatticeSpawnSettings.cs)

**Readback helpers:** [`GpuParticleReadbackUtility`](../Assets/AdvancedHarmonicEngine_V3/Diagnostics/GpuParticleReadbackUtility.cs), [`SphHashCpuMirror`](../Assets/AdvancedHarmonicEngine_V3/Diagnostics/SphHashCpuMirror.cs)

**Run Phase 1 & 2:** Test Runner → EditMode → filter **`Category=GPU`** (includes `GpuPhase1PlayModeTests`, `GpuPhase2PlayModeTests`, `SpatialHashGridPlayModeTests`).

---

## GPU verification — Phase 3

SPH thermodynamics and stream compaction after Phase 1/2 hash integrity is green.

| Phase | Test class | What it proves |
|-------|------------|----------------|
| **3 — density** | [`GpuPhase3PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase3PlayModeTests.cs) | Inner 2×2×2 of 10³ lattice: `ExecuteContainerSphDensityForVerification` + density cache readback; core average within ±15% of `RestDensity` (mass scaled for lattice coherence) |
| **3 — floor** | [`GpuPhase3PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase3PlayModeTests.cs) | Heavy gravity + 10 frames: every particle stays at or above `ContainerFloorY` |
| **3 — nozzle** | [`GpuPhase3PlayModeTests`](../Assets/Tests/Editor/PlayMode/GpuPhase3PlayModeTests.cs) | Bucket path: open nozzle SDF routes particles to falling buffer; internal count decreases |

**Run Phase 3:** Test Runner → **EditMode** → filter **`Category=GPU`** (same filter as Phase 1 & 2; expect 13 GPU tests total).

**Path note:** density and floor tests use **container fluid** mode; the nozzle test uses the **bucket integration** path (`SetContainerFluidEnabled` left false) because `EvaluateNozzleExitSDF` lives only in `ExecuteInternalFluidIntegration`.

**Phase 4 (SSFR) manual:** Window → Analysis → Frame Debugger while playing HarmonicEngineLab; see [`debugging-and-rendering.md`](debugging-and-rendering.md) SSFR section.

---

## Debug rendering

[`HarmonicParticleDebugSizingTests`](../Assets/Tests/Editor/EditMode/HarmonicParticleDebugSizingTests.cs):

- Point radius tracks `SmoothingRadius × multiplier` when auto-size enabled
- Manual size when disabled

[`HarmonicParticleDebugRendererPlayModeTests`](../Assets/Tests/Editor/PlayMode/HarmonicParticleDebugRendererPlayModeTests.cs):

- Container-fluid mode exposes internal buffer with active count after one frame
- World-falling-only mode exposes falling buffer

---

## Color and canvas lifecycle

| Test | Mode | Checks |
|------|------|--------|
| [`FluidParticleColorTests`](../Assets/Tests/Editor/EditMode/FluidParticleColorTests.cs) | Edit | RGBA8 pack/unpack round-trip |
| [`MultiVolumeSpawnColorPlayModeTests`](../Assets/Tests/Editor/PlayMode/MultiVolumeSpawnColorPlayModeTests.cs) | Play | Two spawn regions → distinct `PackedColorRGBA` |
| [`ColorDiffusionPlayModeTests`](../Assets/Tests/Editor/PlayMode/ColorDiffusionPlayModeTests.cs) | Play | Low vs high `_ColorDiffusionRate` |
| [`CanvasPerHitColorPlayModeTests`](../Assets/Tests/Editor/PlayMode/CanvasPerHitColorPlayModeTests.cs) | Play | Canvas hit buffer carries particle color |

See [`paint-color-and-lifecycle.md`](paint-color-and-lifecycle.md).

---

## Running locally

**Edit Mode:** Unity → Window → General → Test Runner → EditMode → Run All

**Play Mode / Scale / Stress:** Test Runner → PlayMode → filter `Category=Scale` or `Category=Stress`

**Headless Edit Mode (CI script):**

```powershell
.\scripts\RunHarmonicEditModeTests.ps1
```

---

## Adding a new GPU test

1. Create class under `Assets/Tests/Editor/PlayMode/`.
2. Use `TestPipelineFactory.CreatePipeline(capacity)`.
3. `[UnityTest]` + `yield return PlayModeTestUtility.EnsurePlayMode()`.
4. `[Category("GPU")]` or `[Category("Scale")]` as appropriate.
5. `Object.DestroyImmediate(pipeline.gameObject)` in cleanup.

---

## HarmonicEngineV4 test strategy

V4 tests live under `Assets/HarmonicEngineV4/Tests/` with their own asmdefs
(`HarmonicEngineV4.Tests.EditMode`, `HarmonicEngineV4.Tests.PlayMode`) and are
organized in three layers. Every geometric/zone/flag function exists twice
(C# reference + HLSL transliteration); the layers keep them provably in lockstep.

| Layer | Where | What it proves |
|-------|-------|----------------|
| **1 — CPU reference units** | `Tests/EditMode/` (`V4BucketGeometryTests`, `V4ZoneMathTests`, `V4BucketBakeTests`, `V4CanvasSplatMathTests`, …) | Pure math is correct in isolation, including containment edge cases (deep wall tunneling, floor-slab sweep, beyond-outer shell clamp, outside-never-resolves-inward) |
| **2 — GPU/CPU parity** | `Tests/PlayMode/V4GpuCpuParityTests` + `V4TestKernels.compute` | The HLSL mirrors match the C# reference on thousands of seeded samples, for both collision modes (`containInside` true/false) |
| **3 — pipeline invariants** | `Tests/PlayMode/` (`V4PipelineIntegrationTests`, `V4ContainmentTests`, `V4GoldenFrameTests`, `V4CanvasGpuTests`) | Full-pipeline behavior: conservation, determinism, escape latch, golden-frame regression |

Key invariant suites:

- **`V4ContainmentTests`** — bucket motion must never leak fluid:
  - `LateralShake_NoParticleLeaksThroughWall` — 1 m/s side-to-side sway; every
    non-escaped particle stays inside the cavity (or above the open rim).
  - `UpwardMove_NoParticleFallsThroughFloor` — 1.5 m/s upward sweep; the floor
    slab may not pass through resting fluid.
  - `FastCanvasImpact_IsAbsorbedAsPaintSplat` — free-falling paint is absorbed
    on impact (impact splat), shrinking the live count and adding canvas depth.
- **`V4PipelineIntegrationTests`** — conservation every frame
  (`spawned == live + settled`), rest stability, escape-latch permanence under
  violent motion, rigid carry entrainment, bit-exact determinism of state + canvas.
- **`V4GoldenFrameTests`** — fixed 100-frame choreography; two runs must be
  bit-identical and the behavior envelope (escapes happen, counts monotonic,
  no NaN) must hold across code changes.
- **`V4WallEscapeFirmHoldInvestigationTests`** — sustained directional bucket
  hold (Lab2 config): asserts `beyondOuter` stays near zero after the
  beyond-outer shell clamp; reports `overRim` / `falseLatch` / `cif` separately.
- **`V4FramePerformanceCollectorTests`** (EditMode) — perf scope ranking and channel settings for `performance.log`.

Run them from Test Runner → PlayMode / EditMode with the `HarmonicEngineV4`
assembly filter. All V4 PlayMode tests require compute shader support.
