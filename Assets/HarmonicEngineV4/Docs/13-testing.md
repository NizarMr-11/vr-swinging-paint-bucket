# 13 — Testing

HarmonicEngineV4 uses a **three-layer** test strategy. All tests live under `Assets/HarmonicEngineV4/Tests/`.

---

## Layer overview

| Layer | Mode | GPU | Purpose |
|-------|------|-----|---------|
| **1 — CPU reference** | EditMode | No* | Pure math: geometry, zones, bake, flags, splat, EMA, manifest |
| **2 — GPU/CPU parity** | PlayMode | Yes | HLSL matches C# on thousands of seeded samples |
| **3 — Integration** | PlayMode | Yes | Full pipeline invariants, determinism, containment, canvas |

\*Some EditMode buffer tests skip if compute unavailable.

---

## Assemblies

- `HarmonicEngineV4.Tests.EditMode` — Editor only
- `HarmonicEngineV4.Tests.PlayMode` — Player + Editor

Run via Unity Test Runner or Unity MCP `run_tests` tool.

---

## EditMode tests

| File | Covers |
|------|--------|
| `V4BakeMathTests` | Spawn spacing, lattice, canvas grid |
| `V4BucketBakeTests` | Hole rings, spacing validation |
| `V4BucketGeometryTests` | Inside, shell, collision, ShouldContainInside, ComputeContainInside, face-contact band, beyond-outer shell clamp |
| `V4ZoneMathTests` | Classification priority, force direction |
| `V4ParticleFlagsTests` | Bit packing |
| `V4CanvasSplatMathTests` | Cell weights, depth blend |
| `V4EmaLossModelTests` | EMA step |
| `V4BufferRegistryTests` | Registry lifecycle |
| `V4ManifestValidationTests` | UAV budget, duplicate pass IDs |
| `V4DeviceCapsTests` | Tier detection |
| `V4LogTests` / `V4ChannelFileSinkTests` / `V4RunLogPathsTests` | Logging |

Fast — run on every commit without GPU.

---

## PlayMode tests

| File | Covers |
|------|--------|
| `V4TestRig` | Headless pipeline factory for all integration tests |
| `V4PipelineIntegrationTests` | Spawn, conservation, determinism, escape permanence |
| `V4GoldenFrameTests` | 100-frame choreography baselines |
| `V4GpuCpuParityTests` | Geometry, collision, flags, color pack |
| `V4ContainmentTests` | Wall/floor leaks under motion, canvas impact |
| `V4CanvasGpuTests` | GPU splat vs CPU reference |
| `V4SpatialHashTests` | Grid build, neighbor coverage |
| `V4RadixSortTests` | Sort stability |
| `V4RendererSmokeTests` | Renderer init smoke |
| `V4RunRecorderTests` | JSONL recording |
| `V4GhostWeightSweepTests` | `boundaryGhostWeight` sweep + sealed-column settled assertion |
| `V4StackingDepthInstrumentationTests` | Per-layer density vs stack depth, iteration sweep |

### Investigation & instrumentation suites (report-only)

These replicate lab scenes or tall columns, log numeric diagnostics to the Unity console, and write `*.txt` reports under `Tests/PlayMode/Results/`. Most use `Assert.Pass(...)` only — they do **not** assert simulation correctness unless noted.

