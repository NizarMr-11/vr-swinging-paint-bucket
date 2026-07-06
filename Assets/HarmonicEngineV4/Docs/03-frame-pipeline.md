# 3 — Frame pipeline

Every simulation frame is driven by `V4PipelineRoot.Step(float deltaTime)`. This document lists the exact order and what each stage reads/writes.

---

## Initialization (once)

Called from `Start()` or manually via `Initialize()`:

```
1. Load compute shaders (V4ShaderLibrary)
2. ParticleRadius = f(globalDensity); SmoothingRadius = 4 × ParticleRadius
3. BakeBucket()     → _bakedHoles[], upload _Holes buffer
4. BakeCanvas()     → CanvasGridSize, CanvasCellSize
5. BakeSpawnZones() → list of SpawnedParticle (deterministic lattice)
6. CreateBuffers()  → SOA, scratch, canvas grid, counters, sort temps
7. UploadProfiles() → _Profiles buffer from V4LiquidProfile table
8. UploadParticles()→ initial SOA read set
9. BuildManifestAndExecutor()
10. ClearCanvas + BlitCanvasToTexture (empty canvas)
11. V4EmaLossModel init
```

If `bucket` or `canvas` is null, initialization aborts and the component disables itself.

---

## Per-frame sequence

```
Step(deltaTime):
│
├─ 1. bucket.SampleKinematics(dt)     → LinearVelocity, AngularVelocity
├─ 2. SetFrameUniforms(dt)             → matrices, bucket params, gravity, canvas plane
│
├─ 3. ClearFrameCounters              [Classification shader, 1 group]
├─ 4. Classify                        [ActiveParticleCount threads]
├─ 5. ExternalForces                  [ActiveParticleCount threads]
├─ 6. Predict                         [ActiveParticleCount threads]
│
├─ 7. Spatial hash build on _Predicted:
│      ClearGridCells → GenerateGridKeys → RadixSort → BuildCellRanges
│
├─ 8. PBF loop (pbfIterations times, default 2):
│      Density → Lambda → SolveDelta → ApplyDelta
│
├─ 9. Finalize                        [stage write SOA + compaction pairs + splat events]
├─ 10. Compaction radix sort          [_CompactionPairs by sort key]
├─ 11. Gather                         [copy survivors → read SOA]
├─ 12. SplatApply                     [1×1×1 — sequential canvas writes]
├─ 13. BlitCanvasToTexture            [CanvasToTextureKernel, outside executor]
│
├─ 14. ReadBackCounters()             → update live/escaped/settled, EMA
└─ 15. FrameIndex++; FrameCompleted event
```

`Update()` calls `Step(min(Time.deltaTime, maxDeltaTime))` when `autoRun` is true.

---

## Pass details

### Classify

**Shader:** `V4Classification.compute`  
**Writes:** `_Flags` (inside, zone, escape latch), `_Counters` (top band, escaped, per-hole eject)  
**Reads:** `_Block0` (positions)

Only kernel allowed to set `hasEscaped`. Escaped particles skip re-classification.

### ExternalForces

**Shader:** `V4ExternalForces.compute`  
**Writes:** `_Block1` (velocity)  
**Reads:** `_Block0`, `_Flags`, `_Holes`, `_Profiles`, `_Counters`

Applies gravity (all), carry (inside), zone forces, top-band push-down.

### Predict

**Shader:** `V4PbfSolver.compute` → `PredictKernel`  
**Writes:** `_Predicted`  
**Reads:** `_Block0`, `_Block1`, `_Flags`

Semi-implicit Euler + bucket position clamp + canvas floor clamp.

### Spatial hash

**Shader:** `V4SpatialHash.compute` + `V4RadixSort.compute`  
Cell size = smoothing radius. 27-cell neighbor stencil in PBF passes.

### PBF iteration block

| Kernel | Output |
|--------|--------|
| `DensityKernel` | `_Densities`, `_BlendedColors` (neighbor-weighted color sum) |
| `LambdaKernel` | `_Lambdas` from density constraint + CFM epsilon |
| `SolveDeltaKernel` | `_Deltas` (Jacobi — no in-place position races) |
| `ApplyDeltaKernel` | Updated `_Predicted`, optional color diffusion on `_PackedColors` |

### Finalize

**Writes:** `_WriteBlock0/1`, `_WriteFlags`, `_WritePackedColors`, `_CompactionPairs`, `_SplatEvents`, `_Counters`

- XSPH viscosity + cohesion on predicted grid
- Full bucket collision (position + velocity)
- Canvas plane bounce
- Impact splat (fast) or slow settle → mark `removed`, emit splat event
- Compaction pair: `(removed ? 0xFFFFFFFF : liveIndex, sourceIndex)`

### Gather

Copies sorted survivor prefix from write staging into canonical **read** SOA. Removed slots are not copied. `ActiveParticleCount` decreases in `ReadBackCounters` by settled count this frame.

---

## Buffer ping-pong note

`V4ParticleSoa` has read/write buffer pairs. The frame path is:

1. **Read** set → input to Classify through Finalize read bindings
2. **Write** set → Finalize output staging
3. **Gather** → writes back into **read** set

There is no `Swap()` call in `Step()`; Gather explicitly repopulates the read buffers.

---

## Manual stepping

Tests and tools set `autoRun = false` and call:

```csharp
pipeline.Initialize();
pipeline.Step(1f / 60f);
```

Lab keyboard controller moves the bucket transform; kinematics are sampled at the start of each `Step`.

---

## Related

- [PBF solver](06-pbf-solver.md)
- [External forces](07-external-forces.md)
- [GPU shaders](09-gpu-shaders.md) — full kernel/buffer tables
