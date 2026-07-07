# 14 — Invariants & parity

Rules that must hold for correct engine behavior, and CPU↔GPU synchronization requirements.

---

## Simulation invariants

### 1. Particle conservation

```
SpawnedTotal == ActiveParticleCount + SettledTotal
```

Every frame after readback. Settled particles are removed from live SOA but counted in `SettledTotal`.

### 2. Escape latch authority

- **Only** `ClassifyKernel` sets `hasEscaped`
- Once set, particle is permanently Outside
- Escaped particles skip bucket collision in Finalize
- Escaped particles are not re-classified

### 3. Single zone per frame

Priority: **hole zones (Level 1) > top band (Level 2) > none**

At most one hole claims a particle (nearest within d2).

### 4. Classification vs containment

- **Inside flag** — classification footprint, drives carry/zones
- **ComputeContainInside** — collision decision: frame-start Inside flag OR current footprint OR frame-start reference footprint (with `faceEps = 1e-4` on radius)
- A particle may be Outside flag but still contained via reference footprint (float noise at `r = R`)
- **ShouldContainInside** alone is insufficient at call sites — use `ComputeContainInside`

### 5. Deterministic compaction

- Removed particles: sort key `0xFFFFFFFF`
- Survivors: stable index order preserved by radix sort
- Gather copies prefix in sorted order

### 6. Deterministic canvas

- Splat events tagged with source index (`pad0`)
- `SplatApplyKernel` sorts by source index before apply
- Single-thread dispatch (1×1×1)

### 7. Jacobi PBF

- `SolveDeltaKernel` writes `_Deltas` — never races in-place on `_Predicted`
- `ApplyDeltaKernel` applies clamped delta separately

### 8. ApplyDelta clamp

```
maxCorrection = 0.2 × smoothingRadius
```

Prevents single-frame detonation from deep spawn compression.

### 9. Impact speed source

Impact absorption uses **pre-clamp incoming velocity** (`_Block1.y`), not position delta — predict-stage canvas clamp would hide approach speed otherwise.

### 10. UAV budget

Each manifest pass ≤ 8 RW buffers. Validated by `V4ManifestValidation` at init.

---

## CPU ↔ GPU parity pairs

**Change both sides in the same commit.** Run parity tests.

| CPU (C#) | GPU (HLSL / compute) |
|----------|----------------------|
| `V4ParticleFlags` | `V4Common.hlsl` flag macros |
| `V4BucketGeometry` | `V4BucketZones.hlsl` geometry + collision |
| `V4ZoneMath` | `V4BucketZones.hlsl` zone functions |
| `V4SpatialHashMath` | `V4Common.hlsl` hash/cell functions |
| `V4CanvasSplatMath` | `V4CanvasSplat.compute` SplatApplyKernel |
| `V4GpuProfileData` | `V4Profiles.hlsl` |
| `V4BakedHole` / `V4HoleDef` | `V4BucketZones.hlsl` `V4Hole` |

### Parity test rules

- Skip samples within `1e-4` of geometric boundaries (float noise)
- Test collision for both `containInside = true` and `false`
- Parity tests pass `contactVelLocal = 0` (stationary bucket); moving-frame reflection is a call-site input — both CPU and GPU must use `ReflectAgainstNormalInMovingFrame` inside `ResolveCollision`
- Color pack/unpack must be lossless for RGBA8
- Use `V4TestKernels.compute` parity kernels in PlayMode

---

## Spawn determinism

- Cubic lattice fill — **no RNG**
- Overlap culling uses spatial hash of occupied cells
- Same config → identical initial positions (golden frame tests)

---

## Common failure modes

| Symptom | Likely break |
|---------|--------------|
| Particles pass through walls | `containInside` computed from geometry only (must use `ComputeContainInside`); missing face-contact band at `r = R`; wall-band floor gap |
| Escape particles re-enter bucket | Classify not sole escape authority |
| Nondeterministic canvas | Splat order not sorted |
| GPU/CPU test mismatch | HLSL edited without C# reference update |
| Init failure | Manifest UAV budget exceeded |

---

## Related

- [Testing](13-testing.md)
- [Bucket & zones](05-bucket-and-zones.md)
