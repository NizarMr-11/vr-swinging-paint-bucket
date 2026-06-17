# Hardware Requirements — Harmonic Drip Engine V3

Reference targets for the GPU paint simulation on `anas-sandbox`.

## Minimum (Low tier — 100k particles)

| Component | Spec |
|-----------|------|
| GPU | DirectX 11 / Vulkan compute, 4 GB VRAM |
| CPU | 4-core, 2018+ |
| RAM | 8 GB |
| Target | 60 FPS @ 100k live particles |

## Recommended (Medium tier — 500k particles)

| Component | Spec |
|-----------|------|
| GPU | RTX 2060 / RX 6600 or better, 6 GB VRAM |
| CPU | 6-core |
| RAM | 16 GB |
| Target | 60 FPS @ 500k live particles |

## High (1M particles)

| Component | Spec |
|-----------|------|
| GPU | RTX 3070 / RX 6800, 8 GB+ VRAM |
| RAM | 16 GB |
| Target | 30–60 FPS @ 1M particles |

## Cinematic bake (5M particles)

| Component | Spec |
|-----------|------|
| GPU | RTX 4080 / RX 7900 XTX, 12 GB+ VRAM |
| RAM | 32 GB |
| Target | 15–30 FPS live or offline bake without TDR |

## Controls (desktop)

| Key | Action |
|-----|--------|
| Space | Start simulation |
| P | Pause |
| R | Reset |
| 1–4 | Quality tier (Low / Med / High / Cinematic) |
| B | Bake record mode |
| V | Bake playback mode |
| L | Live mode |

## Stress tests

Run manually from Unity Test Runner with filter `Category=Stress`. Not intended for CI.
