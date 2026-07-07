# 15 — Getting started

Run and explore HarmonicEngineV4 in under ten minutes.

---

## Prerequisites

- Unity project with `HarmonicEngineV4.Runtime` assembly compiling
- GPU with compute shader support (`SystemInfo.supportsComputeShaders`)

---

## Quick start

1. Open **`Assets/Scenes/HarmonicEngineLab2.unity`**
2. Press **Play**
3. Use **WASD / arrow keys** (via `V4BucketMotionController`) to move the bucket
4. Watch fluid slosh inside the bucket; hole streams paint the canvas below

---

## Scene components

| Object | Components |
|--------|------------|
| Pipeline root | `V4PipelineRoot` |
| Bucket | `V4Bucket`, mesh renderer, motion controller |
| Canvas | `V4Canvas`, paint renderer |
| Spawn zones | `V4SpawnZone` (colored paint blobs) |
| Camera | `V4FlyCamera`, optional SSFR |

---

## Create a minimal test scene

1. Empty scene with **Directional Light** and **Camera**
2. Create empty GO → add **`V4PipelineRoot`**
3. Child GO → add **`V4Bucket`** (set radius/height)
4. Child GO → add **`V4Canvas`** (position Y below bucket, e.g. -1)
5. Child GO → add **`V4SpawnZone`** (position inside bucket, set radius/color)
6. Assign bucket + canvas on pipeline root
7. Create **`V4LiquidProfile`** asset → assign as `globalProfile`
8. Add **`V4DebugPointRenderer`** → link pipeline
9. Play

---

## Manual stepping (debug)

```csharp
var root = FindObjectOfType<V4PipelineRoot>();
root.autoRun = false;
root.Initialize();

for (int i = 0; i < 60; i++)
    root.Step(1f / 60f);

Debug.Log($"live={root.ActiveParticleCount} settled={root.SettledTotal}");
```

---

## Run tests

**Window → General → Test Runner**

- EditMode: `HarmonicEngineV4.Tests.EditMode` — fast, no play mode
- PlayMode: `HarmonicEngineV4.Tests.PlayMode` — requires GPU

Start with `V4BucketGeometryTests` and `V4ContainmentTests`.

---

## Enable logging

`V4Log` writes to Unity console by default. For file logs, add `V4ChannelLogSettings` and `V4ChannelFileSink` per project logging setup.

For run recordings, attach `V4RunRecording` to record JSONL frame samples.

For leak investigation, enable on `V4PipelineRoot`:

- `debugLogWallEscapeForensics` → `Logs/Engine2/run_*/channels/wall_escape_forensics.log`
- `debugLogBoundaryPressure` → `boundary_pressure.log` (large; optional)

See [Configuration](11-configuration.md) for metric caveats (`cif` vs over-rim / beyond-outer).

---

## Troubleshooting

| Problem | Check |
|---------|-------|
| No particles | Spawn zones inside bucket cavity; `restrictSpawnToBucketCavity` |
| Particles explode | Lower density; increase `pbfIterations`; check spawn overlap |
| Walls don't hold | Shader recompile; see [Bucket & zones](05-bucket-and-zones.md); run `V4ContainmentTests` + `Investigate_FirmDirectionalHold_WallEscapeForensics` |
| Fluid flies over rim | Open top by design — not a wall leak; check `overRim` in forensics log |
| Black paint wrong color | Use debug points or SSFR with `_UseParticleColor` |
| Pipeline disabled on play | Console bake errors; bucket/canvas null refs |
| Null refs after script reload | Pipeline auto-reinits; stop/start play mode |

---

## Read next

1. [Overview](01-overview.md) — concepts and glossary
2. [Frame pipeline](03-frame-pipeline.md) — what runs each frame
3. [Configuration](11-configuration.md) — tuning profiles and density

Full index: [README](README.md)
