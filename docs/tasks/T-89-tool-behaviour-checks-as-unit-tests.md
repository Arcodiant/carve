---
description: >-
  Non-rendering tool behaviour checks run on the harness as unit tests; server-regression keeps process, extension and render checks
---
# T-89 — Tool behaviour checks as unit tests

**Status:** In Progress

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** T-87

**Verify:** server-unit, server-regression

## Goal

Tool behaviour that needs no device runs as `unit` tests on T-87's harness. `server-regression` keeps only what
needs the real process or the GPU.

## Scope

- Move the regression classes that never call `render` (20 of 28 at planning): `CheckToolTests`,
  `ClipLibraryToolTests`, `DeleteToolTests`, `ExportLimitsToolTests`, `ExportToolTests`, `GetWeightsToolTests`,
  `HoledFaceToolTests`, `KnifeToolTests`, `LoopCutToolTests`, `MergeBridgeToolTests`, `PlaneCutToolTests`,
  `SaveOpenToolTests`, `SelectToolTests`, `ShadingToolTests`, `SkeletonProblemListTests`, `SkeletonToolTests`,
  `SkinnedExportToolTests` and `SubdivideToolTests`. Also move the non-rendering cases of the mixed classes
  (`EditToolTests`, `DefineMeshTests`, `CarveFileVersion2Tests`).
- Keep in `regression`: `StdioTests`, `LoaderTests`, `DebugExtensionTests`, `RenderTests`, `BoneRenderToolTests`,
  `WeightsLookTests`, the rendering cases of `NamedMeshTests`, and anything that depends on the working directory
  or the file system as the process sees it, unless the harness reproduces that exactly.
- Keep at least one end-to-end case per tool family in `regression`, through the real process, so the
  wiring in `Program.cs` stays covered.
- Same assertions on the same inputs; do not loosen any. Unit cases are about 1 s or less each.
- Coordinate with T-88 on shared classes: rebase onto the milestone branch rather than merging.

## Notes
