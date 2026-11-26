---
description: >-
  Open, check, subdivide, unwrap and export report repeated warnings as counts with samples, so a dense cage costs hundreds of tokens and not thousands
---
# M-23 — Quiet on dense meshes

**Status:** Active

**One-liner:** *Open, check, subdivide, unwrap and export report repeated warnings as counts with samples, so a dense cage costs hundreds of tokens and not thousands*

## What this milestone delivers

A tool result on a dense mesh stays small enough to read, and still says what is wrong.

- Warnings of one kind are grouped: a count, the worst case and a few samples.
- A verbosity setting, and a way to get the full list when it is wanted.
- `unwrap` reports by island, not by face.
- A stated tolerance for non-planar faces, or export that triangulates them, so a subdivided quad mesh can stay quads in the file.

Content: the fps-arms cage opened, subdivided, checked and exported with each result under a stated size.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- Opening the 600-vertex quad cage listed about 250 non-planar faces, one warning each: about 12,000 tokens for one `open`.
- The subdivided mesh would have listed thousands, so the project let Carve subdivide and save, triangulated in its own script, and only then opened and exported.
- `unwrap` returned 5,000 to 9,000 tokens for each tool. One tool was rescaled in script to avoid a second unwrap.

## Open at planning

- Default size budget for each tool's result.
- Does the full list go to a file the agent can read?
- Is non-planarity a warning at all on a face that will be triangulated at export?

**Decided at planning (2026-11-24).** Each ruling is the person's unless it says the planner's.

1. **A repeated warning is one line per kind.** A kind with 1 to 3 warnings is listed as before; with 4 or more it is one line: the count, the worst case where the kind measures something, three samples by lowest id, and the `select` query that names them all. It replaces T-72's "Name 10, fold the rest". `warnings` stays an array of strings. Agreed sketch:
   ```
   "warnings": [
     "252 faces are not planar: worst faces[10], 6.03E-05 off its plane (limit 1.07E-05); e.g. faces[0], faces[1], faces[2]; select faces where non_planar names them",
     "798 edges belong to only one face: e.g. edges[0-1], edges[0-9], edges[1-2]; select edges where open names them",
     "136 faces have UVs that overlap another face: e.g. faces[3], faces[7], faces[12]",
     "vertices[7]: no face uses this vertex"
   ]
   ```
   The planner's: which face is "worst" (largest distance off the plane over its limit), and no worst pair for UV overlaps, which the sketch the person saw did show (T-197).
2. **Not planar on a smooth-shaded mesh is not a warning.** `check` gives one note with the count and the worst case, and `ok` can be true; every other tool says nothing. A flat-shaded mesh warns as before, grouped. `select faces where non_planar` names every face either way. `PlanarityTolerance` is unchanged, and export triangulates as it always has. Answers T-127.
3. **`verbosity` on every tool that changes the model or writes a file, and on `check`**: `silent`, `summary` (the default), `full`. Not on `get_mesh`, `get_parts`, `get_skeleton`, `get_weights`, `get_clips`, `select`, `render`, `render_glb` or `help`, whose whole result is what was asked for. A tool error is whole at every level. Agreed sketch (an edit on the subdivided cage):
   ```
   verbosity: "silent"     {"ok": true}
   verbosity: "summary"    {"vertices": 9496, "faces": 9492, "edges": 18984, "closed": false,
                            "bounds": {...}, "warnings": [one line per kind],
                            "created": {...}, "deleted": {...}, "split": []}        (default)
   verbosity: "full"       as summary, with every warning one per line
   ```
   "silent says only ok": no counts, no ids, no warnings. The full list is returned in the result; nothing is written to a file. The planner's, to confirm in T-200: `check` at `silent` answers its own `ok`, and `export` at `silent` answers JSON.
4. **`unwrap` returns a summary and its five largest islands.** Agreed sketch (numbers illustrative):
   ```
   {"mesh": "toolkit", "islands": 135, "single_face_islands": 118,
    "fill": 0.71, "scale": 2.4,
    "stretch": {"worst": 1.38, "lowest_face": 14}, "flipped": 0,
    "largest": [{"faces": 12, "lowest_face": 0, "rect": [0.016, 0.016, 0.383, 0.271], "stretch": 1.0, "flipped": 0}, ... 4 more ...]}
   ```
   The planner's, to confirm in T-202: every island comes back at `verbosity: "full"` as `all`, not by the `islands: "all"` argument of the sketch, since `verbosity` was decided after it.
5. **The budget: 2,000 characters for every result at `summary`** on the fps-arms cage and tools, counted in characters of the result text and held by tests; `silent` is `{"ok":true}`.
6. **The samples**: fps-arms' `work/arms_cage.carve`, `cutter_src.carve`, `toolkit_src.carve` and `scanner_src.carve` (`main` at `0813e3a`) are copied into `samples/` byte for byte, as `wrench.carve` was.
7. **Every carve tool description goes under Claude Code's 4,096-character cut in this milestone**, held by a test over all of them. Seven are over it today, so what `check`, `export` and `open` say about warnings is not seen. The planner's: the test's limit is 3,686 (a 10% margin, as patina's T-170), which adds `get_mesh`, `define_mesh`, `project_uvs` and `unwrap` to the seven.
8. **The long text is served by a new tool, `help`, by topic.** Agreed sketch:
   ```
   help {}                       topics: warnings, check, export, render, render-modes, bake, files, select, ...
   help {"topic": "warnings"}    every warning by kind, what it means for a game, the fix, how a kind is
                                 grouped past 3, the select query that names each, and the verbosity levels
   check's description           what it does, arguments, result shape, one worked example, and
                                 "... explained in help {topic: 'check'} and help {topic: 'warnings'}"
   ```
