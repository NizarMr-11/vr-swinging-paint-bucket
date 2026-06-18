# Debugging and Rendering

How to visualize GPU particles and profile the pipeline.

**See also:** [`scenes.md`](scenes.md) · [`HarmonicEngineLab`](../Assets/Scenes/HarmonicEngineLab.unity)

---

## Particle debug rendering

**Component:** [`HarmonicParticleDebugRenderer`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/PlaybackStreaming/HarmonicParticleDebugRenderer.cs) on `HarmonicPipelineRoot`

**Shader:** [`HarmonicEngine/ParticleDebugPoints`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Shaders/ParticleDebugPoints.shader)

- Draws in `OnRenderObject` via `Graphics.DrawProceduralNow` (points → geometry shader billboards)
- Binds live `ComputeBuffer` from `TryGetInternalParticleBuffer` / `TryGetFallingParticleBuffer`
- Mode routing:
  - `ContainerFluidEnabled` → internal buffer only
  - `WorldFallingOnly` → falling/world buffer only
  - Else → internal + falling

### Point size

| Setting | Effect |
|---------|--------|
| `autoSizeFromSph` | Radius = `SmoothingRadius × pointSizeMultiplier` |
| Manual `pointSize` | World-radius in meters when auto off |

Logic lives in [`HarmonicParticleDebugSizing`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/PlaybackStreaming/HarmonicParticleDebugSizing.cs) (testable).

---

## Stats overlay (lab)

**Component:** [`HarmonicPipelineStatsOverlay`](../Assets/SwingingPaintBucket/Scripts/Debugging/HarmonicPipelineStatsOverlay.cs)

On-screen GUI:

- FPS / frame ms
- Active particles / max capacity
- Sort size (`FrameSortSize / PaddedSortSize`)
- Profiler markers: `Harmonic.ContainerFluidFrame`, `Harmonic.SpatialHashGrid`, `Harmonic.BitonicSort`, `Harmonic.SphDensity`, `Harmonic.SphIntegration`

---

## Pipeline diagnostics

On `PipelineExecutionController`:

| Flag | Effect |
|------|--------|
| `verbosePipelineDiagnostics` | Stage logs on count change |
| `perfDiagnosticsMuted` | Disables GPU position readback samples |
| `positionSampleInterval` | Periodic min/max/avg Y + velocity readback |

**Hub:** `HarmonicDiagnosticHost` + aspects (file log, overlay, telemetry) via `HarmonicDiagnosticHub`.

---

## Timeline playback

[`SimulationTimelineRenderer`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/PlaybackStreaming/SimulationTimelineRenderer.cs) reuses `ParticleDebugPoints` with CPU-uploaded positions for bake scrubbing.

---

## Quick checklist (HarmonicEngineLab)

1. Play scene — stats overlay shows `Active: X / 30000`
2. Particles visible as soft blue billboards inside glass bucket
3. Console: no shader compile errors
4. Profiler: `Harmonic.SpatialHashGrid` + `Harmonic.SphDensity` visible when sim running
