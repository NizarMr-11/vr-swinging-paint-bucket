# Architecture Coverage & Test TODO

**Spec:** [`docs/architecure.md`](architecure.md) V3.1-Final  
**Audit date:** 2026-06-02  
**Overall implementation:** ~**95%** structural | ~**70%** validated by automated tests

Use this document as the single backlog for closing gaps between the spec, code, and test suite.

---

## Coverage summary

| Spec section | Implemented | Tested | Gap priority |
|--------------|-------------|--------|--------------|
| §1 Executive pillars | 5/6 full, 1 partial | 3/6 | P1 |
| §2 Memory layouts | 6/6 | 5/6 | P2 |
| §3 Six-stage pipeline | 6/6 (+ extras) | 4/6 | P1 |
| §4 Trap mitigations | 4/4 | 3/4 | P1 |
| §5 Shader kernels | 12/12 exist | 8/12 bound-tested | P1 |
| §6 Orchestrator | ✅ superset | ⚠️ partial | P1 |
| §7 Blueprint files | ✅ + extensions | ⚠️ manifest only | P2 |

**Legend:** ✅ done | ⚠️ partial | ❌ missing | 🔲 test TODO

---

## §1 Executive summary — item checklist

| # | Spec requirement | Code location | Status | Existing test | Test TODO |
|---|------------------|---------------|--------|---------------|-----------|
| 1.1 | Ping-pong A/B buffers | `PingPongCounterManager`, `_bufferInternalA/B` | ✅ | `PingPongCounterManagerTests` | 🔲 Play Mode: 1000-frame count stability |
| 1.2 | GPU indirect Option A | `ArgumentUtility.compute`, `PipelineExecutionController` | ✅ | `IndirectDispatchMathTests`, kernel test | 🔲 Play Mode: verify indirect args slot 3 → groups |
| 1.3 | Two-pass SPH | `StreamCompactionPingPong.compute` | ✅ | `SolverTests`, Play Mode pipeline | 🔲 Play Mode: density before integration ordering |
| 1.4 | Quantized VRAM cache (FP16) | `DataCompactionPacker.compute`, `HarmonicBakeRecorder` | ⚠️ readback works; 5M not profiled | `QuantizedFrameEncoderTests`, `HalfPrecisionCompressorTests` | 🔲 Play Mode: round-trip quantize/dequantize error bound |
| 1.5 | Eulerian voxel drag field | `EulerianDragGrid.compute` | ⚠️ scatter/apply only; no advect | `ComputeShaderKernelTests` | 🔲 Play Mode: drag coefficient changes trajectory |
| 1.6 | Impasto height canvas | `ImpastoCanvasDisplace.shader`, `HighScaleFramePresenter` | ✅ | none | 🔲 Edit Mode: `StampImpastoAtUv` pixel delta |
| 1.7 | 5M+ particle capacity | `HarmonicQualityPresets.Cinematic` | ⚠️ alloc only | `HarmonicQualityPresetsTests` | 🔲 `[Category("Stress")]` 1M/5M frame budget |

---

## §2 Memory topology — item checklist

| # | Spec struct / buffer | File | Size | Status | Test | Test TODO |
|---|----------------------|------|------|--------|------|-----------|
| 2.1 | `FluidParticle` 32 B | `Domain/Models/FluidParticle.cs` | 32 | ✅ | `StructLayoutTests` | — |
| 2.2 | `QuantizedBakeParticle` 16 B | `Domain/Models/QuantizedBakeParticle.cs` | 16 | ✅ (packed layout) | `StructLayoutTests` | 🔲 Field-level FP16 round-trip vs spec ushort fields |
| 2.3 | `GridKeyPair` 8 B | `Domain/Models/GridKeyPair.cs` | 8 | ✅ | `StructLayoutTests` | — |
| 2.4 | `HashCellGridRange` 8 B | `Domain/Models/HashCellGridRange.cs` | 8 | ✅ | `StructLayoutTests` | — |
| 2.5 | `_InternalFluid_A/B` | `PipelineExecutionController` | — | ✅ | Play Mode append | 🔲 Counter never exceeds capacity |
| 2.6 | `_FallingFluidStream` | `_bufferFalling` | — | ✅ | Play Mode | 🔲 Nozzle exit increases falling count |
| 2.7 | `_DensityWritableCache` | `_bufferDensityCache` | — | ✅ | none | 🔲 Play Mode: density > rest after neighbor pass |
| 2.8 | `_GridKeyValueBuffer` | `_gridKeyValueBuffer` | — | ✅ | `GPUIndirectSortBinderTests` | 🔲 Padding entries sort to tail (0xFFFFFFFF) |
| 2.9 | `_CellStartEndBuffer` | `_cellStartEndBuffer` | — | ✅ | none | 🔲 Play Mode: cell ranges valid after sort |
| 2.10 | `VoxelDragCell` (extension) | `Domain/Models/VoxelDragCell.cs` | 16 | ✅ | `StructLayoutTests` | — |
| 2.11 | `CanvasPaintHit` (extension) | `Domain/Models/CanvasPaintHit.cs` | 16 | ✅ | `CanvasPaintHitTests` | 🔲 Play Mode: hits when particle crosses plane |

