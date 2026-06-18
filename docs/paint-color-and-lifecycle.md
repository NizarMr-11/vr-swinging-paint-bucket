# Paint Color and Lifecycle

How per-particle color flows from spawn through SPH mixing, canvas hits, absorption, and drying.

**See also:** [`configuration-api.md`](configuration-api.md) · [`engine-communication.md`](engine-communication.md) · [`debugging-and-rendering.md`](debugging-and-rendering.md) · [`testing-strategy.md`](testing-strategy.md)

---

## FluidParticle layout (48 bytes)

| Block | Fields | Notes |
|-------|--------|-------|
| 1 | `Position` + `Density` | World or bucket-local depending on mode |
| 2 | `Velocity` + `Pressure` | SPH state |
| 3 | `PackedColorRGBA` + `_Padding` | RGBA8 color; `_Padding.x` stores **wetness** for resting canvas particles |

C# mirror: [`FluidParticle.cs`](../Assets/AdvancedHarmonicEngine_V3/Domain/Models/FluidParticle.cs)  
GPU mirror: [`SphCommon.hlsl`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/Include/SphCommon.hlsl)

Pack/unpack helpers:

- C#: `FluidParticleFactory.PackColor` / `UnpackColor`
- HLSL: `PackFloat3ToUint` / `UnpackUintToFloat3`

---

## Spawn color

Each [`HarmonicSpawnRegion`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/HarmonicSpawnRegion.cs) carries a `spawnColor`. [`HarmonicParticleSpawner`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/HarmonicParticleSpawner.cs) writes `PackedColorRGBA` at append time. Queue multiple regions with different colors before the sim starts to get interacting colored bodies.

---

## SPH color mixing

In Pass 6 (`StreamCompactionPingPong.compute`), neighbors contribute a Laplacian diffusion term scaled by `_ColorDiffusionRate`. At rate `0` colors stay distinct; higher rates produce marbling and eventual blending.

Set from the pipeline:

```csharp
pipeline.SetColorDiffusionRate(1.5f); // lab tuning
pipeline.SetColorDiffusionRate(0f);   // non-lab scenes (default)
```

---

## Canvas hits (per-particle color)

[`CanvasPaintHit`](../Assets/AdvancedHarmonicEngine_V3/Domain/Models/CanvasPaintHit.cs) (32 bytes):

| Field | Purpose |
|-------|---------|
| `WorldPosition` | Hit location on the plane |
| `PaintWeight` | Splat intensity / impasto scale |
| `PackedColorRGBA` | Real particle color (not bucket default) |
| `WetnessDeposit` | Amount of wet paint deposited this frame |

Written in [`FallingFluidWorld.compute`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/FallingFluidWorld.compute), read by [`HarmonicCanvasHitBridge`](../Assets/SwingingPaintBucket/Scripts/Particles/HarmonicCanvasHitBridge.cs), which calls `CanvasController.OnParticleHit(..., wetnessDeposit)`.

---

## Stay + absorb + dry lifecycle

When canvas culling is enabled and **paint absorb** is on (default):

1. Particle hits the plane → rests at `planeY`, velocity zeroed.
2. `_Padding.x` (wetness) starts at `1.0` and drains at `_CanvasAbsorbRate`.
3. Each drain step appends a `CanvasPaintHit` with partial weight + wetness.
4. When wetness reaches zero the particle is removed from the falling buffer.

Configure via [`HarmonicCanvasSurface`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Management/HarmonicCanvasSurface.cs):

```csharp
pipeline.SetCanvasSurface(new HarmonicCanvasSurface
{
    planeY = canvasY,
    cullIntoCanvas = true,
    paintAbsorbEnabled = true,
    absorbRate = 1.5f,
    absorbPaintWeightScale = 1f
});
```

Set `paintAbsorbEnabled = false` for the legacy instant single-hit cull.

**Drying on the canvas:** [`CanvasController`](../Assets/SwingingPaintBucket/Scripts/Canvas/CanvasController.cs) maintains a per-pixel wetness map. Wetness decays over time (`wetnessDryRate`); as it drops, stamped color desaturates and darkens slightly to simulate drying.

---

## Debug visualization

[`HarmonicParticleDebugRenderer`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/PlaybackStreaming/HarmonicParticleDebugRenderer.cs) with `useParticleColor = true` tints debug points from `PackedColorRGBA` via [`ParticleDebugPoints.shader`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Shaders/ParticleDebugPoints.shader).

---

## Tests

| Test | What it checks |
|------|----------------|
| `FluidParticleColorTests` | Pack/unpack round-trip |
| `MultiVolumeSpawnColorPlayModeTests` | Two spawn regions → distinct colors |
| `ColorDiffusionPlayModeTests` | Low vs high `_ColorDiffusionRate` |
| `CanvasPerHitColorPlayModeTests` | GPU hit buffer carries particle RGB |

See [`testing-strategy.md`](testing-strategy.md) for how to run them.
