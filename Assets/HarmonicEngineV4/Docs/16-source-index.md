# Source index

Complete file index for `Assets/HarmonicEngineV4/`. Use with [README](README.md) navigation.

---

## Runtime / Simulation

| File | Type | Role |
|------|------|------|
| `V4PipelineRoot.cs` | MonoBehaviour | Pipeline orchestrator, bake, Step(), buffers |
| `V4ParticleSoa.cs` | Class | Ping-pong SOA + `V4Counters` constants |
| `V4Bucket.cs` | MonoBehaviour | Bucket authoring, kinematics |
| `V4Canvas.cs` | MonoBehaviour | Canvas authoring |
| `V4SpawnZone.cs` | MonoBehaviour | Spherical spawn zone |
| `V4SpatialHashGrid.cs` | Class | Grid build + radix sort orchestration |
| `V4GpuRadixSort.cs` | Class | 6-bit LSD radix sort |
| `V4BucketMotionController.cs` | MonoBehaviour | Lab keyboard bucket control |

---

## Runtime / Core

| File | Role |
|------|------|
| `V4PassManifest.cs` / `V4PassDef.cs` | Pass manifest data |
| `V4PassExecutor.cs` | Kernel dispatch + buffer bind |
| `V4BufferRegistry.cs` | Named ComputeBuffer registry |
| `V4ShaderLibrary.cs` | Resources.Load shader paths |
| `V4ParticleFlags.cs` / `V4Zone.cs` | Flag bit layout, zone enum |
| `V4BucketGeometry.cs` | CPU bucket geometry + collision |
| `V4WallEscapeForensics.cs` | Wall-escape exit classification + hole proximity (investigation) |
| `V4ZoneMath.cs` / `V4BakedHole` | Zone classification + baked hole struct |
| `V4CanvasSplatMath.cs` | CPU canvas splat footprint |
| `V4SpatialHashMath.cs` | CPU hash/cell math |
| `V4EmaLossModel.cs` | EMA per-hole loss |
| `V4ManifestValidation.cs` | Manifest UAV validation |
| `V4DeviceCaps.cs` / `V4DeviceTier` | GPU tier detection |

---

## Runtime / Bake

| File | Role |
|------|------|
| `V4BucketBake.cs` / `V4HoleDef.cs` | Hole ring bake |
| `V4SpawnMath.cs` | Density → radius, lattice fill |
| `V4CanvasGridMath.cs` | Canvas grid dimensions |

---

## Runtime / Profiles

| File | Role |
|------|------|
| `V4LiquidProfile.cs` | ScriptableObject liquid tuning |
| `V4GpuProfileData.cs` | 56-byte GPU struct mirror |

---

## Runtime / Rendering

| File | Role |
|------|------|
| `V4DebugPointRenderer.cs` | Particle debug quads |
| `V4ScreenSpaceFluidRenderer.cs` | SSFR CommandBuffer |
| `V4CanvasPaintRenderer.cs` | Canvas texture quad |
| `V4BucketMeshRenderer.cs` | Cosmetic bucket mesh |
| `V4FlyCamera.cs` | Lab fly camera |

---

## Runtime / Logging

| File | Role |
|------|------|
| `V4Log.cs` | Logging facade + categories |
| `V4ConsoleLogSink.cs` | Unity console sink |
| `V4ChannelFileSink.cs` | Per-category file sink |
| `V4ChannelLogSettings.cs` | Category config |
| `V4RunRecorder.cs` / `V4RunRecording.cs` | JSONL run recording |
| `V4RunLogPaths.cs` | Log path helpers |

---

## Runtime / Resources (GPU)

| Asset | Kernels |
|-------|---------|
| `V4Classification.compute` | ClearFrameCounters, Classify |
| `V4ExternalForces.compute` | ExternalForces |
| `V4PbfSolver.compute` | Predict, Density, Lambda, SolveDelta, ApplyDelta, Finalize, Gather |
| `V4SpatialHash.compute` | ClearGridCells, GenerateGridKeys, BuildCellRanges |
| `V4RadixSort.compute` | Extract, Histogram, Scan, Scatter, Pack |
| `V4CanvasSplat.compute` | ClearCanvas, SplatApply, CanvasToTexture |
| `V4DebugPoints.shader` | Debug billboard pass |
| `V4SSFluidRender.shader` | SSFR 3-pass |
| `Include/V4Common.hlsl` | Flags, kernels, hash |
| `Include/V4BucketZones.hlsl` | Geometry, zones, collision |
| `Include/V4Profiles.hlsl` | Profile struct |
| `Include/V4NeighborQuery.hlsl` | Neighbor loop macro |

---

## Editor

| File | Role |
|------|------|
| `V4PassManifestEditor.cs` | Inspector manifest validation |

---

## Profiles (assets)

| Asset | Role |
|-------|------|
| `Profiles/V4Liquid_Default.asset` | Default liquid tuning |

---

## Tests / EditMode

`V4BakeMathTests`, `V4BucketBakeTests`, `V4BucketGeometryTests`, `V4ZoneMathTests`, `V4ParticleFlagsTests`, `V4CanvasSplatMathTests`, `V4EmaLossModelTests`, `V4BufferRegistryTests`, `V4ManifestValidationTests`, `V4DeviceCapsTests`, `V4LogTests`, `V4ChannelFileSinkTests`, `V4RunLogPathsTests`

---

## Tests / PlayMode

`V4TestRig`, `V4PipelineIntegrationTests`, `V4GoldenFrameTests`, `V4GpuCpuParityTests`, `V4ContainmentTests`, `V4CanvasGpuTests`, `V4SpatialHashTests`, `V4RadixSortTests`, `V4RendererSmokeTests`, `V4RunRecorderTests`, `V4WallEscapeFirmHoldInvestigationTests`, `V4Lab2EscapeStatisticsTests`, `V4Lab2LeakProbeTests`, `V4CollisionEnergyInvestigationTests`, `V4CohesionViscosityInvestigationTests`, `V4ApplyDeltaClampInvestigationTests`, `V4ScorrInvestigationTests`, `V4StackingLeakInvestigationTests`, `V4StackingDepthInstrumentationTests`, `V4GhostWeightSweepTests`

Test GPU: `Resources/HarmonicEngineV4Tests/V4TestKernels.compute`

---

## Scenes

| Scene | Purpose |
|-------|---------|
| `Assets/Scenes/HarmonicEngineLab2.unity` | Primary V4 lab / playtest |

---

## Assembly definitions

| File |
|------|
| `Runtime/HarmonicEngineV4.Runtime.asmdef` |
| `Editor/HarmonicEngineV4.Editor.asmdef` |
| `Tests/EditMode/HarmonicEngineV4.Tests.EditMode.asmdef` |
| `Tests/PlayMode/HarmonicEngineV4.Tests.PlayMode.asmdef` |