9. **`PartToolsListTests`' SHA-256 digest of tools/list is retired** (T-197 deletes it). Its companion, which pins where each agreed sentence is, stays and follows the sentences into `help`.

No frame is drawn by this milestone, so it has no scenario project and no golden: the real `carve` is reached through `server-regression` (T-206).

### Measured at planning

On `main` at planning, the real `carve` over stdio, fps-arms' files as they are. Characters of the result text.

| Call | Characters | Of which |
|---|---|---|
| `open` the cage (600 vertices, 590 faces) | 1,156 | 10 not-planar lines and "and 238 more" (887) |
| `check` the cage | 1,434 | the same 11 lines |
| `export` the cage, skinned | 2,038 | the 11 lines; 44 bone names (about 900) |
| `subdivide` once (2,372 faces) | 373 | "2264 faces are not planar (expected after subdivide)" |
| `subdivide` twice (9,488 faces) | 386 | |
| `check` after two | 1,467 | |
| `delete` 200 faces of it | 41,310 | 798 open-edge lines |
| `check` after `project_uvs box` on 100 faces | 43,700 | 280 UV-overlap lines |
| `check` after both | 61,363 | 798 + 136 + 11 |
| `unwrap` wrench (39 islands) | 4,778 | |
| `unwrap` scanner (67) | 8,089 | |
| `unwrap` cutter (95) | 11,371 | |
| `unwrap` toolkit (135) | 16,086 | |

fps-arms' 12,000 tokens for one `open` was Carve 0.9.0, before T-72's fold; `main` already folds not-planar faces, and no other kind. Tool descriptions over 3,686 characters: render 18,551, export 13,480, bake 8,410, check 7,565, save 5,997, select 5,571, open 5,334, get_mesh 4,068, define_mesh 4,032, project_uvs 3,809, unwrap 3,720.

### Order

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-197 | the grouped line and the samples; everything reads its wording |
| 2 | T-198 | the smooth rule, on the same pins and `MeshReport` |
| 3 | T-199, T-200 | side by side: `help` adds a tool and no existing tool file; `verbosity` keeps out of `CarveServer.cs` |
| 4 | T-201, T-202 | side by side on T-200's helper: nineteen tools, and `unwrap` |
| 5 | T-203, T-204, T-205 | side by side, each its own tool files; they share only lists that add or remove a row |
| 6 | T-206 | the real run and the measured table |

Ten tasks, past the usual eight, because the description cut was taken in whole. T-197, T-199 and T-200 are design-heavy and decide what others build on (a wording, a public tool, a mechanism); T-201 and the three description tasks are mechanical; T-206 is a real run.

## Required tasks

- [ ] [T-197](../tasks/T-197-warnings-grouped-one-line-per-kind-and-the-fps-arms-samples.md) — Every mesh report groups a warning kind past 3 into one line (count, worst, 3 samples, the select query), on the fps-arms cage and tool sources now in samples/
- [ ] [T-198](../tasks/T-198-a-not-planar-face-on-a-smooth-mesh-is-a-note-in-check.md) — A smooth-shaded mesh no longer warns of not-planar faces: check notes their count and worst case, and a subdivided mesh can read ok
- [ ] [T-199](../tasks/T-199-the-help-tool-with-the-warnings-topic.md) — help lists reference topics and returns one; the warnings topic explains every warning kind, its grouping and the verbosity levels, and a test holds description lengths
- [ ] [T-200](../tasks/T-200-verbosity-on-the-tools-that-return-a-mesh-report.md) — define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
- [ ] [T-201](../tasks/T-201-verbosity-on-the-other-tools-that-change-the-model-or-write-a-file.md) — The parts, style, UV, skeleton, weight, clip, target, save and bake tools take verbosity, silent answering only ok
- [ ] [T-202](../tasks/T-202-unwrap-reports-a-summary-and-its-five-largest-islands.md) — unwrap returns the island count, worst stretch, flipped total and five largest islands, and every island only at verbosity full
- [ ] [T-203](../tasks/T-203-check-and-export-described-under-the-cut.md) — check's and export's descriptions fit under Claude Code's 4,096-character cut, their reference text served by help
- [ ] [T-204](../tasks/T-204-open-save-select-get-mesh-and-define-mesh-described-under-the-cut.md) — open, save, select, get_mesh and define_mesh fit under the description cut, their reference text served by help
- [ ] [T-205](../tasks/T-205-render-bake-project-uvs-and-unwrap-described-under-the-cut.md) — render, bake, project_uvs and unwrap fit under the description cut, their reference text served by help
- [ ] [T-206](../tasks/T-206-the-dense-run-every-result-on-the-fps-arms-cage-under-2-000-characters.md) — The fps-arms cage opened, subdivided twice, checked, unwrapped and exported through the real carve with every result under 2,000 characters, the sizes tabled

## Retrospective
