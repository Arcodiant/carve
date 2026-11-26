---
description: >-
  The fps-arms cage opened, subdivided twice, checked, unwrapped and exported through the real carve with every result under 2,000 characters, the sizes tabled
---
# T-206 — The dense run: every result on the fps-arms cage under 2,000 characters

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-199, T-201, T-202

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The milestone's content, shown on the real server: fps-arms' pipeline on its own cage, every answer under the stated size, with the measured sizes written down.

## Scope

- **`DenseRunProcessTests`** (`regression`, the real `carve` over stdio, reading `samples/`): `open` `arms-cage.carve`; `check`; `subdivide` twice; `check`; `move` one vertex; `save`; `open` the saved file; `export` skinned; then for `wrench.carve`, `cutter-src.carve`, `toolkit-src.carve` and `scanner-src.carve`: `open`, `unwrap`, `check`; then the two floods of `DenseMeshBudgetTests` (798 open edges, 280 overlaps). Each call is made at `summary`, `silent` and `full`.
  - `summary`: every result is `Budget` (2,000) characters or less. `silent`: exactly `{"ok":true}` (`check` per T-200). `full`: holds every warning (the open-edge flood lists 798 lines).
  - The cage after two subdivides reads `ok: true` from `check`, and the exported `.glb` holds 18,976 triangles.
  - It writes `dense-run.tsv` (call, level, characters) to the test's output folder; copy it to `artifacts/` and its table into M-23's file under `### Measured`, beside the planning numbers there.
- **Guards, made final here**: ~~delete `DescriptionLengthTests`' exception table~~ (struck: it never existed, `HelpToolTests` holds the one rule with no tool exempt); delete T-200's pending table; re-measure every description from the served `tools/list` and table it beside the result sizes; every topic `help` lists is named by some description.
- **Through an ordinary client**: with the built server registered in a Claude Code session, run the same sequence by hand once and record the token cost the client reports for `open` on the cage and `unwrap` on the toolkit, beside fps-arms' 12,000 and 5,000 to 9,000. Say plainly if this was not done.
- **`README.md`**: `## Dense meshes` gains the measured table and the budget; check the section against what the earlier tasks wrote and make it one account.
- A result over budget that no M-23 task covers (a flood from `problems`, clip or limit warnings, bone names) is reported to the person with its size; it is not trimmed here.

## Notes

- A real run: it fills context fastest, so it is a task of its own and changes no product code. A product fault it finds goes back to the task that owns the file, or to the person.
- No frame is drawn by this milestone (results are text), so there is no scenario project and no golden; the real process is reached through `server-regression`.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Claude Sonnet 5.5, by one agent.

**`DenseRunProcessTests`** (`regression`, the real `carve` over stdio, a fresh process and folder per level): the 26-call sequence of the Scope (open cage, check, subdivide x2, check, move vertex 0, save, open the saved file, export, delete of 200 faces and check, undo, project_uvs box on faces 0-99 and check, then open/unwrap/check for the four tool samples), each at `summary`, `silent` and `full`. `summary`: every result within `DenseMeshBudgetTests.Budget` (2,000). `silent`: exactly `{"ok":true}`, `check` its own `ok` as read at `summary`. `full`: at least as many warning lines as the count named at `summary` (798 open edges, 280 overlaps). The cage reads `ok: true` after two subdivides; the exported `.glb` (accessor counts) holds 18,976 triangles. It writes `dense-run.tsv` beside the test assembly, copied to `artifacts/M-23/T-206/dense-run.tsv` (untracked: `artifacts/` is git-ignored). A second fact, `The_served_descriptions_are_measured_and_none_is_over_the_limit`, writes `descriptions.tsv` from the served `tools/list`.

**Guards**: `VerbosityToolTests`' `ToDo` table (already empty) is deleted and the test renamed `Every_tool_has_verbosity_or_is_a_reader`. The description-length rule is `HelpToolTests` (no tool exempt; there never was an exception table in `DescriptionLengthTests`). "Every topic `help` lists is named by some description" is T-199's `HelpToolTests` check, unchanged.

**Measured** (characters; planning is `main` before M-23). No result is over budget, so no M-23 gap to report.

| Call | summary | silent | full | Planning |
|---|---|---|---|---|
| `open` cage | 244 | 11 | 244 | 1,156 |
| `check` cage | 730 | 11 | 730 | 1,434 |
| `subdivide` once | 319 | 11 | 319 | 373 |
| `subdivide` twice | 332 | 11 | 332 | 386 |
| `check` after two (ok: true) | 766 | 11 | 766 | 1,467 |
| `move` vertex 0 | 265 | 11 | 265 | |
| `save` | 149 | 11 | 146 | |
| `open` the saved file | 290 | 11 | 287 | |
| `export` skinned | 1,039 | 11 | 1,033 | 2,038 |
| `delete` 200 faces (798 open edges) | 1,119 | 11 | 40,389 | 41,310 |
| `check` after it | 873 | 12 | 40,143 | |
| `undo` | 186 | 11 | 186 | |
| `project_uvs` box (280 overlaps) | 734 | 11 | 734 | |
| `check` after it | 848 | 12 | 43,000 | 43,700 |
| `unwrap` wrench | 787 | 11 | 5,491 | 4,778 |
| `unwrap` cutter | 794 | 11 | 12,091 | 11,371 |
| `unwrap` toolkit | 790 | 11 | 16,801 | 16,086 |
| `unwrap` scanner | 786 | 11 | 8,799 | 8,089 |
| `check` wrench / cutter / toolkit / scanner | 475 / 752 / 492 / 487 | 11 | same | |

**Description lengths** (served `tools/list`, limit 3,686, 44 tools): define_mesh 3,684, project_uvs 3,682, get_mesh 3,678, define_material 3,678, select 3,658, render 3,609, unwrap 3,602, define_target 3,533, export 3,390, bake 3,361, open 3,324, knife 3,255, displace 3,142, save 3,126, define_clip 2,789, subdivide 2,734, define_part 2,692, loop_cut 2,541, plane_cut 2,476, merge_faces 2,455, bridge 2,405, extrude 2,343, mark_edges 2,334, move 2,275, check 2,195, set_uvs 2,148, set_material 2,087, get_parts 1,917, define_skeleton 1,904, delete 1,891, set_weights 1,848, get_weights 1,761, undo 1,713, weigh 1,632, render_glb 1,581, set_colour 1,390, keep_level 1,377, set_shading 1,157, get_skeleton 1,121, define_group 886, help 789, delete_target 686, get_clips 664, delete_clip 262. define_mesh and project_uvs are 2 and 4 under the limit.

**README**: `## Dense meshes` gains the measured table (summary against planning), the budget and the full-list size; the earlier tasks' paragraphs (grouping, smooth rule, `verbosity`, `unwrap`) were read against it and agree, so they stay.

**Through an ordinary client: NOT DONE by this agent.** The carve MCP registered in Claude Code is a published copy. The person would run `%LOCALAPPDATA%\carve-mcp\update.ps1 -Server carve` (carve-mcp's update script), `/mcp` reconnect, then `open` samples/arms-cage.carve and `unwrap` samples/toolkit-src.carve and read the token cost the client reports, to set beside fps-arms' 12,000 and 5,000 to 9,000. Expected from the table: about 244 and 790 characters.

**Failing on a wrong input**: during development the `full` check failed with "0 warning lines for 798 counted" when it matched the wrong wording, so it does bite. The budget and `silent` assertions were not mutated (not shown failing).

**Engine needs**: none found; no product code changed.
