# 10 — Rendering

Visual output is **decoupled** from simulation. Render components read GPU buffers produced by `V4PipelineRoot`; they never dispatch simulation kernels.

---

## Debug point renderer

**Component:** `V4DebugPointRenderer` (on camera; toggled from `V4RenderingSettings`)  
**Shader:** `V4DebugPoints.shader`

- Binds `Soa.ReadBlock0`, `ReadColors`, `ActiveParticleCount`
- `Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, count)` — camera-facing quads
- Shows **true particle colors** (including black paint)
- Low cost, best for debugging containment and spawn

Attach to any GameObject; assign `pipeline` reference or auto-find.

---

## Central rendering settings

**Component:** `V4RenderingSettings` (on the same GameObject as `V4PipelineRoot`)

Single inspector for fluid display:

| Field | Purpose |
|-------|---------|
| `targetCamera` | Camera that draws particles (defaults to `Camera.main`) |
| `showScreenSpaceFluid` | Enable SSFR on the camera |
| `showDebugPoints` | Enable debug quads on the camera |
| `debugPointSize` | Debug quad size (0 = particle radius) |
| `screenSpaceFluid` | SSFR tuning block (splat radius, blur, specular, half-res, etc.) |

Settings push to `V4ScreenSpaceFluidRenderer` / `V4DebugPointRenderer` on the camera each frame. Components are auto-added when a mode is enabled and missing.

**Lab2:** `V4 Pipeline` → `V4 Rendering Settings` (SSFR on, debug points off by default).

---

## Screen-space fluid renderer (SSFR)

**Component:** `V4ScreenSpaceFluidRenderer` (on camera; driven by `V4RenderingSettings`)  
**Shader:** `V4SSFluidRender.shader` (3 passes)

Pipeline (CommandBuffer, `CameraEvent.BeforeImageEffects`):

1. **Pass 0** — sphere impostors → MRT (depth + thickness)
2. **Pass 1** — depth blur
3. **Pass 2** — normal reconstruction + specular composite

Options:

- Half-resolution mode for performance
- `_UseParticleColor` — full particle color vs fluid tint

Best for **liquid appearance**; more expensive than debug points.

---

## Canvas paint renderer

**Component:** `V4CanvasPaintRenderer`  
**Data:** `pipeline.CanvasTexture`

- Renders a quad at `canvas.PlaneY + 0.001`
- Unlit texture — shows accumulated splats from `_CanvasGrid`
- Updated every frame after `BlitCanvasToTexture`

---

## Bucket mesh renderer

**Component:** `V4BucketMeshRenderer`

- Procedural transparent cylinder mesh from `V4Bucket` dimensions
- **Cosmetic only** — hole cutouts are visual; collision uses analytic cylinder
- Does not affect simulation

---

## Lab helpers

| Component | Purpose |
|-----------|---------|
| `V4FlyCamera` | RMB + WASD fly camera for lab scenes |
| `V4BucketMotionController` | Keyboard bucket move/tilt/spin for testing |

---

## Typical scene stack

```
Main Camera
  ├─ V4FlyCamera
  └─ V4ScreenSpaceFluidRenderer  (optional)

V4DebugPointRenderer             (optional, separate GO)

V4PipelineRoot
  ├─ V4Bucket (+ V4BucketMeshRenderer, V4BucketMotionController)
  └─ V4Canvas (+ V4CanvasPaintRenderer)
```

You can enable **both** debug points and SSFR; debug draws on top.

---

## Render vs simulation order

Unity render callbacks run **after** `Update`:

1. `V4PipelineRoot.Update` → `Step()` completes (SOA + canvas texture current)
2. `V4ScreenSpaceFluidRenderer.LateUpdate` / `OnRenderObject` reads buffers
3. Canvas quad uses texture from last Step

If renderers show stale/empty data, check `pipeline.Initialized` and `ActiveParticleCount > 0`.

---

## Related

- [Canvas & splats](08-canvas-and-splats.md)
- [Getting started](15-getting-started.md)
