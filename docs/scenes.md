# Scenes

Two entry points separate the **classic VR paint-bucket experience** from the **Harmonic Engine lab** used to develop and validate the GPU pipeline.

| Scene | Path | Purpose |
|-------|------|---------|
| **Classic Paint Simulation** | `Assets/Scenes/ClassicPaintSimulation.unity` | Original swinging-bucket flow: pendulum, bucket, CPU/GPU particle emitter, impasto canvas, `SimulationManager`, keyboard controls, bake record/playback. |
| **Harmonic Engine Lab** | `Assets/Scenes/HarmonicEngineLab.unity` | Engine development scene: shape-volume emission, world-falling particles, debug point rendering, optional capture + YouTube-style timeline scrubber. |

Legacy aliases (same content, kept for older docs/links):

- `MainSimulation.unity` → classic scene
- `AnasScene.unity` → engine lab scene

---

## Classic Paint Simulation

**Open:** `ClassicPaintSimulation.unity`

**What happens**

1. Swinging bucket with pendulum physics
2. Particles spawn from the bucket (`ParticleEmitter` → GPU pipeline when enabled)
3. Paint hits the canvas (`CanvasController` + impasto shader)
4. `SimulationManager` drives start / pause / reset and quality tiers

**Controls** (`HarmonicSimulationControls` on `SimulationManager`)

| Key | Action |
|-----|--------|
| Space | Start |
| P | Pause |
| R | Reset |
| 1–4 | Quality tier |
| B | Bake record |
| V | Bake playback |
| L | Live mode |
| S | Save canvas PNG to Pictures folder |

**Hierarchy (minimum)**

```text
MainSimulation
├── HarmonicPipelineRoot     → PipelineExecutionController, HarmonicBakeRecorder
├── SimulationManager        → SimulationManager, HarmonicSimulationControls
├── Canvas                   → CanvasController, HighScaleFramePresenter
└── Bucket                   → PendulumSimulator, BucketController, ParticleEmitter
```

---

## Harmonic Engine Lab

**Open:** `HarmonicEngineLab.unity`

**What happens**

1. On load: setup prompt (particle count, duration, optional **Save calculation**)
2. **Save OFF** — live world-falling simulation for the requested duration
3. **Save ON** — record every frame, scrub already-calculated frames while still computing, save `.harmonicbake` to disk, then full timeline playback

**Hierarchy (minimum)**

```text
HarmonicEngineLab
├── HarmonicPipelineRoot     → PipelineExecutionController, HarmonicParticleDebugRenderer, HarmonicDiagnosticHost
├── ParticleShapeVolume      → ShapeVolumeEmitter (sphere fill)
├── SimulationTimeline       → SimulationTimelineDirector, SimulationTimelineRenderer
├── Main Camera              → GodModeFlyCamera
└── Directional Light
```

See [harmonic-engine-api.md](harmonic-engine-api.md) sections 7A–7B for API details.

---

## Build settings

Both scenes are registered in **File → Build Settings** so builds and CI can load either entry point.
