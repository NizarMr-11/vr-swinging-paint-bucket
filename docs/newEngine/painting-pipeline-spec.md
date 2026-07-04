# Painting Pipeline — Architecture Spec (v0.2)

## 1. Scene Objects

- **PipelineRoot** — orchestrator. Owns SpawnZones (position, radius, volume, color), global density, refs to Bucket/Canvas, all compute buffers, and drives the bake phase.
- **Bucket** — moving/rotating mesh with holes. Baked once; only transform sent per frame.
- **Canvas** — fixed plane, baked once into a paint grid.

---

## 2. Bake Phase (runtime `Start()`, once)

1. **Bucket geometry bake**: mesh + hole locations → GPU collision representation (SDF or primitive list), static buffer. For each hole, also bake its **zone rings** (see §5) as distance thresholds from the hole opening, in bucket-local space. **Enforce a minimum radius between holes** at bake time: if two holes' outermost zone ring (`d2`) would overlap given their spacing, that's a bake-time validation error (or auto-shrink the rings) rather than a runtime ambiguity - a particle should never be unsure which hole's zone it belongs to.
2. **Canvas grid bake**: cell size = particle width / 2. Grid buffer sized `(width/(w/2)) × (height/(w/2))`, each cell = accumulated color + paint depth (see §6).
3. **Spawn zone bake**: per zone, particle count = f(volume, global density); compute even 3D distribution inside the volume.

---

## 3. Spawn Phase

Each particle gets: position, velocity=0, radius+size (from density), color (from its zone), state = **Outside**, `hasEscaped = false` (permanent, irreversible per your call).

---

## 4. Per-Frame Bucket Update

- Bucket sends position + rotation to GPU (transform only, no re-bake).
- Per particle, per frame: test against bucket's baked collision volume (transformed by current bucket transform) -> **Inside / Outside** flag.
  - **Inside <-> Outside is reversible.** Only `hasEscaped` is permanent - confirmed.
- **Inside** particles: bucket's linear + angular velocity applied to them as a moving-reference-frame contribution.
- **Outside, not escaped** particles: unaffected by bucket motion directly, but collide with bucket's outer surface (slide/friction response, reusing your `OtcClampToContainer`-style pattern from the Harmonic Engine).
- **Escaped** particles: `hasEscaped = true` forever, always Outside, never re-tested against the bucket volume again - skip the collision test entirely for these once flagged.

---

## 5. Zone System (near holes + top-of-bucket) - priority-based, no blending between levels

Two independent zone families, each with a **priority level**. A particle only ever receives the force from the *single highest-priority* zone it currently qualifies for - zone forces never blend or fight each other.

- **Level 1 (higher priority) - Hole zones.** Each hole has nested zones computed from distance to its opening in bucket-local space (baked thresholds `d0 < d1 < d2`, non-overlapping between holes per the min-radius bake constraint in §2):

  | Zone | Meaning | Behavior |
  |---|---|---|
  | **Zone 0** | At the hole | Ejected **next frame**, flagged Outside + `hasEscaped = true` |
  | **Zone 1** | Near the hole | Pulled toward Zone 0 |
  | **Zone 2** | Further out | Pulled toward Zone 1 |

- **Level 2 (lower priority) - Top-layer push.** Particles in the top band of the bucket get pushed downward per the loss-approximation below (no longer a numbered "zone", just a lower-priority force band).

**Resolution rule:** classify the particle against Level 1 first. If it falls in any hole's Zone 0/1/2, apply that force and stop - Level 2 does not apply this frame. Only if it matches no Level 1 zone do you evaluate whether it's in the Level 2 top band and apply that instead. Single priority check per particle per frame - two branches, not two forces summed - which guarantees a hole zone always wins over the top-push near the surface, as you specified.

**Zone force** (one extra term in the external-forces step, before PBF position prediction):

```
dir = normalize(holePos - particlePos)   // bucket-local space
strength = zoneStrength[zone]            // e.g. Zone2=0.5, Zone1=1.5, Zone0=large eject impulse
velocity += dir * strength * deltaTime
```

At the frame a particle is classified Zone 0, apply the eject impulse along the hole's outward normal, set the flags, and let normal PBF integration carry it out - no special-case teleport needed.

**Level 2 top-pressure redistribution (approximation):**

Two small aggregate values computed once per frame via a cheap reduction pass before the main PBF pass:

1. **Expected loss rate** - per hole, an exponential moving average of how many particles actually got ejected (Zone 0 -> escaped) recently:
   `expectedLoss[hole] = lerp(expectedLoss[hole], actualEjectedThisFrame[hole], smoothing)`
   Sum across holes = `totalExpectedLoss`.
2. **Top layer count** - count of particles currently in the top band *and not already claimed by a Level 1 hole zone* this frame (an `InterlockedAdd` counter in the same classification pass).

Then, per top-band particle (that lost the priority check against Level 1):
```
pushDown = (totalExpectedLoss / max(topLayerCount, 1)) * downwardScale
velocity.y -= pushDown * deltaTime
```

