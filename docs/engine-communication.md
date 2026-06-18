# Engine Communication

How scene code, bridges, diagnostics, and the GPU pipeline talk to each other.

**See also:** [`harmonic-engine-api.md`](harmonic-engine-api.md) · [`configuration-api.md`](configuration-api.md)

---

## Central owner: `PipelineExecutionController`

Single GPU simulation owner (~1,100 lines). Scene code **configures** it and **appends** particles; consumers **read** buffers after each frame.

### Pull model (default)

| API | Use |
|-----|-----|
| `SetSimulationActive`, `EnableExternalIngestion`, `ClearAllParticles` | Lifecycle |
| `SetContainerFluidEnabled`, `SetCanvasSurface`, `SetBucketVolume` | Mode + geometry |
| `AppendParticles` / `HarmonicParticleSpawner.Spawn` | Ingestion |
| `TryGetInternalParticleBuffer`, `TryGetFallingParticleBuffer`, `TryGetCanvasHitBuffer` | Read GPU results |
| `TryGetSpatialHashBuffers` | Tests / spatial-hash diagnostics |

Depend on **`IHarmonicParticleSource`** when you only need the read surface (debug renderer, overlays, tests).

### Push model (additive)

`PipelineExecutionController.FrameCompleted` fires at the end of each simulated frame with `HarmonicFrameInfo` (active count, canvas hits, sort size, mode flags). Use for overlays/logging without polling every `Update`.

---

## Bridge pattern (SwingingPaintBucket)

Thin MonoBehaviour adapters on the bucket/canvas side:

| Bridge | Direction | Role |
|--------|-----------|------|
| `HarmonicGpuEmitterBridge` | CPU → GPU | `AppendParticles` from `ParticleEmitter` |
| `HarmonicBucketKinematicBridge` | Scene → GPU | `IBucketKinematicProvider` for pseudo-forces |
| `HarmonicCanvasHitBridge` | GPU → CPU | `TryGetCanvasHitBuffer` → `CanvasController.OnParticleHit` |

`SimulationManager` wires bridges on `Start`. Lab scene (`HarmonicEngineLab`) often calls the pipeline directly via `ParticleRainDirector` / `FluidContainer`.

---

## Diagnostics hub

Pub/sub cross-cut, decoupled from simulation:

```
HarmonicDiagnosticHost → HarmonicDiagnosticHub.Initialize(pipeline)
Publish(event) → IHarmonicDiagnosticAspect.OnEvent (FileLog, Overlay, Telemetry)
```

Publishers: `PipelineExecutionController`, `ParticleRainDirector`, `ShapeVolumeEmitter`.

---

## Recommended integration order

1. Assign compute shaders on `HarmonicPipelineRoot`.
2. Configure **`HarmonicCanvasSurface`**, **`HarmonicBucketVolume`**, spawn via **`HarmonicSpawnRegion`** (see [`configuration-api.md`](configuration-api.md)).
3. `EnableExternalIngestion(true)` before spawning.
4. `SetSimulationActive(true)` when ready.
5. Read buffers in `LateUpdate` / `OnRenderObject` (after pipeline `Update`).
