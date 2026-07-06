# 8 — Canvas & splats

How paint accumulates on the canvas below the bucket.

---

## Canvas authoring

`V4Canvas` component:

| Field | Purpose |
|-------|---------|
| `width`, `depth` | World size in meters (XZ plane) |
| `baseColor` | Empty canvas RGB |
| `transform.position.y` | Sets `PlaneY` — world Y of canvas surface |

Grid computed at bake: cell size = particle radius, max 16M cells guard.

Visual: `V4CanvasPaintRenderer` displays `pipeline.CanvasTexture` on a quad slightly above the plane.

---

## Canvas grid buffer

Each cell stores `float4(rgb, depth)`:

- **rgb** — accumulated paint color (weighted blend)
- **depth** — saturating paint thickness, capped by `canvasMaxDepth`

CPU reference: `V4CanvasSplatMath`  
GPU apply: `V4CanvasSplat.compute` → `SplatApplyKernel`

---

## Two paths to canvas

Both **remove** the particle from live SOA (compaction) and emit a **splat event**.

### 1. Impact absorption (fast splash)

Conditions in `FinalizeKernel`:

- Particle **not** Inside flag (outside or escaped)
- Touching canvas plane: `(pos.y - radius) ≤ canvasPlaneY`
- Within canvas UV rect
- Impact speed `≥ impactAbsorbSpeed` (uses **incoming** `vel.y` before position clamp)

Effect:

- Large splat radius: `radius × (1 + impactSpeed × impactSplashScale)`
- Deep contribution: `1 + impactSpeed × impactSplashScale`
- Immediate removal — no bounce

Tuning: `impactAbsorbSpeed`, `impactSplashScale` on `V4LiquidProfile`.

### 2. Slow settle

Conditions:

- Not removed by impact path
- Outside, near plane (`settleDistance = 2 × particleRadius`)
- On canvas rect
- `|vel| < settleEpsilon`

Effect:

- Unit radius/contribution splat
- Removal on settle

---

## Splat pipeline

```
FinalizeKernel
  └─ InterlockedAdd → _SplatEvents[slot]
  └─ pad0 = source particle index (deterministic sort key)

SplatApplyKernel (1×1×1 dispatch)
  └─ Sort events by pad0
  └─ For each event: V4CanvasSplatMath footprint → _CanvasGrid cells

CanvasToTextureKernel (8×8 groups)
  └─ _CanvasGrid → RenderTexture for renderer
```

Sequential splat apply guarantees **deterministic canvas** regardless of GPU thread scheduling in Finalize.

---

## Bucket projection / beam

Canvas particles already on the surface are **outside** the bucket and should not be affected by bucket motion. Containment and carry apply only to inside/footprint particles. Settled canvas paint lives in `_CanvasGrid`, not live SOA.

---

## Tuning splash feel

| Goal | Adjust |
|------|--------|
| More explosive impacts | Lower `impactAbsorbSpeed`, raise `impactSplashScale` |
| Thicker paint per hit | Raise `impactSplashScale`, `canvasMaxDepth` |
| Less bounce before absorb | Lower `surfaceRestitution` on profile |
| Wider splats | Raise `impactSplashScale` |

---

## Related

- [Data model](04-data-model.md) — splat event struct
- [Rendering](10-rendering.md) — canvas quad
- Tests: `V4CanvasGpuTests`, `V4ContainmentTests.FastCanvasImpact_IsAbsorbedAsPaintSplat`
