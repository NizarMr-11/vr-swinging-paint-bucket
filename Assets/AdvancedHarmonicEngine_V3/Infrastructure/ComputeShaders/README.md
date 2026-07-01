# Compute Shaders

| Asset | Role |
|-------|------|
| `PbfSolver.compute` | Position Based Fluids for open-top cylinder |
| `WcsphDensity.compute` | WCSPH density pass (formerly StreamCompaction) |
| `WcsphIntegration.compute` | WCSPH integration (formerly StreamCompactionIntegrate) |
| `ContainerRigidCarry.compute` | Rigid-body carry in oriented cylinder |
| `Include/OpenTopCylinderBoundary.hlsl` | Shared floor/wall/spill boundary rules |
| `Include/SphCommon.hlsl`, `SphNeighborQuery.hlsl` | Shared SPH structs and neighbor iteration |

Spill particles are flagged in `spillTransferFlags` (not `_GradSqSum`).
