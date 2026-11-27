---
description: >-
  define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
---
# T-200 — verbosity on the tools that return a mesh report

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-198

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The tools that answer with a mesh report take `verbosity`, so a script can run them silently and an agent can ask for the whole list.

## Scope

- **The argument**, `verbosity`: `"silent"`, `"summary"` (the default) or `"full"`; any other value is a tool error naming the three. One schema fragment and one reader in `MeshToolSupport`, used by every tool (T-201 and T-202 use them too).
  ```
  verbosity: "silent"     {"ok": true}
  verbosity: "summary"    today's fields, warnings grouped (T-197)     (default)
  verbosity: "full"       as summary, every warning one per line
  ```
- **Tools here**: `define_mesh`, `move`, `extrude`, `delete`, `merge_faces`, `bridge`, `plane_cut`, `loop_cut`, `knife`, `subdivide`, `undo`, `open`, `check`, `export`.
- **`silent`** (the person, 2026-11-24: "silent says only ok"): the result is exactly `{"ok":true}`, no counts, no ids, no warnings; the edit is made and is one undo step as ever. A tool error is returned whole at every level. Two readings of the planner's, to confirm with the person before landing: `check` at `silent` answers `{"ok": <its own ok>}`, since `true` over a broken mesh would mislead; `export` at `silent` answers the JSON `{"ok":true}` although its result is otherwise text.
- **`full`**: every `MeshWarning` one per line as before T-197, and nothing else changed. With no argument a result is byte for byte what it is without this task.
- **The explanation goes in the argument's schema description** (not cut by the client), not in tool descriptions, which must not grow (T-199's ceilings).
- **Guard test** (`unit`): every tool in tools/list either has `verbosity` or is one of `get_mesh`, `get_parts`, `get_skeleton`, `get_weights`, `get_clips`, `select`, `render`, `render_glb`, `help` (absent until T-199 lands; the guard must pass either way), or is in a table of tools still to do (T-201's nineteen and `unwrap`), which T-201 and T-202 empty.
- **Tests** (`unit`, one theory over the fourteen): `silent` is exactly `{"ok":true}` and the model changed; an error at `silent` is the same text as without it; `summary` equals no argument; `full` on the holed cage of `DenseMeshBudgetTests` lists 798 open-edge lines; a bad value. `DenseMeshBudgetTests` gains the `silent` column (every result exactly `{"ok":true}`, `check` per the reading above).
- **`README.md`**: `verbosity` in `## Dense meshes`.

## Notes

- **Decides a mechanism others build on**: write the helper names and how a tool adopts them in `### As built` for T-201 and T-202.
- Runs beside T-199: do not edit `CarveServer.cs` or add a tool.
- `ToolHarnessMatchesProcessTests` and `ToolFamiliesThroughProcessTests` (`regression`) compare results with the real process, hence `Verify: full`.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

By two agents in turn, both Claude Opus 5.5: the first wrote the code and the tests and handed off at its 60-call checkpoint; the second recorded the person's rulings below, ran `verify`, built the table below and wrote this.

**The person's rulings (2026-11-26).**
- The planner's two readings are confirmed: `check` at `silent` answers its own `ok` (`{"ok":false}` on a broken mesh), and `export` at `silent` answers the JSON `{"ok":true}` although its result is otherwise text.
- `displace` (it changes the model and returns a mesh report, but no M-23 task named it) goes to T-201, not here. It stays in the guard's `ToDo` table, labelled as T-201's.
- `verbosity` is explained only in the argument's schema description, and no tool description grows. A sentence in each description was tried and put `define_mesh` (3,830) and `check` (3,796) over 3,686. `undo`'s "Takes no arguments; any argument is a tool error." became "Takes only verbosity; any other is a tool error." so it stays true (one character shorter).

**The helper, for T-201 and T-202** (all in `src/Carve.Server/MeshToolSupport.cs`):
- `enum Verbosity { Summary, Silent, Full }`.
- `WithVerbosity(JsonObject? schema)` adds the `verbosity` property (string, enum `silent`/`summary`/`full`, description `VerbositySchemaText`) to the schema, or to an empty object schema when null, and returns it.
- `ReadVerbosity(JsonElement? value, List<string> errors)` gives `Summary` when absent; any other value than the three adds `verbosity: expected "silent", "summary" or "full", got ...` to `errors`.
- `WarningLines(warnings, level)`: T-197's grouping at `summary`, every `MeshWarning.ToString()` one per line at `full`.
- `Quiet(result, level)`: at `silent` a success becomes exactly `{"ok":true}`; a tool error is returned whole.
- `Report(mesh, level)` and `Edit(store, tool, mesh, edit, addIds, level)` take the level; `Edit` makes the edit (one undo step as ever) and then answers `{"ok":true}` at `silent`, after its refusals.

How a tool adopts it, as the fourteen do: wrap the schema in `WithVerbosity(...)`; add `JsonElement? verbosity = null` to `Run` and `"verbosity"` to its `UnknownArguments`; `var level = ReadVerbosity(verbosity, errors);` before the `Failed` return; pass `level` to `Report`/`Edit`/`WarningLines`; return `Quiet(Ok(body), level)`. What each level returns: `silent` exactly `{"ok":true}` (check: its own `ok`); `summary`, the default, byte for byte the result without the argument; `full`, the summary with every warning one per line; a tool error whole at every level. T-201 and T-202 move their tools out of `ToDo` in `VerbosityToolTests` as they adopt it.

**Tools.** `define_mesh`, `move`, `extrude`, `delete`, `merge_faces`, `bridge`, `plane_cut`, `loop_cut`, `knife`, `subdivide`, `undo`, `open`, `check`, `export`. `CarveServer.cs` is not touched and no tool added.

**Tests.** New `tests/Carve.Server.Tests/VerbosityToolTests.cs`: a theory over the fourteen (silent exact and the same change made, summary equals no argument, full is summary with every warning, an error at silent equals it without, `"loud"` refused with the model unchanged); check's `{"ok":false}` on an open cube; a non-string value refused; `full` on the holed cage of `DenseMeshBudgetTests` gives 798 open-edge lines from both `delete` and `check`; the tools/list guard (every tool has `verbosity`, or is one of the readers, `help` included whether or not T-199 has landed, or is in `ToDo`). `DenseMeshBudgetTests` runs a second carve at `silent` beside the first, step for step: every result exactly `{"ok":true}`, check its own `ok`.

**Pins that moved**, each asserting the same thing with `verbosity` now among the listed properties: `CheckToolDefinitionTests` (check's closed schema), `ExportToolDefinitionTests` (export's), `SaveOpenToolDefinitionTests` (open's only; `save` is T-201's), `TargetToolTests` (define_mesh's properties), `DefineMeshDefinitionTests` (the published schema text, and its property count 4 to 5). No number of the 3, the 2,000 or the 3,686 moved; no committed sample was edited.

**README.** `## Dense meshes` gains a paragraph: the fourteen tools take `verbosity`, the three levels in a sketch, check's and export's silent answers, a tool error whole at every level and any other value an error.

**Each check failing on a wrong input** (the source changed, the classes `VerbosityToolTests`, `DenseMeshBudgetTests` and the moved pins run, then restored):

| Wrong input | What fails |
|---|---|
| `Quiet` returns the result unchanged | `Each_level_answers_as_ruled` (13 of 14 rows; `check` answers its own ok), `DenseMeshBudgetTests` |
| `Edit` answers `{"ok":true}` at silent before making the edit | `Each_level_answers_as_ruled` (the 8 edits: model unchanged), `DenseMeshBudgetTests` |
| `Edit` answers `{"ok":true}` at silent before its refusals | `Each_level_answers_as_ruled` (the 8 edits: error at silent differs) |
| `WarningLines` groups at `full` | `Full_on_the_holed_cage_lists_every_open_edge` |
| `WarningLines` lists every warning at `summary` | `DenseMeshBudgetTests` (over budget) |
| `ReadVerbosity` accepts a bad value | `Each_level_answers_as_ruled` (all 14), `A_value_that_is_not_a_string_is_a_tool_error` |
| `check` at silent answers `{"ok":true}` | `Check_at_silent_answers_its_own_ok_on_a_broken_mesh`, `DenseMeshBudgetTests` |
| `displace` removed from `ToDo` | `Every_tool_has_verbosity_or_is_a_reader_or_is_still_to_do` |
| `WithVerbosity` adds nothing | all six moved pins and the guard |

`Quiet` turning a tool error into `{"ok":true}` fails nothing: no caller passes it an error (each tool returns its errors before `Quiet`), so the guard in `Quiet` is defence only, and the error-at-silent row above is what holds the rule. `summary` equals no argument by construction, so the summary row is held by `DenseMeshBudgetTests`' budget, not by the theory.

**Checked.** `cairn task T-200 verify` (full): VERIFY PASS, `server-regression` (with `ToolHarnessMatchesProcessTests` and `ToolFamiliesThroughProcessTests`) and the scenarios run against the real process. Not checked: no frames were judged, since nothing here draws.