O(1) extra buffers (one small counter per hole + one top-layer counter), computed in a dedicated pass right before external-forces/PBF - matches your single-responsibility-pass style from the Harmonic Engine refactor. `smoothing` and `downwardScale` are tuning constants, not physically derived - expect to tune by feel.

---

## 6. PBF Core (2 iterations)

1. Spatial hash + radix sort (broad phase).
2. External forces pass: gravity + bucket-frame contribution (Inside particles) + zone forces (Section 5).
3. 2x PBF solve iterations (density/lambda/position correction).
4. **Color blending**, once per PBF iteration (twice/frame), kernel-weighted by neighbor distance (reuse the same kernel already used for density, e.g. Spiky/Poly6 weight) rather than a flat average - physically consistent with neighbor closeness, and free since you're already iterating neighbors for density/viscosity:
   ```
   blended = weightedAverage(neighborColors, kernelWeights)
   color = lerp(color, blended, k)   // k = diffusion rate, tune by feel
   ```

---

## 7. Canvas Painting

- A particle registers only once velocity < epsilon (settle threshold) - gives the "spread first, then register" behavior for groups landing together.
- **Footprint = radius splat** (confirmed) - cell size = particle width/2, so a settled particle splats across the ~4+ cells its radius overlaps, weighted by overlap area/distance from cell center.
- **On settle: particle is removed from the PBF buffer entirely** (confirmed) - live particle count shrinks over a session, which helps performance, but buffers should be sized for peak concurrent *unsettled* particles, not total ever spawned.