| File | Test methods | Report output(s) | What it measures |
|------|--------------|------------------|------------------|
| **`V4Lab2EscapeStatisticsTests`** | | | Lab2 exact config (3 spawn zones, 2 holes, density 1e6, 2 PBF iters, bucket at rest) |
| | `Investigate_Lab2RestEscapeStatistics` | `lab2_escape_statistics.txt` | Per-frame inside/outside/escaped/settled counts; Inside→not-Inside exit events by path (rim / wall / floor / hole); timing histogram |
| | `Investigate_Lab2OutsideLiveAnatomy` | `lab2_outside_live_anatomy.txt` | Live particles by flag + physical band (cavity, wall band, sub-floor, over-rim, outside far); wall/floor flicker detail |
| | `Investigate_Lab2FloorCrossingForensics` | `lab2_floor_crossing_forensics.txt` | Floor crossings: latched (hole) vs unlatched (illegitimate); dist-to-hole; final sub-floor population histogram |
| | `Investigate_Lab2LeakChannelGeometry` | `lab2_leak_channel_geometry.txt` | Where unlatched sub-floor particles sit in r vs hole distance during spawn collapse (frames 40–120) |
| | `Investigate_Lab2CanvasPuddleSettleProbe` | `lab2_canvas_puddle_settle_probe.txt` | Canvas-layer live particles: speed vs `settleEpsilon`, near-plane / on-rect settle blockers |
| **`V4Lab2LeakProbeTests`** | | | Identity-stable forensics (settle disabled so compaction never shifts indices) |
| | `Investigate_Lab2FirstCrossingForensics_NoRemovals` | `lab2_leak_probe_first_crossings.txt` | First per-particle wall/floor crossing: prev-frame flag, r, radial vel, hop distance |
| **`V4CollisionEnergyInvestigationTests`** | | | Lateral shake KE and collision reflection |
| | `Investigate_ShakeKineticEnergy_MovingFrameFix` | `collision_energy_shake_post_fix.txt` | KE trend, first rim crossing, settled count (moving-frame fix regression) |
| | `Investigate_Frame3EarlyLeak` | `collision_frame3_early_leak.txt` | SampleKinematics frames 0–9; frame-3 rim crosser vel.y traces |
| | `Investigate_ShakeKineticEnergy_DefaultAndZeroRestitution` | `collision_energy_shake.txt` | KE with default vs zero `surfaceRestitution` |
| | `Investigate_ShakeMotion_IsSmoothSinusoidal` | `collision_shake_motion_profile.txt` | Bucket velocity smoothness vs analytic sin derivative |
| **`V4CohesionViscosityInvestigationTests`** | | | FinalizeKernel cohesion / XSPH probes |
| | `Investigate_FormulasAndRestTopLayer` | `cohesion_viscosity_rest_top_layer.txt` | Rest top-band vel.y; formula normalization at low neighbor count |
| | `Investigate_Frame3RimCrosserFinalizeTerms` | `cohesion_viscosity_frame3_crossers.txt` | Per-frame density, neighbor count, cohesion/XSPH deltas for tracked rim crossers |
| | `Investigate_ShakeIsolation_CohesionAndViscosity` | `cohesion_viscosity_shake_isolation.txt` | Cohesion=0 and viscosity=0 isolation vs baseline shake leak |
| **`V4ApplyDeltaClampInvestigationTests`** | | | ApplyDelta max-correction clamp |
| | `Investigate_ClampFrequency_RestAndShake` | `clamp_frequency_rest_shake.txt` | How often clamp fires; rim launches vs clamp scale |
| | `Investigate_TrackedFloorParticles_AndRimLaunches` | `clamp_tracked_particles_rim.txt` | Floor-column particle deltas and rim exit correlation |
| | `Investigate_MaxCorrectionSweep` | `clamp_maxcorrection_sweep.txt` | Sweep `_DebugMaxCorrectionScale` vs leak/settle |
| **`V4ScorrInvestigationTests`** | | | Macklin s_corr artificial pressure |
| | `Investigate_ScorrDistribution_EarlyFrames` | `scorr_distribution_early.txt` | s_corr magnitude distribution in first frames |
| | `Investigate_KCorrSweep` | `scorr_kcorr_sweep.txt` | `pbfKCorr` sweep vs rim leak |
| | `Investigate_ScorrCapAndIterationLevers` | `scorr_cap_iteration_levers.txt` | Debug ratio cap, per-iter apply, saturate pow |
| **`V4StackingLeakInvestigationTests`** | | | Tall sealed column rim leak |
| | `Investigate_LeakPath_Timeline` | `stacking_leak_timeline.txt` | When/where particles leave cavity over time |
| | `Investigate_GhostScorr_Isolation` | `stacking_leak_isolation.txt` | ghostWeight / s_corr isolation |
| **`V4StackingDepthInstrumentationTests`** | | | Stacking density vs depth (particle spacing / compression) |
| | `Instrument_StackingDepth_AtDefaultIterations` | `stacking_depth_default_iters.txt` | Per-y-layer avg Poly6 density at default `pbfIterations` |
| | `Instrument_TallColumn_IterationSweep` | `stacking_depth_iteration_sweep.txt` | Layer density vs PBF iteration count |
| **`V4GhostWeightSweepTests`** | | | Boundary mirror ghost weight |
| | `FinalIterScorr_SealedTallColumn_SettledStaysNearZero` | — | **Assertion:** settled count ~0 with final-iter-only s_corr |
| | `Sweep_GhostWeight_ReportTable` | `ghost_weight_sweep.txt` | ghostWeight sweep table (settled, rim, max inside Y) |
| **`V4WallEscapeFirmHoldInvestigationTests`** | | | Firm +Z hold (Lab2 config, 4 PBF iters) — wall vs rim vs latch anatomy |
| | `Investigate_FirmDirectionalHold_WallEscapeForensics` | `wall_escape_firm_hold.txt` | Per-frame `beyondOuter`, `overRim`, `wallBandOutside`, `falseLatch`, `cif`; **asserts** `beyondOuter < 50` at end |
| | `Investigate_BeyondOuterCrossingDeltas` | `beyond_outer_crossing_forensics.txt` | First crossing into beyond-outer shell: hop distance vs `maxCorrection` |

