# 9 — GPU shaders reference

Production shaders live in `Runtime/Resources/HarmonicEngineV4/`. Loaded by name via `V4ShaderLibrary`.

---

## HLSL includes

| File | Contents |
|------|----------|
| `Include/V4Common.hlsl` | Flag macros, `HashCellGridRange`, Poly6/Spiky kernels, RGBA8 pack/unpack, counter slot defines |
| `Include/V4BucketZones.hlsl` | `V4Hole`, bucket geometry, collision, zone classification, zone force direction |
| `Include/V4Profiles.hlsl` | `V4GpuProfile` struct (14 floats) |
| `Include/V4NeighborQuery.hlsl` | `V4_FOREACH_NEIGHBOR_BEGIN/END` — 27-cell stencil |

---

## V4Classification.compute

| Kernel | Groups | RW | RO |
|--------|--------|----|----|
| `ClearFrameCountersKernel` | 1 | `_Counters` | — |
| `ClassifyKernel` | ⌈N/64⌉ | `_Flags`, `_Counters` | `_Block0`, `_Holes` |

Uniforms: bucket transforms, inner radius, height, wall thickness, top band height, hole count.

---

## V4ExternalForces.compute

| Kernel | RW | RO |
|--------|----|----|
| `ExternalForcesKernel` | `_Block1` | `_Block0`, `_Flags`, `_Holes`, `_Profiles`, `_Counters` |

Extra uniforms: `_Gravity`, `_DeltaTime`, `_CarryRate`, bucket linear/angular velocity, origin, `_TotalExpectedLoss`, `_DownwardScale`.

---

## V4PbfSolver.compute

| Kernel | RW | RO |
|--------|----|----|
| `PredictKernel` | `_Predicted` | `_Block0`, `_Block1`, `_Flags` |
| `DensityKernel` | `_Predicted`, `_Densities`, `_BlendedColors` | `_PackedColorsRead`, grid buffers |
| `LambdaKernel` | `_Predicted`, `_Densities`, `_Lambdas` | `_Flags`, `_Profiles`, grid |
| `SolveDeltaKernel` | `_Predicted`, `_Lambdas`, `_Deltas` | `_Flags`, `_Profiles`, grid |
| `ApplyDeltaKernel` | `_Predicted`, `_Deltas`, `_BlendedColors`, `_PackedColors` | `_Flags`, `_Profiles` |
| `FinalizeKernel` | `_WriteBlock0/1`, `_WritePackedColors`, `_WriteFlags`, `_CompactionPairs`, `_Counters`, `_SplatEvents` | `_Block0`, `_Block1`, `_Flags`, `_PackedColorsRead`, `_PredictedRead`, `_Profiles`, `_Holes`, grid |
| `GatherKernel` | `_GatherBlock0/1`, `_GatherColors`, `_GatherFlags` | `_SortedPairs`, `_StageBlock0/1`, `_StageColors`, `_StageFlags` |

Key uniforms: `_DeltaTime`, `_SmoothingRadius`, `_CellSize`, `_GridResolution`, `_PbfEpsilon`, canvas plane/min/size, `_SettleDistance`, `_MaxSplatEvents`.

---

## V4SpatialHash.compute

| Kernel | Purpose |
|--------|---------|
| `ClearGridCellsKernel` | Reset cell ranges |
| `GenerateGridKeysKernel` | Hash positions → (hash, index) pairs |
| `BuildCellRangesKernel` | Scan sorted keys for start/end |

Uses `_Block0` or `_Predicted` depending on build call site (PBF uses predicted).

---

## V4RadixSort.compute

6-bit LSD radix, 6 passes × 64 buckets:

`ExtractGridKeysKernel` → (`ClearHistogram` → `Histogram` → `Scan` → `Scatter`) × 6 → `PackGridKeysKernel`

Also used for **compaction pair sort** via `V4GpuRadixSort` wrapper.

---

## V4CanvasSplat.compute

| Kernel | Groups | Purpose |
|--------|--------|---------|
| `ClearCanvasKernel` | ⌈cells/64⌉ | Init grid to base color |
| `SplatApplyKernel` | **1×1×1** | Apply sorted splat events |
| `CanvasToTextureKernel` | 8×8 | Grid → `RenderTexture` |

---

## Surface shaders

| File | Passes | Purpose |
|------|--------|---------|
| `V4DebugPoints.shader` | 1 | Billboard quads per particle |
| `V4SSFluidRender.shader` | 3 | Depth/thickness MRT → blur → composite |

---

## Test shaders

`Tests/PlayMode/Resources/HarmonicEngineV4Tests/V4TestKernels.compute`

Parity kernels: `GeometryParityKernel`, `CollisionParityKernel`, `FlagsParityKernel`, `ColorPackParityKernel`.

---

## UAV budget rule

Each manifest pass lists at most **8** simultaneous RW buffers. `V4ManifestValidation` checks this at init (D3D11 limit).

---

## Related

- [Frame pipeline](03-frame-pipeline.md)
- [Invariants & parity](14-invariants-and-parity.md)
