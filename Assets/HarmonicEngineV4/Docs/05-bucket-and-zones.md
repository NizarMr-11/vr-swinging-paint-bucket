# 5 — Bucket & zones

Bucket geometry, particle classification, wall containment, and hole escape.

---

## Coordinate system

**Bucket-local space** (used by all geometry and zone math):

- **Origin:** center of the inner floor
- **+Y:** up toward the open rim
- **Rim:** `y = height`
- **Inner cavity:** `0 ≤ y ≤ height`, `r ≤ innerRadius` where `r = sqrt(x² + z²)`
- **Solid shell:** wall band `[innerRadius, innerRadius + wallThickness]` below rim, plus floor slab `y ∈ (-wallThickness, 0)`

Transform matrices `_BucketWorldToLocal` / `_BucketLocalToWorld` are uploaded every frame from `V4Bucket.transform`.

---

## Classification vs containment

These are **separate systems**:

| | Classification | Containment collision |
|---|----------------|----------------------|
| **Purpose** | Gameplay state: carry, zones, escape | Physical wall/floor response |
| **Function** | `V4BucketIsInside` | `V4ComputeContainInside` (GPU) / `V4BucketGeometry.ComputeContainInside` (CPU) |
| **Floor rule** | Strict: `y ≥ 0` | Allows brief sub-floor lag (moving bucket) |
| **Deep below floor** | Outside | Not contained (free fall to canvas) |
| **Updated by** | `ClassifyKernel` only | Predict, ApplyDelta, Finalize |

### ComputeContainInside (caller contract)

Containment is **not** geometry-only. Each collision call site passes:

1. **Frame-start Inside flag** — authoritative classification
2. **Current-position footprint** — `V4BucketShouldContainInside(localPos)`
3. **Frame-start reference footprint** — `ShouldContainInside(refLocalPos, R + faceEps)` where `faceEps = 1e-4`

The reference check is load-bearing: solver-loop clamps pin pressurized particles at exactly `r = R`. Classification can flicker them Outside by one float ulp; without the reference footprint, a spawn-collapse jet can hop them past the wall mid-plane in a single step and nearest-face resolution ejects them through the wall.

`ApplyDelta` binds `_Block0` for the frame-start reference; `Finalize` uses `oldP.xyz`.

### ShouldContainInside

```csharp
// CPU: V4BucketGeometry.ShouldContainInside(localPos, innerRadius, height, wallThickness)
// GPU: V4BucketShouldContainInside(...)
```

Returns true when:

- `y ≤ height` (below open rim)
- `y ≥ -floorLag` where `floorLag = max(wallThickness × 4, 0.02)` — brief lag only
- `r ≤ innerRadius` (inner cylinder footprint)

Particles far below the bucket (e.g. canvas fall) are **not** pulled back to the bucket floor.

---

## Classification (`ClassifyKernel`)

Per particle, each frame (unless escaped):

1. Transform world position → bucket-local
2. `inside = V4BucketIsInside(localPos)`
3. Set Inside flag
4. `V4ClassifyZones` — priority resolution:
   - **Level 1:** nearest hole within `d2` → HoleZone0/1/2
   - **Level 2:** if no hole and `y ≥ height - topBandHeight` → TopBand
5. **Hole Zone 0:** set Outside + **permanent escape latch**, increment escaped + per-hole eject counters

Escaped particles: forced Outside, zone None, never re-classified.

---

## Zone force summary

| Zone | Force | Applied in |
|------|-------|------------|
| HoleZone0 | Velocity-target eject along `outwardNormal` up to `zoneStrength0` | ExternalForces (before latch removes inside behavior next frame) |
| HoleZone1/2 | Pull toward hole opening × strength × dt | ExternalForces |
| TopBand | Downward push scaled by EMA loss / top count | ExternalForces |

Holes are **not** geometric cutouts in collision — escape is exclusively via Zone 0 latch + eject force. Solid shell collision treats the bucket as a sealed cylinder with no mesh holes.

---

## Collision resolution

`V4BucketResolveCollision(ref pos, ref vel, …, containInside, contactVelLocal)`

Reflection uses **moving-frame** velocity: subtract bucket contact velocity at the impact point before reflecting, then add it back (`V4ReflectAgainstNormalInMovingFrame`). Contact velocity:

```
contactVelWorld = linearVel + cross(angularVel, worldPos - bucketOrigin)
```

### When `containInside == true`

Used when `V4ComputeContainInside` returns true:

1. If `y < 0` → clamp to floor (`y = 0`), reflect vertical velocity (moving frame)
2. **Face-contact band** (`r ≥ innerRadius - 1e-4`): clamp position to inner wall if `r > R`, reflect inward radial velocity (moving frame). Required because solver-loop clamps pin particles at exactly `r = R` — without the band, outward jet velocity never gets reflected and accumulates until a flag-flicker frame ejects the particle.

Open top (`y > height`): no collision — fluid can slosh over rim.

### When `containInside == false`

Outside-flagged particles near shell use **nearest-face resolution**:

- **Cavity footprint, below floor:** snap to inner floor or exclude below slab (float-noise recovery)
- **Wall band:** radial push to inner or outer face; **also** lift to floor if `y < 0`
- **Beyond outer shell** (`r ≥ R + T`, `y ≤ height`): clamp position to outer face (`r = R + T`), reflect outward radial velocity in the moving wall frame (`ReflectAgainstNormalInMovingFrame` with normal `-radialDir`). PBF predict/apply plus bucket motion can push Outside particles past the shell in a single step (`maxCorrection ≈ 0.2h` is much smaller than `wallThickness`); without this path they accumulate outside the solid shell while collision no-ops.

Open top (`y > height`) is unchanged — over-rim slosh is not blocked here.

### Escaped particles

Skip all bucket collision in Finalize permanently.

---

## Moving bucket behavior

| Motion | Expected behavior |
|--------|-------------------|
| Lateral shake | Carry force co-moves inside fluid; `ComputeContainInside` + face-contact band + beyond-outer clamp prevent wall/floor tunneling |
| Firm directional hold | Sustained bucket translation can push fluid through the shell; beyond-outer clamp keeps `beyondOuter` at 0; over-rim (`y > height`) remains the dominant open-top path |
| Upward move | Fluid lags in world space → brief local `y < 0`; `ShouldContainInside` keeps floor collision active |
| Tilt / spin | `AngularVelocity` in carry; collision reflects in wall moving frame; wall-band floor fix prevents corner leaks |

Regression tests: `V4ContainmentTests` (PlayMode).

---

## Hole authoring

On `V4Bucket`:

```csharp
public List<V4HoleDef> holes;
public float ringSpacing = 0.08f;
public bool autoShrinkRings = true;
```

Each `V4HoleDef` has `localPosition`, `radius`, and optional normal override. Bake computes rings and validates minimum spacing between holes.

---

## Related

- [External forces](07-external-forces.md) — zone force details
- [Invariants & parity](14-invariants-and-parity.md) — CPU/HLSL sync
- Source: `Runtime/Core/V4BucketGeometry.cs`, `Runtime/Core/V4ZoneMath.cs`, `Include/V4BucketZones.hlsl`
