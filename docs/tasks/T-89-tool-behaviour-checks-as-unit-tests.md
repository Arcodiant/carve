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

### As built

Run on the model Sonnet 5.5 (`claude-sonnet-5-5`).

**Mechanism.** A class moves by its start line (`new CarveProcess([], workingDirectory: Root)` to
`new ToolHarness(workingDirectory: Root)`) and its `Tier` trait; no assertion, input or tolerance changed. `FileToolTests.StartAsync`
now starts the harness; `StartProcessAsync` is the process twin for the regression classes that render
(`CheckerArgumentTests`, `MaterialRenderTests`, `PbrEqualityTests`, the process half of `CarveFileVersion2Tests`). A one-method
interface `IToolCaller` (`CarveProcess` and `ToolHarness` both implement it) lets `CarveFileVersion2Tests`' helpers
(`Box`, `Bones`, `Wave`, `BuildChainAsync`, `LookAsync`, now `internal`) serve both. T-88's definition tests (`Examples(...)`,
`ListToolsAsync` descriptions and schemas) were left in place, bodies untouched; those inside converted classes now run on the
harness (they work there) and stay for T-88 to cut.

**Moved whole to `unit`** (the spec's list): `CheckToolTests`, `ClipLibraryToolTests`, `DeleteToolTests`, `ExportLimitsToolTests`,
`ExportToolTests`, `GetWeightsToolTests`, `HoledFaceToolTests`, `KnifeToolTests`, `LoopCutToolTests`, `MergeBridgeToolTests`,
`PlaneCutToolTests`, `SaveOpenToolTests`, `SelectToolTests`, `ShadingToolTests` (its regression half; the unit half was already),
`SkeletonProblemListTests`, `SkeletonToolTests`, `SkinnedExportToolTests`, `SubdivideToolTests`. Also whole: `EditToolTests`
(its `render` inline case is the unknown-argument refusal, which happens before the engine and passes on the harness).
M-19's and later non-rendering tool classes, whole: `NotPlanarFoldToolTests`, `PieceExportToolTests` (get_clips is in
`ClipLibraryToolTests`), plus `ProjectUvsToolTests`, `UnwrapToolTests`, `UvToolTests`, `MaterialToolTests`,
`MaterialExportToolTests` (not in the list; none renders, all pass on the harness with `ToolFolder` resolving `define_material` paths).
`PieceExportTests`, `FaceColourTests`, `ClipsExportTests` were already `unit`.

**Mixed classes, split.** `DefineMeshTests` is unit; its one case that reads the mesh through a render caption
(`A_failed_call_keeps_the_previous_mesh...`) moved to a new regression class `DefineMeshRenderTests` on the process, whole.
`CarveFileVersion2Tests` stays regression with its two render/new-process cases; its three save/open-by-mesh cases moved to
new unit class `CarveFileVersion2ToolTests`. `NamedMeshTests` stays regression with its render cases and the schema case
(`Every_tool_refuses_an_unknown_argument...`, T-88's); five non-rendering cases moved to new unit class `NamedMeshToolTests`
(refuse-to-guess theory, unknown/empty name, colon name, undo across meshes, replace in place and open by name).

**Kept in regression, and why.** `StdioTests`, `LoaderTests`, `DebugExtensionTests`, `RenderTests`, `BoneRenderToolTests`,
`WeightsLookTests`, `UvLayoutLookTests`, `CheckerArgumentTests`, `CheckerLookTests`, `MaterialRenderTests`, `PbrEqualityTests`,
`RenderReleaseTests`, `RenderGlbTests` (render and render_glb need the engine; `RenderPumpTests` was already `unit`), the process half of
`CarveFileVersion2Tests` and `NamedMeshTests`, `DefineMeshRenderTests`, `ToolHarnessMatchesProcessTests`.

**End-to-end per family, kept in regression.** `ToolHarnessMatchesProcessTests` (T-87: define_mesh, move, get_mesh, check, undo
and the tool list) plus the new `ToolFamiliesThroughProcessTests`: for each family (edit, cut, delete and select, shading, uvs,
skeleton/weights/clips, save/open/export) the same calls go to the real process and to a harness, each in its own working folder;
every result must be the same JSON and every written file byte-equal.

**Timings (this machine, loaded by sibling runs).** Before: server-unit 370 tests, 4 s test time / 9 s wall; server-regression 603 tests,
10 min 30 s. After: server-unit 746 tests, 17 s test time / 20 s wall (no case at or over 1 s); server-regression 228 tests, 7 min 6 s (7 min 10 s wall).

**Not checked.** `PbrReleaseTests` and `UvExportTests` (regression, no process, not in the spec) were left as they are, not examined for a unit move. No scenario or frames (none needed). The `finish` queued run was not compared case-by-case with the old one beyond the
counts: 370 + 603 = 973 before; after, unit 746 + regression 228 = 974, the difference being the one added end-to-end test (and the
new classes holding cases moved, not copied).