---

## §3 GPU pipeline stages — item checklist

| Stage | Spec | Implementation | Status | Test TODO |
|-------|------|----------------|--------|-----------|
| 1 | Grid cleardown | `ClearGridCellsKernel` | ✅ | 🔲 Unit: all cells `-1` after dispatch (mock buffer) |
| 2 | Hash generation | `GenerateGridKeysKernel` | ✅ | 🔲 Play Mode: keys non-zero for spawned particles |
| 3 | Bitonic sort | `BitonicSortStepKernel` loop | ✅ | 🔲 Edit Mode: sort monotonicity on CPU reference array |
| 4 | Cell mapping | `BuildCellRangesKernel` | ✅ | 🔲 Play Mode: neighbor lookup finds self |
| 5 | SPH density | `ExecuteSphDensityPass` | ✅ | 🔲 Play Mode: density field non-uniform in bucket |
| 6 | Integration + compaction | `ExecuteInternalFluidIntegration` | ⚠️ **indexed**, not `ConsumeStructuredBuffer` | 🔲 Play Mode: internal count stable 1000 frames |
| 7* | Falling world (extension) | `FallingFluidWorld.compute` | ✅ | 🔲 Play Mode: world Y integration |
| 8* | Quantize falling (extension) | `QuantizeFallingParticlesKernel` | ✅ | 🔲 Play Mode: `LastFallingQuantizeCount` matches falling buffer |
| 9* | Canvas hits (extension) | `_bufferCanvasHits` | ✅ | 🔲 Play Mode: `LastCanvasHitCount` > 0 when paint hits canvas |

---

## §4 Trap mitigations — item checklist

| Trap | Spec fix | Verified in code | Test | Test TODO |
|------|----------|------------------|------|-----------|
| 4.1 Thread group multiplier | `CalculateGridArgsKernel` | ✅ | `IndirectDispatchMathTests` | 🔲 Play Mode: read back indirect args after CopyCount |
| 4.2 Two-pass SPH | Separate density + integration | ✅ | Play Mode single frame | 🔲 Regression: disable density pass → instability |
| 4.3 Sort isolation | Separate `_GridKeyValueBuffer` | ✅ | none | 🔲 Append counter unchanged after sort |
| 4.4 PCIe / FP16 | `DataCompactionPacker` | ⚠️ | `QuantizedFrameEncoderTests` | 🔲 Measure payload = 16 × N + header; bake 300 frames |

---

## §5 Shader / kernel matrix

| Kernel | Shader file | Bound in controller | Kernel test | Integration test TODO |
|--------|-------------|---------------------|-------------|------------------------|
| `CalculateGridArgsKernel` | `ArgumentUtility.compute` | ✅ | ✅ | 🔲 |
| `ClearGridCellsKernel` | `SpatialHashGridIndirect.compute` | ✅ | ✅ | 🔲 |
| `GenerateGridKeysKernel` | `SpatialHashGridIndirect.compute` | ✅ | ✅ | 🔲 |
| `BitonicSortStepKernel` | `SpatialHashGridIndirect.compute` | ✅ | ✅ | 🔲 |
| `BuildCellRangesKernel` | `SpatialHashGridIndirect.compute` | ✅ | ✅ | 🔲 |
| `ExecuteSphDensityPass` | `StreamCompactionPingPong.compute` | ✅ | ✅ | 🔲 |
| `ExecuteInternalFluidIntegration` | `StreamCompactionPingPong.compute` | ✅ | ✅ | 🔲 consume pattern deviation |
| `QuantizeFallingParticlesKernel` | `DataCompactionPacker.compute` | ✅ | ✅ | 🔲 |
| `ExecuteFallingFluidIntegration` | `FallingFluidWorld.compute` | ✅ | ✅ | 🔲 |
| `ClearDragGridKernel` | `EulerianDragGrid.compute` | ✅ | ✅ | 🔲 |
| `ScatterParticleToGridKernel` | `EulerianDragGrid.compute` | ✅ | ✅ | 🔲 |
| `ApplyDragFromGridKernel` | `EulerianDragGrid.compute` | ✅ | ✅ | 🔲 |

---

## §6 Orchestrator (`PipelineExecutionController`) — API test TODO

| Public API | Covered by test | TODO |
|------------|-----------------|------|
| `AppendParticles` | Edit + Play buffer tests | 🔲 Capacity clamp at max |
| `GetActiveParticleCount` | Play Mode | 🔲 Matches append count |
| `ClearAllParticles` | Play Mode | 🔲 Returns 0 after clear |
| `ExecutePipelineFrame` | Play Mode | 🔲 No exception at 0 particles |
| `EnableExternalIngestion` | none | 🔲 Disables seed particles |
| `SetSimulationActive` | none | 🔲 Skips GPU when false |
| `ApplyQualityTier` | presets unit test | 🔲 Play Mode: reallocate buffers |
| `SetSimulationMode` | none | 🔲 BakeRecord skips live integration |
| `TryGetCanvasHitBuffer` | none | 🔲 Play Mode after canvas hit |
| `ConfigureAndInitialize` | Play Mode factory | — |

