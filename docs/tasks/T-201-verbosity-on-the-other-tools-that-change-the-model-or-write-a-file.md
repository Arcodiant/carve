---
description: >-
  The parts, style, UV, skeleton, weight, clip, target, save and bake tools take verbosity, silent answering only ok
---
# T-201 — verbosity on the other tools that change the model or write a file

**Status:** In Progress

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-200

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

Every other tool that changes the model or writes a file takes `verbosity`, so a whole script can run with one-word answers.

## Scope

- **Tools**: `define_part`, `keep_level`, `define_group`, `set_colour`, `define_material`, `set_material`, `set_shading`, `mark_edges`, `set_uvs`, `project_uvs`, `define_skeleton`, `set_weights`, `weigh`, `define_clip`, `delete_clip`, `define_target`, `delete_target`, `save`, `bake`, and `displace` (added by M-29). Not `unwrap` (T-202 owns `UnwrapTool.cs`).
- Each adopts T-200's schema fragment and reader as T-200's `### As built` says: `silent` is exactly `{"ok":true}`; `summary`, the default, is byte for byte today's result; `full` is `summary` unless the tool shortens a list today (then the whole list). Errors whole at every level. No tool description grows.
- Remove the twenty from T-200's guard table.
- **Tests** (`unit`): one theory over the twenty, each called on a model that makes it succeed: `silent` exactly `{"ok":true}` with the change made (read back by the matching `get_*`, or the file on disk for `save` and `bake`); the default unchanged; an error at `silent` whole; a bad value.
- **`README.md`**: the list of tools that take `verbosity`, in `## Dense meshes`.

## Notes

- Mechanical: twenty tools on one helper. If a tool's result cannot go through the helper as it stands, widen the helper; do not write a second one.
- Runs beside T-202; the two share only T-200's guard table (each removes its own rows; keep both sides of that conflict).
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

By one agent, Claude Sonnet 5.5, in one run.

The twenty tools (`define_part`, `keep_level`, `define_group`, `set_colour`, `define_material`, `set_material`, `set_shading`, `mark_edges`, `set_uvs`, `project_uvs`, `define_skeleton`, `set_weights`, `weigh`, `define_clip`, `delete_clip`, `define_target`, `delete_target`, `save`, `bake`, `displace`) adopt T-200's helper as its `### As built` says: `WithVerbosity(...)` on the schema, `JsonElement? verbosity = null` on `Run`, `"verbosity"` in the known arguments (the `Known` arrays of `define_material` and `bake` too), `ReadVerbosity` before the `Failed` return, and `Quiet(Ok(...), level)` on the success. No second helper and no change to `Quiet`, `Report` or `WarningLines`. Two small widenings of what T-200 gave: `PathAndMeshArguments` (only `save` calls it) takes `verbosity` and gives back the level; and `keep_level` names its level `how`, because its own argument is `level`. `displace` is the one tool of the twenty that shortens a list, so it passes `level` to `Report` and to `WarningLines` and `full` gives every open edge one per line; the other nineteen have no list to shorten, so `full` is `summary`. No tool description changed (none said "takes no arguments"); `CarveServer.cs` is untouched.

**Tests.** `VerbosityToolTests`: `ToDo` is now only `unwrap` (T-202's row); the theory has twenty more rows (setup, a success, a tool error), so 34 rows in all. Each row checks `silent` is exactly `{"ok":true}` with the same change made, `summary` equals no argument, `full` as before, an error at `silent` whole, `"loud"` refused with the model unchanged. The state read back is every `get_*` (mesh, parts, skeleton, weights, clips), the model saved to a file (which shows shading, hard edges and targets that no `get_*` gives) and the folder's files, all depth, which holds `save` and `bake`. `bake`'s per-map `"ms"` is normalised, being a timing. A new fact, `Full_on_displace_lists_every_open_edge`.

**Pins that moved**, each asserting the same thing with `verbosity` among the listed properties: `PartToolsTests` (define_part's and keep_level's schemas), `SaveOpenToolDefinitionTests` (save's), `TargetToolTests` (define_target's). No number of the 3, the 2,000 or the 3,686 moved; no committed sample was edited.

**README.** `## Dense meshes` gains one paragraph, after T-200's: the twenty tools take `verbosity` on the same levels, what `silent` does for `save` and `bake`, that `full` equals `summary` for them, and the readers that take none. The lines for help, check, open, export and unwrap are the siblings'.

**Each check failing on a wrong input** (source changed, `VerbosityToolTests` run, then restored):

| Wrong input | What fails |
|---|---|
| `Quiet` returns the result unchanged | the theory, 33 of 34 rows (all but `check`, which answers its own ok) |
| `"verbosity"` left out of `define_material`'s known arguments | the `define_material` row (the call is refused as an unknown argument) |
| `displace` passes no level to `Report` and `WarningLines` | `Full_on_displace_lists_every_open_edge` |
| a tool's `verbosity` left out of its schema | the guard `Every_tool_has_verbosity_or_is_a_reader_or_is_still_to_do` (it held for the twenty before their rows left `ToDo`) |

Not checked: no frame was drawn or judged; the last row is the guard's own, shown by T-200's table (`displace` removed from `ToDo`), not re-run here.
