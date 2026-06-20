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

## Screen-space fluid rendering (Built-in pipeline)

**Components (HarmonicEngineLab — Main Camera):**

| Component | Role |
|-----------|------|
| [`HarmonicFluidVisualController`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Rendering/HarmonicFluidVisualController.cs) | Toggle **Screen Space Fluid** vs **Debug Points** |
| [`HarmonicScreenSpaceFluidRenderer`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Rendering/HarmonicScreenSpaceFluidRenderer.cs) | Built-in `CommandBuffer` @ `BeforeImageEffects` |

**Shader / material:** [`HarmonicEngine/SSFluidRender`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Shaders/SSFluidRender.shader) · [`SSFluidRender.mat`](../Assets/AdvancedHarmonicEngine_V3/Infrastructure/Materials/SSFluidRender.mat)

### Pass flow

1. **DepthAndThickness** — sphere impostors from live `ComputeBuffer` (48-byte `FluidParticle`, read-only)
2. **BilateralBlur** — edge-preserving blur on linear eye depth
3. **Composite** — view-space normals (ddx/ddy depth), Blinn-Phong + Fresnel, alpha-blended over camera color via thickness
4. **DebugDepthVis** (Pass 3) — when `debugPass0Only` is enabled on `HarmonicScreenSpaceFluidRenderer`, blits normalized eye depth to the camera target (near=white, far=black) for Pass 0 verification

### Toggle

| Mode | Behaviour |
|------|-----------|
| `ScreenSpaceFluid` (default in Play) | SSFR on; debug billboards suppressed via `HarmonicParticleDebugRenderer.SuppressDrawing` |
| `DebugPoints` | Legacy `ParticleDebugPoints` billboards |

### Tuning (Main Camera → Harmonic Screen Space Fluid Renderer)

| Setting | Effect |
|---------|--------|
| `splatRadiusMultiplier` | Sphere size = `SmoothingRadius × multiplier` |
| `thicknessWeight` | Additive thickness per splat |
| `blurFalloff` | Bilateral range weight (edge sharpness) |
| `blurRadius` | Depth blur kernel scale (surface tension) |
| `normalScale` | Normal reconstruction strength from depth gradients (flip sign if lighting inverted) |
| `specularPower` / `specularIntensity` | Highlight on reconstructed surface |
| `thicknessAbsorption` | Alpha from accumulated thickness (soft edges) |
| `halfResolutionFluid` | Half-res depth/thickness RTs (performance) |
| `debugPass0Only` | Pass 0 only: grayscale depth splats to screen (disable for full composite) |
| `debugMaxEyeDepth` | Eye-depth normalization range for Pass 0 debug view |

### Lab hotkeys (HarmonicEngineLab — Main Camera → Harmonic Lab View Controller)

| Key | Overlay | Particles |
|-----|---------|-----------|
| **1** | Pipeline stats (`HarmonicPipelineStatsOverlay`) | SSFR fluid render |
| **2** | AOP diagnostic (`HarmonicDiagnosticHost`) | Debug point billboards |

**Future URP port:** replace `CommandBuffer` + `RenderTexture` with `ScriptableRenderPass` + `RTHandle` + `Blitter`; keep shader math and buffer bindings unchanged.

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