---

## §7 Blueprint files — missing / partial

| Spec file | Present | Notes |
|-----------|---------|-------|
| `Rk4SystemSolver.cs` | ✅ | Generic RK4 |
| `HalfPrecisionCompressor.cs` | ✅ | |
| `GPUIndirectSortBinder.cs` | ✅ | |
| `FluidParticle.cs` | ✅ | |
| `QuantizedBakeParticle.cs` | ✅ | Layout differs (uint2 packs) |
| `HashCellGridRange.cs` | ✅ | |
| `GridKeyPair.cs` | ✅ | |
| `SphFluidSolverCore.cs` | ✅ | |
| `LocalSpaceProcessor.cs` | ✅ | |
| `WorldSpaceProcessor.cs` | ✅ | |
| `CompressedDiskWriter.cs` | ✅ | |
| All 4 core compute shaders | ✅ | Renamed physics shader |
| `ImpastoCanvasDisplace.shader` | ✅ | |
| `PipelineExecutionController.cs` | ✅ | Renamed from spec |
| `PingPongCounterManager.cs` | ✅ | |
| `SlidingWindowDiskQueue.cs` | ✅ | |
| `HighScaleFramePresenter.cs` | ✅ | |

**Not in spec but implemented:** `FallingFluidWorld.compute`, `EulerianDragGrid.compute`, `HarmonicCanvasHitBridge`, quality presets, simulation modes, debug renderer, bake playback driver.

**Spec deviation backlog (implementation, not just tests):**

- [ ] **P0** — Decide: implement literal `ConsumeStructuredBuffer` or document indexed+append as approved deviation
- [ ] **P1** — Eulerian `AdvectDragGridKernel` + decay
- [ ] **P1** — `HarmonicBakePlaybackDriver` decode frames → GPU or impasto replay
- [ ] **P2** — Burst `PendulumRk4Job` (IJob/BurstCompile) wrapping scalar integrator
- [ ] **P2** — CI: GitHub Action `-runTests -testPlatform editmode`
- [ ] **P3** — XR grab / VR UI hooks on `SimulationManager`

---

## Recommended test implementation order

### Sprint A — Close spec-critical gaps (Edit Mode)

1. 🔲 `IndirectArgsPlayModeTests` — CopyCount → slot 3 → groups = ceil(n/64)
2. 🔲 `QuantizedBakeRoundTripTests` — GPU quantize → CPU decode → max error < ε
3. 🔲 `ArchitectureManifest_CompletenessTests` — every kernel in manifest exists on disk
4. 🔲 `FluidParticleFactoryTests` — world→local transform matches bucket matrix

### Sprint B — Pipeline stability (Play Mode)

5. 🔲 `Pipeline_Stability_1000Frames` — internal count bounded, no NaN
6. 🔲 `Pipeline_NozzleExit_FallingBuffer` — falling count increases after swing
7. 🔲 `Pipeline_CanvasHits_Readback` — `LastCanvasHitCount` > 0
8. 🔲 `Pipeline_EulerianDrag_Toggle` — trajectory diff with drag on/off

### Sprint C — Scale & bake (manual / Stress category)

9. 🔲 `[Category("Stress")] Pipeline_100k_30Frames` — already stubbed; add FPS assert
10. 🔲 `[Category("Stress")] Pipeline_1M_10Frames` — dev GPU only
11. 🔲 `[Category("Stress")] BakeRecord_300Frames` — no main-thread stall > 2 ms avg
12. 🔲 `[Category("Stress")] VRAM_NoGrowth_10Min` — counter stable

### Sprint D — Visual / integration

13. 🔲 `ImpastoPresenter_StampImpastoAtUv` — height map pixel increases
14. 🔲 `CanvasController_OnParticleHit` — albedo changes at UV
15. 🔲 `SimulationManager_Reset_ClearsCanvasAndHeight` — Play Mode

---

## Manual QA checklist (run every release)

- [ ] Open `MainSimulation`, Play, bucket swings, paint on canvas
- [ ] Impasto height visible on canvas shader
- [ ] Reset (R) clears canvas + height map
- [ ] Quality keys 1–4 change `MaxCapacity` in inspector at runtime
- [ ] Bake mode (B) writes files under `persistentDataPath/HarmonicBakeFrames`
- [ ] CPU fallback when `HarmonicGpuCapabilityGuard` fails
- [ ] Test Runner Edit Mode: all green
- [ ] Test Runner Play Mode: pipeline tests green
- [ ] Optional Stress category on target GPU

---

## Tracking

Update [`architecture-coverage.md`](architecture-coverage.md) when a row moves from ⚠️/❌ to ✅.  
Update [`ArchitectureManifest.cs`](../Assets/AdvancedHarmonicEngine_V3/Core/Validation/ArchitectureManifest.cs) `FeatureMatrix` in the same PR as the test that proves the feature.
