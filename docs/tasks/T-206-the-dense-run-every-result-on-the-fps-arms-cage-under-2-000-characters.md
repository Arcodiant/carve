---
description: >-
  The fps-arms cage opened, subdivided twice, checked, unwrapped and exported through the real carve with every result under 2,000 characters, the sizes tabled
---
# T-206 — The dense run: every result on the fps-arms cage under 2,000 characters

**Status:** In Progress

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
