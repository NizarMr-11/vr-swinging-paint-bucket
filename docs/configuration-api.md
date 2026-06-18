# Configuration API — Spawn, Canvas, Bucket

Three typed objects describe what the engine needs. **Plane + cylinder only** for collision; meshes are cosmetic or spawn-only.

**Types:** `HarmonicSpawnRegion` · `HarmonicCanvasSurface` · `HarmonicBucketVolume`  
**Facade:** `HarmonicParticleSpawner.Spawn`  
**Controller:** `PipelineExecutionController.SetSpawnRegion` / `SetCanvasSurface` / `SetBucketVolume`

---

## HarmonicSpawnRegion — where particles spawn

| Field | Purpose |
|-------|---------|
| `shape` | Box, Sphere, Capsule, or **Mesh** (volume fill only) |
| `center`, `rotation`, size params | World placement |
| `particleCount`, `restDensity`, `initialVelocity` | Particle state |
| `spawnColor` | Packed into `FluidParticle.PackedColorRGBA` |

```csharp
var region = new HarmonicSpawnRegion
{
    shape = ShapeVolumeType.Sphere,
    center = new Vector3(0, 1.5f, 0),
    sphereRadius = 0.2f,
    particleCount = 8000,
    spawnColor = Color.cyan
};
pipeline.SpawnVolume(region);
// or: HarmonicParticleSpawner.Spawn(pipeline, region);
```

`ShapeVolumeEmitter` and future emitters should build a `HarmonicSpawnRegion` and call the spawner.

---

## HarmonicCanvasSurface — surface particles hit

**Not mesh collision.** The GPU uses a single horizontal plane at `planeY` ([`FallingFluidWorld.compute`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/FallingFluidWorld.compute)).

| Field | Purpose |
|-------|---------|
| `planeY` | World Y of the paint/floor plane |
| `cullIntoCanvas` | `true` = paint canvas mode; `false` = solid floor |
| `paintAbsorbEnabled` | `true` = particle rests and drains wetness; `false` = instant single hit |
| `absorbRate`, `absorbPaintWeightScale` | GPU wetness drain + splat weight |
| `center`, `size` | Document UV bounds for `CanvasController.TryWorldToUv` |

```csharp
pipeline.SetCanvasSurface(new HarmonicCanvasSurface
{
    planeY = canvas.transform.position.y,
    cullIntoCanvas = true,
    paintAbsorbEnabled = true,
    absorbRate = 1.5f,
    center = canvas.transform.position,
    size = new Vector2(canvas.transform.localScale.x, canvas.transform.localScale.z)
});
```

Paint hits flow: GPU (`CanvasPaintHit` with per-particle `PackedColorRGBA`) → `TryGetCanvasHitBuffer` → `HarmonicCanvasHitBridge` → `CanvasController.OnParticleHit(worldPos, color, viscosity, wetnessDeposit)`.

See [`paint-color-and-lifecycle.md`](paint-color-and-lifecycle.md) for mixing, absorption, and drying.

---

## HarmonicBucketVolume — volume particles interact with

Two modes wrapped in one type:

**Container mode (lab):** open-top cylinder via `ContainerFluidSettings`  
**Bucket mode (classic):** local nozzle SDF + rim spill

```csharp
// Lab container
pipeline.SetBucketVolume(HarmonicBucketVolume.FromFluidContainer(fluidContainer));
pipeline.SetContainerFluidEnabled(true);

// Classic bucket (via transform + nozzle params on controller)
pipeline.SetBucketTransform(bucketTransform);
pipeline.SetBucketKinematicProvider(kinematicBridge);
```

Cylinder params: `center`, `radius`, `floorY`, `rimY`, restitution/friction/wall stiffness.

---

## HarmonicEngineLab example

```csharp
pipeline.SetCanvasSurface(new HarmonicCanvasSurface { planeY = -2f, cullIntoCanvas = false });
pipeline.SetBucketVolume(HarmonicBucketVolume.FromCylinder(
    center, radius, floorY, rimY, restitution, friction, wallStiffness));
pipeline.SetContainerFluidEnabled(true);
pipeline.EnableExternalIngestion(true);
HarmonicParticleSpawner.Spawn(pipeline, rainRegion);
pipeline.SetSimulationActive(true);
```
