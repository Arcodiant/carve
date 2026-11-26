---
description: >-
  define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
---
# T-200 — verbosity on the tools that return a mesh report

**Status:** In Progress

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

### Hand-off

By the first agent, Claude Opus 5.5, at the 60-call checkpoint. Code and tests committed; `cairn task T-200 verify` not yet run.

**Done.**
- Helpers in `src/Carve.Server/MeshToolSupport.cs`: `enum Verbosity { Summary, Silent, Full }`; `WithVerbosity(JsonObject? schema)` adds the `verbosity` property (string, enum of the three, description `VerbositySchemaText`); `ReadVerbosity(JsonElement? value, List<string> errors)` gives Summary when absent and adds `verbosity: expected "silent", "summary" or "full", got ...` otherwise; `WarningLines(warnings, level)` is T-197's grouping at summary and every `MeshWarning.ToString()` at full; `Quiet(result, level)` turns a success into `{"ok":true}` at silent and leaves an error whole; `Report(mesh, level)` and `Edit(..., addIds, level)` take the level (Edit returns `{"ok":true}` at silent after the edit is made).
- Adopting (as all fourteen do): wrap the schema in `WithVerbosity(...)`, add `JsonElement? verbosity = null` to `Run`, add `"verbosity"` to `UnknownArguments`, `var level = ReadVerbosity(verbosity, errors);` before the `Failed` return, pass `level` to `Report`/`Edit`/`WarningLines`, and return `Quiet(Ok(body), level)`.
- The fourteen tools adopted. `check` at silent answers `{"ok":<its own ok>}` (in `CheckTool.Run`), `export` at silent answers the JSON `{"ok":true}` (both of its success returns go through `Quiet`), as the planner read them.
- No tool description carries a verbosity sentence: one was tried and put `define_mesh` (3,830) and `check` (3,796) over 3,686, so the explanation is only the schema description, as the spec says. `undo`'s "Takes no arguments; any argument is a tool error." became "Takes only verbosity; any other is a tool error." (one character shorter).
- Tests: new `tests/Carve.Server.Tests/VerbosityToolTests.cs` (theory over the fourteen: silent exact and the same change made, summary = no argument, full = summary but every warning, error at silent equals error without, `"loud"` refused with the model unchanged; check's `{"ok":false}` on an open cube; a non-string value; full on the holed cage gives 798 open-edge lines from both `delete` and `check`; the tools/list guard). `DenseMeshBudgetTests` runs a second carve at silent beside the first, step for step.
- Pins moved (each now also lists `verbosity`): `CheckToolDefinitionTests`, `ExportToolDefinitionTests`, `SaveOpenToolDefinitionTests` (open only), `TargetToolTests` (define_mesh's properties), `DefineMeshDefinitionTests` (the published schema text and the property count 4 to 5).
- README: `## Dense meshes` gains the fourteen tools, the three levels, check's and export's silent answers.
- `dotnet test tests/Carve.Server.Tests --filter Tier=unit`: 1,112 passed.

**Left.** Run `cairn task T-200 verify` (full, so `ToolHarnessMatchesProcessTests` and `ToolFamiliesThroughProcessTests` against the real process); build the wrong-input table for `### As built` (e.g. `Quiet` not applied, `WarningLines` grouping at full, check silent answering true, the guard with a tool removed from `ToDo`); write `### As built`; `cairn lint`; `finish`.

**Surprising, for the person.**
- `displace` changes the model and returns a mesh report, but no M-23 task names it (not T-200's fourteen, T-201's nineteen or the readers). It is in the guard's `ToDo` table so the guard holds; someone must own it.
- The orchestrator asked for the levels in each short description; that cannot be done for `define_mesh` and `check` under 3,686 without moving text into a `Reference`, so it was not done anywhere (the spec's own rule). Raise if wanted otherwise.
- The planner's two readings (check's own ok, export's JSON) are implemented but not yet confirmed by the person.
