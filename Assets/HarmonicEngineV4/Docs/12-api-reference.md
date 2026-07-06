# 12 — API reference

Public entry points and key types for integrating HarmonicEngineV4.

---

## V4PipelineRoot

`HarmonicEngineV4.Simulation.V4PipelineRoot` — **primary API**

### Lifecycle

```csharp
void Initialize();           // Bake + buffers + manifest (also called from Start)
void Step(float deltaTime);  // One simulation frame
```

### Inspector fields

See [Configuration](11-configuration.md).

### Read-only state

| Property | Type | Description |
|----------|------|-------------|
| `Initialized` | bool | Pipeline ready |
| `ActiveParticleCount` | int | Live particles |
| `SpawnedTotal` | int | Initial spawn count |
| `EscapedTotal` | int | Cumulative escaped |
| `SettledTotal` | int | Cumulative canvas settles |
| `FrameIndex` | long | Frames stepped |
| `ParticleRadius` | float | From density bake |
| `SmoothingRadius` | float | 4 × radius |
| `CanvasGridSize` | Vector2Int | Grid dimensions |
| `CanvasCellSize` | float | Meters per cell |
| `TotalExpectedLoss` | float | EMA hole loss |
| `Soa` | V4ParticleSoa | Buffer access |
| `CanvasTexture` | RenderTexture | Canvas display |
| `CanvasGridBuffer` | ComputeBuffer | Raw grid |
| `CountersBuffer` | ComputeBuffer | GPU counters |
| `BakedHoles` | IReadOnlyList<V4BakedHole> | Baked hole data |
| `ProfileTable` | IReadOnlyList<V4LiquidProfile> | Profile list |
| `ActiveManifest` | V4PassManifest | Current pass list |

### Events

```csharp
event Action<long, int, int, int> FrameCompleted;
// (frameIndex, live, escaped, settled)
```

---

## V4Bucket

```csharp
float innerRadius, height, wallThickness, topBandHeight, ringSpacing;
List<V4HoleDef> holes;
Vector3 LinearVelocity { get; }
Vector3 AngularVelocity { get; }
Matrix4x4 LocalToWorld / WorldToLocal { get; }
void SampleKinematics(float deltaTime);
V4BucketBake.Result BakeHoles();
```

---

## V4Canvas

```csharp
float width, depth;
Color baseColor;
float PlaneY { get; }      // world Y
Vector2 MinCorner { get; } // XZ min
```

---

## V4SpawnZone

```csharp
float radius;
Color color;
V4LiquidProfile profile;  // optional override
```

---

## V4LiquidProfile

ScriptableObject — see [Configuration](11-configuration.md).

```csharp
float ZoneStrength(int level);  // 0–3
```

---

## Core reference (CPU-only integration)

Safe to call from EditMode tests or tools without GPU:

```csharp
V4BucketGeometry.IsInside(localPos, innerRadius, height);
V4BucketGeometry.ShouldContainInside(localPos, innerRadius, height, wallThickness);
V4BucketGeometry.ResolveCollision(ref pos, ref vel, ...);
V4ZoneMath.Classify(...);
V4CanvasSplatMath.ApplySplat(...);
V4ParticleFlags.IsInside / HasEscaped / GetZone / GetProfile;
V4SpawnMath.ParticleRadiusFromDensity(density);
```

---

## Logging

```csharp
V4Log.Info(V4LogCategory.General, "message");
using (V4Log.BeginPass("MyPass")) { ... }
```

Categories: `General`, `Bake`, `Pass`, `CanvasSettle`, etc.

---

## Test rig (PlayMode)

```csharp
using var rig = V4TestRig.Create(config);
rig.Step(frames, dt);
Vector4[] positions = rig.ReadPositions();
uint[] flags = rig.ReadFlags();
Vector4[] canvas = rig.ReadCanvasGrid();
```

`V4TestRig.Config` — bucket size, holes, spawn zones, density, profile callback.

---

## Assembly reference

```xml
<!-- In your asmdef -->
"references": [ "HarmonicEngineV4.Runtime" ]
```

Namespace root: `HarmonicEngineV4.*`

---

## Related

- [Architecture](02-architecture.md)
- [Testing](13-testing.md)
