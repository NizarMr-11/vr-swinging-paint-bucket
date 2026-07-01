# Simulation Passes

Each pass receives `HarmonicSimulationContext` (active count, dt) and a `HarmonicPipelineController` host for GPU bindings.

| Pass | Responsibility |
|------|----------------|
| `HarmonicSimulationFrameRouter` | Frame branching: bucket SPH, open-top cylinder, world falling |
| `SpatialHashBuildPass` | Grid clear/generate, radix/bitonic sort, cell ranges |
| `OpenTopCylinderPbfSimulationPass` | PBF predict/density/lambda/solve/apply + spill append |
| `OpenTopCylinderRigidBodyCarryPass` | Rigid rotation carry for in-cylinder particles |
| `FallingWorldSimulationPass` | World-space falling fluid integration + canvas hits |

Deferred (still on controller): bucket WCSPH path, diagnostics partials.
