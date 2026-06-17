# Approved Spec Deviations — V3.1

Formal deviations from [`architecure.md`](architecure.md) that are **intentional** for correctness or platform constraints.

---

## DEV-001 — Stream compaction uses indexed read + append (not `ConsumeStructuredBuffer`)

| Field | Value |
|-------|-------|
| **Spec reference** | §5.3 `ExecuteInternalFluidIntegration` — `FluidParticle particle = _InternalConsume.Consume();` |
| **Implementation** | `StreamCompactionPingPong.compute` reads `_DensityWritableCache[particleIndex]` and appends to `_InternalAppend` / `_FallingAppend` |
| **Reason** | Parallel `Consume()` in a `[numthreads(64,1,1)]` kernel does not define a deterministic consumption order across warps; multiple threads consuming one append buffer causes races. Indexed pass + append output preserves two-pass SPH correctness and ping-pong counter semantics. |
| **Approved** | 2026-06-02 |
| **Alternative** | Future: single-threaded consume dispatch (not scalable) or prefix-sum compaction pass |

**Validation:** Play Mode stability test — internal count bounded over N frames.

---

## DEV-002 — `QuantizedBakeParticle` C# layout vs GPU `uint2` packs

| Field | Value |
|-------|-------|
| **Spec reference** | §2.1 separate `ushort` fields |
| **GPU** | `uint2 Packed0`, `uint2 Packed1` (16 bytes total) |
| **C#** | Eight `ushort` fields (16 bytes total) |
| **Reason** | GPU packer uses bit-packing in HLSL; CPU decode uses `QuantizedBakeDecoder` matching GPU bit layout for disk playback |
| **Approved** | 2026-06-02 |

---

## DEV-003 — RK4 pendulum: scalar integrator, not Burst `IJob` (interim)

| Field | Value |
|-------|-------|
| **Spec reference** | §7 `Rk4SystemSolver.cs (Burst Vectorized)` |
| **Implementation** | `PendulumRk4Integrator` static scalar; `PendulumRk4Job` added in gap closure |
| **Reason** | Initial Burst `float2` extern functions failed BC1064; scalar math is Burst-safe |
| **Status** | **Closed** — `PendulumRk4Job` wired in `PendulumSimulator.UseBurstRk4Job` |

---

## DEV-004 — Eulerian field: no wind/advect in v1 closure

| Field | Value |
|-------|-------|
| **Spec reference** | §1 ambient Eulerian voxel drag field |
| **Implementation** | Clear → **Advect** (decay) → scatter → apply |
| **Remaining** | External wind injection, 3D grid indexing (currently hash-mod flat grid) |
| **Status** | **Partial** — `AdvectDragGridKernel` + decay wired; wind injection and 3D grid remain |
