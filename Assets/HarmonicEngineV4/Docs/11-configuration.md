# 11 — Configuration

How to wire scenes, create liquid profiles, and tune simulation behavior.

---

## Scene wiring checklist

Minimum required hierarchy:

```
V4PipelineRoot
├── bucket  → V4Bucket
├── canvas  → V4Canvas
└── spawnZones (children or explicit list)
      └── V4SpawnZone(s)
```

Optional:

- `V4BucketMeshRenderer` on bucket
- `V4BucketMotionController` on bucket
- `V4CanvasPaintRenderer` on canvas
- `V4DebugPointRenderer` / `V4ScreenSpaceFluidRenderer` on camera
- `V4RunRecording` for JSONL logs

**Lab scene reference:** `Assets/Scenes/HarmonicEngineLab2.unity`

---

## V4PipelineRoot parameters

### Scene objects

| Field | Description |
|-------|-------------|
| `bucket` | Required. `V4Bucket` authoring |
| `canvas` | Required. Paint surface |
| `spawnZones` | Spherical spawn regions; auto-discovers children if empty |

### Spawn

| Field | Default | Description |
|-------|---------|-------------|
| `globalDensity` | 200000 | Particles/m³ → particle radius |
| `globalProfile` | — | Fallback `V4LiquidProfile` |
| `restrictSpawnToBucketCavity` | true | Cull spawn points outside inner cavity |

High density (1M+) → tiny particles, huge counts. Lab may use 200k–1M.

### Simulation

| Field | Default | Description |
|-------|---------|-------------|
| `autoRun` | true | Step in Update |
| `pbfIterations` | 2 | PBF repeat count (1–4) |
| `carryRate` | 10 | Bucket co-move strength |
| `downwardScale` | 1 | Top-band push multiplier |
| `emaSmoothing` | 0.1 | Hole loss EMA smoothing |
| `pbfEpsilon` | 50 | PBF CFM relaxation |
| `maxDeltaTime` | 1/50 | Cap dt for stability |
| `gravity` | (0,-9.81,0) | World gravity |
| `maxSplatEvents` | 1024 | Max canvas splats per frame |

---

## V4Bucket parameters

| Field | Description |
|-------|-------------|
| `innerRadius` | Cavity radius (meters) |
| `height` | Rim height above floor |
| `wallThickness` | Solid shell thickness |
| `topBandHeight` | Level-2 zone height below rim |
| `ringSpacing` | Hole zone ring spacing |
| `holes` | List of `V4HoleDef` |
| `autoShrinkRings` | Shrink overlapping rings at bake |

---

## V4SpawnZone parameters

| Field | Description |
|-------|-------------|
| `radius` | Sphere radius in world space (position = transform) |
| `color` | Initial particle color |
| `profile` | Optional per-zone liquid profile |

Zone transform is in **world space**; bake converts to bucket-local for cavity filter.

---

## V4LiquidProfile

Create via **Assets → Create → HarmonicEngineV4 → Liquid Profile**.

Default asset: `Profiles/V4Liquid_Default.asset`

| Section | Key fields |
|---------|------------|
| PBF | `restDensity`, `viscosity`, `cohesion`, `pbfKCorr`, `pbfNCorr`, `pbfDeltaQScale` |
| Color | `colorDiffusionRate` |
| Canvas | `settleEpsilon`, `canvasMaxDepth`, `impactAbsorbSpeed`, `impactSplashScale` |
| Surface | `surfaceFriction`, `surfaceRestitution` |
| Zones | `zoneStrengthByLevel` curve (keys 0–3) |

Zone curve defaults:

- t=0 → 2.5 (Zone 0 eject speed target)
- t=1 → 1.5 (Zone 1 pull)
- t=2 → 0.5 (Zone 2 pull)
- t=3 → 1.0 (top-band scale)

---

## Tuning workflow

1. **Containment first** — sealed bucket, no holes, debug points, shake with `V4BucketMotionController`
2. **Viscosity/cohesion** — profile sliders until slosh feels right
3. **Holes** — add holes, tune zone curve and `ringSpacing`
4. **Canvas** — impact/splash via `impactAbsorbSpeed`, `impactSplashScale`
5. **Performance** — lower density, SSFR half-res, reduce `pbfIterations`

Run `V4ContainmentTests` after collision changes.

---

## Related

- [PBF solver](06-pbf-solver.md)
- [External forces](07-external-forces.md)
- [Getting started](15-getting-started.md)
