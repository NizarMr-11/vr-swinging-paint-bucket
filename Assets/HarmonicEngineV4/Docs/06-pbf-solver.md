# 6 — PBF solver

Position-Based Fluids implementation in `V4PbfSolver.compute`.

---

## Overview

V4 uses **PBF** (Macklin & Mueller) rather than force-based SPH integration:

1. Predict positions from velocity
2. Build spatial hash on predicted positions
3. Iterate: compute density constraint → lambda → position deltas
4. Finalize: derive velocity, viscosity, cohesion, collisions, lifecycle

Default **`pbfIterations = 2`**. Increase for stiffer fluid at higher cost.

---

## Key parameters

| Parameter | Source | Typical value |
|-----------|--------|---------------|
| `ParticleRadius` | `V4SpawnMath.ParticleRadiusFromDensity(globalDensity)` | ~0.001–0.02 m |
| `SmoothingRadius h` | `4 × ParticleRadius` | Kernel support radius |
| `restDensity ρ₀` | Lattice calibration × profile multiplier | ~1000 baseline |
| `pbfEpsilon` | `V4PipelineRoot.pbfEpsilon` | CFM relaxation (default 50) |
| `pbfKCorr` / `pbfNCorr` / `pbfDeltaQScale` | `V4LiquidProfile` | s_corr artificial pressure (0.1, 4, 0.2×h) |
| Cell size | `= SmoothingRadius` | Spatial hash cell |

**Rest density calibration:** At init, `V4PipelineRoot` computes Poly6 sum over the spawn lattice at spacing `2 × ParticleRadius` and scales by `profile.restDensity / 1000`. Constant per particle for the whole run — not adapted at boundaries.

---

## Kernel stages

### PredictKernel

```
predicted = pos + vel × dt
predicted = V4ClampPositionToBucket(predicted, flags)   // geometry containment, vel untouched
predicted.y = max(predicted.y, canvasPlaneY + radius)   // world canvas floor
```

### DensityKernel

- Poly6 kernel self + neighbors within `h`
- **Boundary ghost density** (contained particles only): mirror-particle contributions across bucket floor (`y=0`) and inner cylindrical wall, computed in bucket-local space with world-space kernel distances
- Writes `_Densities[i]` and `_BlendedColors[i]` (weighted neighbor color sum)

### LambdaKernel

Constraint: `C = ρ/ρ₀ - 1`  
If `C ≤ 0`: lambda = 0 (intentional — no pull toward vacuum in open space)

Otherwise:

```
λ = -C / (Σ|∇W|² + ε)
```

Neighbor Spiky gradients plus **ghost mirror gradients** in the denominator only (immovable boundary resistance, no reactive λ for ghosts).

### SolveDeltaKernel

Jacobi-style accumulation with **s_corr** artificial pressure (Macklin & Müller §3.4):

```
scorr = -kCorr × (W(r,h) / W(δQ·h, h))^nCorr
Δp_i += Σ_j (λ_i + λ_j + scorr) ∇W_ij / ρ₀
```

Profile fields: `pbfKCorr`, `pbfNCorr`, `pbfDeltaQScale` (reference distance = scale × h).

### ApplyDeltaKernel

```
Δp = clamp(|Δp|, max = 0.2 × h)    // prevents detonation from deep compression
predicted += Δp
predicted = V4ClampPositionToBucket(predicted)
predicted.y = max(predicted.y, canvasPlaneY + radius)
```

Color diffusion (once per iteration):

```
color = lerp(ownColor, neighborAverage, colorDiffusionRate)
```

### FinalizeKernel

1. **Velocity** from position delta: `vel = (predicted - oldPos) / dt`
2. **XSPH viscosity** on predicted grid
3. **Cohesion** (surface tension) along neighbor gradients
4. **Bucket collision** (full pos + vel, profile friction/restitution)
5. **Canvas plane** bounce for survivors
6. **Impact splat** or **slow settle** → remove particle, emit splat event
7. Write staging buffers + compaction pair

---

## Spatial neighborhood

`V4NeighborQuery.hlsl` macro iterates 27 cells (3×3×3 stencil) using `_CellStartEndBuffer` and sorted `_GridKeyValueBuffer`.

Built fresh each frame on `_Predicted` positions before the PBF loop.

---

## Compaction

After Finalize:

1. Radix sort `_CompactionPairs` — removed particles get key `0xFFFFFFFF`
2. `GatherKernel` copies survivor prefix to read SOA in stable order
3. `ActiveParticleCount -= settledThisFrame`

Deterministic ordering preserves reproducibility for golden-frame tests.

---

## Tuning guide

| Symptom | Knob |
|---------|------|
| Fluid too compressible / explosive | Increase `pbfIterations`, decrease spawn density |
| Particles don't stack on floor/wall | Raise `pbfKCorr` slightly; verify ghost density active |
| Particles tunnel walls | Containment geometry (not PBF); see [Bucket & zones](05-bucket-and-zones.md) |
| Too watery | Increase `viscosity`, `cohesion` on profile |
| Too stiff / jittery | Decrease `pbfIterations`, increase `pbfEpsilon` |
| Color smears too fast | Lower `colorDiffusionRate` |
| Detonation on spawn | ApplyDelta clamp (0.2h) — if still bad, reduce initial overlap in spawn bake |

---

## Related

- [Frame pipeline](03-frame-pipeline.md)
- [GPU shaders](09-gpu-shaders.md)
- Source: `Runtime/Resources/HarmonicEngineV4/V4PbfSolver.compute`
