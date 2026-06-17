# vr-swinging-paint-bucket

Unity-based simulation of swinging paint bucket motion and canvas drawing for the Virtual Reality course project.

## Harmonic GPU Pipeline (V3)

AdvancedHarmonicEngine_V3 provides a GPU SPH paint simulation with impasto canvas rendering, bake/playback modes, and quality presets (100k–5M particles).

See [docs/architecure.md](docs/architecure.md), [docs/full-implementation-plan.md](docs/full-implementation-plan.md), [docs/hardware-requirements.md](docs/hardware-requirements.md), and **[docs/harmonic-engine-api.md](docs/harmonic-engine-api.md)** (Unity integration guide).

### Quick start

**Classic paint bucket (original flow)** — `Assets/Scenes/ClassicPaintSimulation.unity`

1. Open the scene and press **Play**
2. Press **Space** to start, **P** pause, **R** reset
3. Keys **1–4** switch quality tier; **B** bake record, **V** playback, **L** live mode; **S** save canvas to Pictures

**Harmonic Engine lab (GPU pipeline + timeline)** — `Assets/Scenes/HarmonicEngineLab.unity`

1. Open the scene and press **Play**
2. Enter particle count + duration in the setup prompt; toggle **Save calculation** for record/scrub workflow
3. See [docs/scenes.md](docs/scenes.md) and [docs/harmonic-engine-api.md](docs/harmonic-engine-api.md)

Legacy scene names: `MainSimulation.unity` (classic), `AnasScene.unity` (engine lab).