Report convention: `Assets/HarmonicEngineV4/Tests/PlayMode/Results/*.txt` — regenerate by re-running the test. Files may be gitignored locally.

### V4TestRig usage

```csharp
using var rig = V4TestRig.Create(new V4TestRig.Config {
    BucketRadius = 0.3f,
    GlobalDensity = 120000f,
    RestrictSpawnToBucketCavity = false,  // for free-fall spawns
});
rig.Step(120, 1f/60f);
Assert.AreEqual(0, rig.Root.EscapedTotal);
```

Always set `autoRun = false` (rig does this). Call `Step` explicitly.

---

## Key invariants tested

| Invariant | Test |
|-----------|------|
| `Spawned == live + settled` | `V4PipelineIntegrationTests` |
| Escape latch permanent | Integration + containment |
| Deterministic replay | `V4GoldenFrameTests` |
| CPU ↔ GPU geometry | `V4GpuCpuParityTests` |
| No floor/wall tunnel | `V4ContainmentTests`, `OutsideParticleBeyondOuterShell_IsClampedToOuterFace` (`V4BucketGeometryTests`) |
| Beyond-outer shell drift | `V4WallEscapeFirmHoldInvestigationTests` |
| Canvas splat math | `V4CanvasGpuTests` |

---

## Running tests

### Unity Test Runner

1. **Window → General → Test Runner**
2. EditMode or PlayMode tab
3. Filter assembly `HarmonicEngineV4`

### Unity MCP

```
run_tests(mode=PlayMode, assembly_names=["HarmonicEngineV4.Tests.PlayMode"])
```

PlayMode needs `init_timeout` ≥ 120000 ms for domain reload.

### CI

Project may add V4 EditMode to CI; PlayMode typically manual (GPU required).

---

## Adding tests

1. **Geometry/zone change** → update C# + HLSL, extend `V4BucketGeometryTests` + parity tests
2. **New GPU kernel** → add manifest entry, manifest validation test, integration smoke
3. **New behavior** → prefer `V4TestRig` integration over scene-based tests
4. **Regression** → add focused test to `V4ContainmentTests` or golden frame

See also [`docs/testing-strategy.md`](../../../docs/testing-strategy.md) (project-wide, includes V4 section).

---

## Related

- [Invariants & parity](14-invariants-and-parity.md)
