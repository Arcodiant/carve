---
description: >-
  Description, worked-example and report-shape checks run on the harness as unit tests
---
# T-88 — Tool definition checks as unit tests

**Status:** Done

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** T-87

**Verify:** server-unit, server-regression

## Goal

The checks on what the tool list says run as `unit` tests on T-87's harness. These are descriptions, schemas,
worked examples that run clean, and every "Returns JSON" shape naming its fields. Most of them start a fresh
`carve` per `[InlineData]` row today, only to read `tools/list`.

## Scope

- Find them across the regression classes: tests built on `ListToolsAsync` and `Examples(...)`, such as
  `EditToolTests.Every_worked_example_in_a_description_runs_on_the_cube_closed_and_warning_free` and
  `Every_edit_report_shape_in_a_description_names_deleted_and_split`, and their peers in `SelectToolTests`,
  `DefineMeshTests`, `NamedMeshTests`, `SubdivideToolTests` and the cut tools.
- Move each to a `unit` class on the harness with the same assertions on the same inputs. Do not loosen any.
- If a worked example renders, it stays in `regression`; say which ones did in the notes.
- Every test keeps its `Tier` trait. A moved class has no `regression` cases left behind by accident.

## Notes

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`).

44 test methods (46 cases: `Every_worked_example_in_a_description_...` is a 3-row theory) moved, text unchanged apart from
`CarveProcess` -> `ToolHarness` (`new ToolHarness(workingDirectory: Root)` where it had a folder), each into a new
`unit` class beside its family, and deleted from the `regression` class. No class was emptied, so none was deleted.
No assertion, tolerance or input changed; all 46 cases pass on the harness (9 s for the lot).

| From | To | Moved |
|---|---|---|
| `CheckToolTests` | `CheckToolDefinitionTests` | 3: worked example to the character, closed schema, description teaches the fixes |
| `DeleteToolTests` | `DeleteToolDefinitionTests` | 1 |
| `EditToolTests` | `EditToolDefinitionTests` | 7: every worked example (3 rows), edit report shape names deleted/split, render names every edit, undo example, move example, get_mesh sample, each schema |
| `ExportToolTests` | `ExportToolDefinitionTests` | 2 |
| `GetWeightsToolTests` | `GetWeightsToolDefinitionTests` | 1 |
| `KnifeToolTests` | `KnifeToolDefinitionTests` | 1 |
| `LoopCutToolTests` | `LoopCutToolDefinitionTests` | 2 |
| `MaterialToolTests` | `MaterialToolDefinitionTests` | 1 |
| `MergeBridgeToolTests` | `MergeBridgeToolDefinitionTests` | 4 (merge, bridge, README cutting examples, merge input is loop_cut's output) |
| `NamedMeshTests` | `NamedMeshDefinitionTests` | 2 |
| `PlaneCutToolTests` | `PlaneCutToolDefinitionTests` | 1 |
| `ProjectUvsToolTests` | `ProjectUvsToolDefinitionTests` | 1 |
| `RenderTests` | `RenderDefinitionTests` | 3 (descriptions only; none calls render) |
| `SaveOpenToolTests` | `SaveOpenToolDefinitionTests` | 2 |
| `SelectToolTests` | `SelectToolDefinitionTests` | 4 |
| `SubdivideToolTests` | `SubdivideToolDefinitionTests` | 1 |
| `UnwrapToolTests` | `UnwrapToolDefinitionTests` | 1 (schema) |
| `UvToolTests` | `UvToolDefinitionTests` | 2 |
| `DefineMeshTests` | `DefineMeshDefinitionTests` | 3 (worked example, schema and rules, typed schema) |
| `SkeletonToolTests` | `SkeletonToolDefinitionTests` | 1 |
| `HoledFaceToolTests` | `HoledFaceToolDefinitionTests` | 1 |

Classes that derived from `FileToolTests` (whose `StartAsync` starts a `CarveProcess`) derive from `TempFolderTests` in
their definition class and carry private harness copies of `StartAsync`, `Ok` and `Refused`; `FileToolTests` itself is
untouched, for T-89. Each new class copies only the helpers its tests use.

**Worked examples that render: none moved, so none stayed behind for that reason.** `CheckerArgumentTests.The_checker_examples_upright_list_is_unflipped_and_its_mirrored_one_flipped` runs render's checker example and stays in `regression`.
Not moved because their subject is behaviour, with a description line asserted in passing (T-89's): `SubdivideToolTests.After_a_delete_each_level_..._and_the_description_says_so`,
`CheckToolTests.It_changes_nothing_...` (uses the move example as setup), `KnifeToolTests.An_off_surface_point_...`, `LoopCutToolTests.Two_fractions_...`, `PlaneCutToolTests.Keep_both_...`, `UnwrapToolTests.The_barrel_...`, `LoaderTests.An_extensions_tool_reaches_tools_list` (extension hooks are not on the harness).
M-19's classes (`ClipLibraryToolTests`, `RenderGlbTests`, `PieceExportToolTests`, `NotPlanarFoldToolTests`, ...) hold no tools/list, description or schema checks, so nothing moved from them.

Not checked: the moves were made by script and read through only where the build or run flagged something; whole `server-regression` ran only through `verify`/`finish`. The unit classes were not compared with the process-run originals beyond both passing.
