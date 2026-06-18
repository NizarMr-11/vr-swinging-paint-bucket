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
