# Infrastructure / Management

`HarmonicPipelineController` owns the simulation loop, serialized tuning, and GPU buffer lifetime.

## Key types

- **HarmonicPipelineController** — main MonoBehaviour entry point
- **OpenTopCylinderSettings** — open-top cylinder bounds and SPH tuning
- **Gpu/** — `HarmonicGpuResourcePool`, shader property IDs, SoA bind helpers
- **SimulationPasses/** — extracted per-pass execution (see nested README)

Golden-frame regression: `Assets/Tests/PlayMode/HarmonicGoldenFrameBaseline.json` (100 frames, 5% tolerance).