**Stacking rule - recommendation:**
A true unbounded history stack per cell is a bad idea (unbounded memory, and you'd need to recomposite every cell every frame). Recommended instead: incremental weighted blend with a saturating "paint depth", O(1) storage per cell:

```
cell.depth = min(cell.depth + contribution, maxDepth)
alpha = contribution / cell.depth        // newer paint's share of the (capped) total
cell.color = lerp(cell.color, particleColor, alpha)
```

Behaves like real paint: early layers show through less as more paint accumulates, but never needs more than `(color, depth)` per cell - same shape as the color-blend logic in Section 6, applied to a grid cell instead of a particle. If you want very early paint to still show through faintly under thick layers (literal layered look rather than converging to a flat average), that's a different, costlier model (actual top-down alpha compositing) - only worth it if you specifically want that visual.

---

## 8. Buffer Registry & Device-Adaptive UAV Assignment

**Key fact:** the D3D11 8-UAV cap applies per **kernel dispatch**, not globally across the project. Unity's `ComputeShader.SetBuffer(kernelIndex, "BufferName", buffer)` binds by name per-kernel; the HLSL compiler assigns actual register slots. You never manually pick "u0, u1, ..." in C# - the limit is controlled by which buffers each kernel touches, i.e. how work is split into passes.

Recommended structure, matching your existing single-responsibility-pass pattern:

1. **BufferRegistry** (plain C# class or ScriptableObject): dictionary of every ComputeBuffer, keyed by string/enum ID, with metadata (stride, count, lifetime: static-baked vs per-frame, read/write usage).
2. **PassManifest** (data-driven, per quality tier or detected device capability): a list of compute passes, each declaring which buffer IDs it needs bound:
   ```csharp
   struct PassDef {
       string kernelName;
       string[] requiredBuffers;
   }
   ```
   At init, for each `PassDef`, loop `requiredBuffers` and call `SetBuffer` on the matching kernel. Adding/removing a buffer from a pass becomes a data change, not a code change.
3. **Device capability check** (`SystemInfo.graphicsDeviceType`, `SystemInfo.supportsComputeShaders`, etc.) picks which `PassManifest` variant to load - e.g. a "combined" manifest merging zone-force + PBF-external-forces into one kernel on capable hardware, vs a "split" manifest keeping them as separate dispatches (more passes, fewer simultaneous UAVs each) on constrained hardware like the Iris Plus target.
4. Add a small editor/debug validator that sums `requiredBuffers.Length` per pass and warns if it exceeds 8 (or the target device's actual cap) - catches the problem at data-authoring time instead of a cryptic runtime GPU error.

This gives buffer-to-pass assignment as a manifest you can tune per device, without touching kernel code.

---

## 10. Liquid Profiles (Paint vs Water, extensible)

A **LiquidProfile** (ScriptableObject) bundles every tunable that changes "what kind of liquid this is." Every constant that appeared as "tune by feel" earlier in this doc actually belongs here, not hardcoded in the solver:

```csharp
[CreateAssetMenu]
class LiquidProfile : ScriptableObject {
    public string profileName;
    public float restDensity;          // PBF density target
    public float viscosity;            // XSPH coefficient
    public float cohesion;             // cohesion force strength
    public float colorDiffusionRate;   // k, §6
    public float settleEpsilon;        // velocity threshold to register on canvas, §7
    public float canvasMaxDepth;       // paint depth cap, §7
    public float surfaceFriction;      // bucket-wall slide friction
    public float surfaceRestitution;   // bucket-wall bounce
    public AnimationCurve zoneStrengthByLevel; // scales Level 1/2 forces, §5
}
```

- **Water profile**: low viscosity/cohesion, low `settleEpsilon` (keeps sloshing, doesn't stick easily), low `canvasMaxDepth` and fast-converging color blend (water spreads/mixes readily), low surface friction (slides off the bucket).
- **Paint profile**: higher viscosity/cohesion (clumps, moves slower), higher `settleEpsilon`... actually the opposite - paint should settle *more* readily (lower threshold to register, since it's meant to stick), high `canvasMaxDepth` (paint layers build up visibly), high surface friction (paint clings to the bucket wall rather than sliding off cleanly).

**Where profiles attach:** recommend putting the profile reference on each **SpawnZone**, not globally on PipelineRoot - defaulting to a global fallback if a zone doesn't specify one. This is the same architecture either way but a per-zone reference costs nothing and means you can later spawn a water zone and a paint zone in the same scene without restructuring. Confirm if you'd rather keep it strictly global (one liquid per scene) - simpler, but forecloses mixed-liquid scenes.

All the solver/canvas/zone-force code from §5-§7 reads its constants from `particle.profile` (or a per-particle profile index into a small profile lookup buffer on the GPU side) instead of hardcoded values - this is a data change to the buffer layout (one extra `profileIndex` byte per particle) rather than a logic change.

---

## 11. Logging (AOP-style)

True method-level AOP (IL-weaving via Fody, etc.) is overkill here and can't reach into compute shaders anyway - the GPU-side code has no concept of a log call. The pragmatic version that gets you the same benefit: **everything interesting already funnels through the PassManifest execution loop from §8**, so that single loop is the one seam where cross-cutting logging can wrap every pass automatically, without any individual pass writing its own logging code.

```csharp
foreach (var pass in manifest.passes) {
    using (var scope = Log.BeginPass(pass.kernelName)) {   // AOP-style wrapper
        BindBuffers(pass);
        dispatcher.Dispatch(pass);
        scope.RecordBufferSizes(pass.requiredBuffers);
    }
    // scope.Dispose() logs duration, buffer byte totals, and any GPU errors automatically
}
```

- `Log.BeginPass` is the one cross-cutting hook - it logs pass name, start time, on `Dispose()` logs duration + any `GL.GetError()`/async GPU readback errors, with zero logging code inside the actual PBF/zone/canvas kernels or their C# dispatch calls.
- Categories: `Bake`, `Spawn`, `PassExecution`, `ZoneClassification`, `CanvasSettle`, `BufferBinding` - each toggleable independently (so you can silence per-frame PBF pass logs but keep bake/spawn logs, for example).
- Log levels: `Verbose` (per-pass timing, every frame - expensive, dev-only), `Info` (bake results, profile loaded, manifest chosen), `Warning` (UAV budget near limit, hole spacing near minimum), `Error` (bake validation failure, GPU error).
- Sink: a simple `ILogSink` interface (console sink for editor, file sink for the run-recording in §12) so the same log calls feed both the Unity console and the persisted run file without duplicating call sites.

This gets you the AOP *benefit* (instrumentation without cluttering business logic) via one wrapper at the one place all your GPU work already passes through, rather than needing an actual weaving framework.

---

## 12. Run Recording

Every play session, write a run record capturing enough to reproduce/debug it later:

```json
{
  "runId": "2026-07-04T21:14:03Z",
  "device": { "gpu": "...", "graphicsApi": "Direct3D11", "tier": "split" },
  "manifestUsed": { /* the actual PassManifest variant loaded, §8 */ },
  "liquidProfiles": [ { "zone": "ZoneA", "profile": "Water" }, ... ],
  "spawnZones": [ { "position": [...], "radius": ..., "volume": ..., "particleCount": ... } ],
  "density": 1.0,
  "bakeResult": { "holeSpacingOk": true, "canvasGridSize": [cols, rows] },
  "summary": { "totalEscaped": 0, "totalSettled": 0, "avgFrameMs": 0.0 }
}
```

- Written to `Application.persistentDataPath/Runs/run_<timestamp>.json` on scene start (header fields) and appended/finalized on quit or scene teardown (summary fields).
- Embedding the actual manifest (not just its name/version) means a run file is self-contained - if buffer-to-pass assignment changes later, old run files still show exactly what ran.
- This reuses the `ILogSink` from §11 - the run file is just another sink that happens to write structured JSON instead of console lines, so `Log.Info(...)` calls naturally populate both without extra code.

**Open question:** do you want per-frame time-series data in the run file (frame-by-frame particle counts, escape events) or just the start config + end summary? Per-frame is much more useful for debugging but grows large over long sessions - could sample every N frames instead of every frame as a middle ground.

---

## 13. Remaining Notes

- Level 1/Level 2 priority resolution is now a hard rule (hole zones always win, no blending) - remaining tuning is just `d0/d1/d2`, the min-hole-radius, and the top-band height threshold.
- `smoothing`, `downwardScale`, `k` (color diffusion rate), `maxDepth` (canvas) are all tuning constants with no "correct" value - expect an empirical pass once mechanics are running.
- Buffer sizing should target peak concurrent *live* (unsettled, unescaped) particles, not total ever spawned, since both painting-settle and hole-escape permanently retire particles from the sim.
