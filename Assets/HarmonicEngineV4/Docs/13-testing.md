# 13 — Testing

HarmonicEngineV4 uses a **three-layer** test strategy. All tests live under `Assets/HarmonicEngineV4/Tests/`.

---

## Layer overview

| Layer | Mode | GPU | Purpose |
|-------|------|-----|---------|
| **1 — CPU reference** | EditMode | No* | Pure math: geometry, zones, bake, flags, splat, EMA, manifest |
| **2 — GPU/CPU parity** | PlayMode | Yes | HLSL matches C# on thousands of seeded samples |
| **3 — Integration** | PlayMode | Yes | Full pipeline invariants, determinism, containment, canvas |

\*Some EditMode buffer tests skip if compute unavailable.

---

## Assemblies

- `HarmonicEngineV4.Tests.EditMode` — Editor only
- `HarmonicEngineV4.Tests.PlayMode` — Player + Editor

Run via Unity Test Runner or Unity MCP `run_tests` tool.

---

## EditMode tests

| File | Covers |
|------|--------|
| `V4BakeMathTests` | Spawn spacing, lattice, canvas grid |
| `V4BucketBakeTests` | Hole rings, spacing validation |
| `V4BucketGeometryTests` | Inside, shell, collision, ShouldContainInside |
| `V4ZoneMathTests` | Classification priority, force direction |
| `V4ParticleFlagsTests` | Bit packing |
| `V4CanvasSplatMathTests` | Cell weights, depth blend |
| `V4EmaLossModelTests` | EMA step |
| `V4BufferRegistryTests` | Registry lifecycle |
| `V4ManifestValidationTests` | UAV budget, duplicate pass IDs |
| `V4DeviceCapsTests` | Tier detection |
| `V4LogTests` / `V4ChannelFileSinkTests` / `V4RunLogPathsTests` | Logging |

Fast — run on every commit without GPU.

---

## PlayMode tests

| File | Covers |
|------|--------|
| `V4TestRig` | Headless pipeline factory for all integration tests |
| `V4PipelineIntegrationTests` | Spawn, conservation, determinism, escape permanence |
| `V4GoldenFrameTests` | 100-frame choreography baselines |
| `V4GpuCpuParityTests` | Geometry, collision, flags, color pack |
| `V4ContainmentTests` | Wall/floor leaks under motion, canvas impact |
| `V4CanvasGpuTests` | GPU splat vs CPU reference |
| `V4SpatialHashTests` | Grid build, neighbor coverage |
| `V4RadixSortTests` | Sort stability |
| `V4RendererSmokeTests` | Renderer init smoke |
| `V4RunRecorderTests` | JSONL recording |

### V4TestRig usage

```csharp
using var rig = V4TestRig.Create(new V4TestRig.Config {
    BucketRadius = 0.3f,
    GlobalDensity = 120000f,
    RestrictSpawnToBucketCavity = false,  // for free-fall spawns
});
rig.Step(120, 1f/60f);
Assert.AreEqual(0, rig.Root.EscapedTotal);
```

Always set `autoRun = false` (rig does this). Call `Step` explicitly.

---

## Key invariants tested

| Invariant | Test |
|-----------|------|
| `Spawned == live + settled` | `V4PipelineIntegrationTests` |
| Escape latch permanent | Integration + containment |
| Deterministic replay | `V4GoldenFrameTests` |
| CPU ↔ GPU geometry | `V4GpuCpuParityTests` |
| No floor/wall tunnel | `V4ContainmentTests` |
| Canvas splat math | `V4CanvasGpuTests` |

---

## Running tests

### Unity Test Runner

1. **Window → General → Test Runner**
2. EditMode or PlayMode tab
3. Filter assembly `HarmonicEngineV4`

### Unity MCP

```
run_tests(mode=PlayMode, assembly_names=["HarmonicEngineV4.Tests.PlayMode"])
```

PlayMode needs `init_timeout` ≥ 120000 ms for domain reload.

### CI

Project may add V4 EditMode to CI; PlayMode typically manual (GPU required).

---

## Adding tests

1. **Geometry/zone change** → update C# + HLSL, extend `V4BucketGeometryTests` + parity tests
2. **New GPU kernel** → add manifest entry, manifest validation test, integration smoke
3. **New behavior** → prefer `V4TestRig` integration over scene-based tests
4. **Regression** → add focused test to `V4ContainmentTests` or golden frame

See also [`docs/testing-strategy.md`](../../../docs/testing-strategy.md) (project-wide, includes V4 section).

---

## Related

- [Invariants & parity](14-invariants-and-parity.md)
