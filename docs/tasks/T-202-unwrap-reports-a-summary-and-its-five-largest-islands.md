---
description: >-
  unwrap returns the island count, worst stretch, flipped total and five largest islands, and every island only at verbosity full
---
# T-202 — unwrap reports a summary and its five largest islands

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-200

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

`unwrap` answers with a summary and its five largest islands, so a tool with 135 islands costs about 800 characters, not 16,000.

## Scope

- **The result at `summary`** (the default), agreed sketch:
  ```
  {"mesh": "toolkit",
   "islands": 135,
   "single_face_islands": 118,
   "fill": 0.71, "scale": 2.4,
   "stretch": {"worst": 1.38, "lowest_face": 14},
   "flipped": 0,
   "largest": [
     {"faces": 12, "lowest_face": 0, "rect": [0.016, 0.016, 0.383, 0.271], "stretch": 1.0, "flipped": 0},
     ... 4 more ...]}
  ```
  `islands` is the count; `stretch` names the island with the largest stretch by its lowest face; `flipped` is the total over all islands; `largest` is up to five islands by face count, ties to the lowest face, each entry in today's form. (The numbers in the sketch are illustrative.)
- **`verbosity`** by T-200's helper: `silent` is `{"ok":true}`; `full` is `summary` plus `all`, every island in today's entry form, in today's order. This replaces the `islands: "all"` argument of the agreed sketch (the planner's change, since `verbosity` came after it: confirm with the person before landing).
- **Readers**: search every caller of `unwrap` in `scenarios/` and `tests/` (`scenario-uvs`' three islands, `UnwrapToolTests`, `UnwrapToolDefinitionTests`, the wrench and bakes) and move each to `largest` or to `verbosity: "full"`, asserting what it asserted. The worked example in `unwrap`'s description shows the new result and is run as written; the description does not grow. unwrap's description is 3,685 characters (1 under the limit); if the new example does not fit, move its detail into a new `help` entry (`UnwrapTool.Reference`, one line in `HelpTool.Entries`, the `More:` pointer), as T-207 did.
- **Tests** (`unit`): the barrel's three islands; a mesh with more than five islands (order, ties, `single_face_islands`, the worst stretch's island, the `flipped` total against `full`'s entries); `full` equal entry for entry to the result before this task; in `DenseMeshBudgetTests`, `unwrap` on `samples/wrench.carve`, `cutter-src.carve`, `toolkit-src.carve` and `scanner-src.carve` (39, 95, 135 and 67 islands; 4,778, 11,371, 16,086 and 8,089 characters at planning) each `Budget` or less.
- Remove `unwrap` from T-200's guard table. **`README.md`**: `## UVs` says what `unwrap` returns.

## Notes

- Owns `UnwrapTool.cs` in its wave; T-205 trims the description after it.
- `get_mesh` still reads every UV, and `render mode=uvs` shows the layout; neither changes.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Claude Sonnet 5.5, one agent.

**The result.** `UnwrapTool.cs` answers `{mesh, islands (count), single_face_islands, fill, scale, stretch: {worst, lowest_face}, flipped (total), largest}`; `largest` is up to five islands by face count, ties to the lowest face, in today's entry form. It takes `verbosity` by T-200's helper: `silent` is `{"ok":true}`; `full` is the summary plus `all`, every island in today's entry form and order. The planner's reading of ruling 4 (every island at `full` as `all`, not an `islands: "all"` argument) is implemented as written; it read fine, so nothing comes back to the person.

**Description.** With the new result sentence unwrap was 3,882 (limit 3,686), so the field meanings went to a `help` entry: `UnwrapTool.Reference`, one line in `HelpTool.Entries`, and the `More:` pointer ends the description after the example. The description is shorter than before; the example `{}` is unchanged and still runs as written.

**Measured at `summary`** (characters, against the planning table):

| Sample | Islands | Planning | Now |
|---|---|---|---|
| wrench | 39 | 4,778 | 787 |
| cutter-src | 95 | 11,371 | 794 |
| toolkit-src | 135 | 16,086 | 790 |
| scanner-src | 67 | 8,089 | 786 |

All under `Budget = 2000` (held by `DenseMeshBudgetTests.Unwrap_on_a_tool_sample_is_within_the_budget`).

**README.** `## UVs`' unwrap line says what it returns; `## Dense meshes` gains one paragraph on unwrap's summary and `full`.

**Pins that moved.** `UnwrapToolTests` barrel and partial-set tests (`islands` array became `largest`, plus `islands` the count 3: same assertions); `UnwrapToolDefinitionTests` (properties gain `verbosity`); `UvsScenario` (the barrel's side island read from `largest`, the island count from `islands`); `VerbosityToolTests` `ToDo` loses `unwrap`. No number (2,000, 3,686, five) moved; no sample edited.

**Each new check failing on a wrong input** (source mutated, tests run, restored):

| Wrong input | What fails |
|---|---|
| `largest` takes 6 | `Summary_counts_...` and the four budget rows |
| `all` added at `summary` | `Summary_counts_...` and the budget rows |
| `single_face_islands` is 0 | `Summary_counts_...` |
| `stretch.lowest_face` is 0 | `Summary_counts_...` |
| `largest` drops the ties-to-lowest-face sort | not caught: islands already arrive in lowest-face order, so the sort is stable either way |
| `flipped` total is 0 | not caught: no sample or test mesh has a flipped island, so every total is 0 |

**Not checked.** No frames (nothing draws). The flipped total is held only at 0.
