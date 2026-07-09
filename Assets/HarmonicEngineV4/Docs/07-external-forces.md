# 7 — External forces

Forces applied in `V4ExternalForces.compute` → `ExternalForcesKernel`, after classification, before PBF predict.

Each thread reads particle position, flags, profile, and bucket kinematics; writes updated velocity to `_Block1`.

---

## Gravity

Applied to **all** particles including escaped:

```
vel += gravity × dt
```

`gravity` from `V4PipelineRoot.gravity` (default `(0, -9.81, 0)`).

---

## Bucket carry

Applied to particles in the **containment footprint** (`V4BucketShouldContainInside`), matching collision — not the Inside classification flag. This includes brief sub-floor lag during fast bucket motion.

```
v_target = linearVel + angularVel × (pos - bucketOrigin)
blend = 1 - exp(-carryRate × dt)
vel += (v_target - vel) × blend
```

| Parameter | Default | Effect |
|-----------|---------|--------|
| `carryRate` | 10 | Higher = fluid tracks bucket motion faster |

Kinematics from `_BucketState` after GPU bucket integrate (pendulum) or `V4Bucket.SampleKinematics(dt)` (keyboard). Bucket **pose** (origin and local/world matrices) is always taken from the Unity transform via CPU uniforms; velocities and accelerations come from `_BucketState`.

---

## Non-inertial forces (pendulum mode)

When `V4BucketMotionSettings.IsPendulumMode` is true, inside particles also receive fictitious accelerations in world space (after gravity, using pre-carry relative velocity for Coriolis):

```
r = pos - bucketOrigin
v_rel = vel - (linearVel + angularVel × r)
centrifugal = -ω × (ω × r)
euler = -α × r
coriolis = -2 × ω × v_rel
translational = -linearAcceleration
vel += (centrifugal + euler + coriolis + translational) × dt
```

`linearAcceleration` is computed each frame in `BucketIntegrateKernel` from the change in bucket linear velocity.

---

## Hole zone forces

Only particles with active zone flags (inside at classify time, except Zone 0 which triggers escape same frame).

### Zone 0 — eject

Velocity **target** along hole `outwardNormal` (world space):

```
Raise speed toward zoneStrength0 (m/s), not scaled by dt
```

Zone 0 also sets escape latch in Classify — next frame particle is outside all inside-only forces.

### Zone 1 / 2 — pull

Pull toward hole opening:

```
dir = normalize(hole.localPosition - localPos)  → world space
vel += dir × zoneStrength1or2 × dt
```

Strengths from profile `zoneStrengthByLevel` keys 1 and 2.

---

## Top-band push-down

Particles in **TopBand** zone receive downward acceleration based on historical loss through holes:

```
pushDown = (TotalExpectedLoss / max(topBandCount, 1)) × downwardScale × topBandScale × dt
vel += bucketLocalDown × pushDown
```

`bucketLocalDown` is the world-space direction of local −Y (bucket floor normal).

| Input | Source |
|-------|--------|
| `TotalExpectedLoss` | CPU `V4EmaLossModel` from prior frames |
| `topBandCount` | Counter from this frame's Classify |
| `downwardScale` | `V4PipelineRoot.downwardScale` |
| `topBandScale` | Profile `zoneStrengthByLevel` key 3 |

### EMA loss model

CPU-side `V4EmaLossModel`:

- Each frame: read per-hole eject counters (slots 8–39)
- `ema[i] = lerp(ema[i], ejected[i], emaSmoothing)`
- `TotalExpectedLoss = sum(ema)`

Simulates paint loss through holes feeding back as top-band downward pressure (keeps rim fluid from piling unrealistically).

---

## Force application order (within kernel)

Typical order per particle:

1. Gravity
2. Carry (if in containment footprint)
3. Non-inertial fictitious forces (if pendulum mode)
4. Zone force (if zone active)
5. Top-band push (if TopBand)

Exact implementation in `V4ExternalForces.compute` — consult source for branch order.

---

## Related

- [Bucket & zones](05-bucket-and-zones.md)
- [Configuration](11-configuration.md) — tuning zone curve
- Source: `Runtime/Resources/HarmonicEngineV4/V4ExternalForces.compute`
