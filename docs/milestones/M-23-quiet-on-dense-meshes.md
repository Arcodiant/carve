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

**Re-scoped after M-29 (2026-11-26).** The person's rulings, which bind over the planner's draft (`artifacts/M-23/rescope.md`). M-29's T-207 had already built `help` (by tool, released in 0.19.0) and brought every description under 3,686 with a test and no tool exempt, so rulings 7 and 9 were met by T-207.

1. `help` gains a `topic` argument beside the released `tool`; `warnings` is the only topic for now, and `tool` is unchanged.
2. T-203 is merged into T-199; T-204 and T-205 are dropped. All three are Done, each noting that T-207 delivered the work.
3. check's catalogue of warnings moves out of its description into the `warnings` topic; check keeps what it does, one example and a pointer to `help {"topic":"warnings"}`.
4. The `warnings` topic covers the ten mesh-report kinds plus check's model and graph-material warnings. Export's limit warnings and the clip warnings stay in their tools' entries.
5. `displace` (added by M-29) joins T-201's tools that take `verbosity`.
6. The planner's readings in T-200 are confirmed: `check` at `silent` answers its own `ok`, and `export` at `silent` answers JSON. The description limit stays at 3,686.

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

### Measured

Measured by T-206 on Claude Sonnet 5.5 and again after RV-91's fixes on Claude Opus 5.5, the real `carve` over stdio at `summary`, characters of the result text (`silent` is 11, or 12 for a `check` that reads false; `full` and every call are in `artifacts/M-23/T-206/dense-run.tsv`).

| Call | At planning | Now |
|---|---|---|
| `open` the cage | 1,156 | 248 |
| `check` the cage | 1,434 | 730 |
| `export`, skinned (at planning the 590-face cage; now after two subdivides and a move) | 2,038 | 1,039 |
| `subdivide` once, twice | 373, 386 | 319, 332 |
| `check` after two | 1,467 | 766 (ok: true) |
| `delete` 200 faces | 41,310 | 1,119 |
| `check` after `project_uvs box` | 43,700 | 849 |
| `unwrap` wrench, scanner, cutter, toolkit | 4,778, 8,089, 11,371, 16,086 | 787, 786, 794, 790 |

The exported cage holds 18,976 triangles. Largest description 3,684 (limit 3,686). No result is over 2,000.

### Order

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-197 | the grouped line and the samples; everything reads its wording |
| 2 | T-198 | the smooth rule, on the same pins and `MeshReport` |
| 3 | T-200 | `verbosity` and its helper, which the next wave builds on |
| 4 | T-199, T-201, T-202 | side by side: T-199 owns check, open and export descriptions and `HelpTool.cs`; T-201 its twenty tools; T-202 `UnwrapTool.cs` |
| 5 | T-206 | the real run and the measured table |

T-203, T-204 and T-205 are Done: M-29's T-207 delivered them (T-203's remainder moved into T-199). Seven tasks remain of the ten. T-197, T-199 and T-200 are design-heavy and decide what others build on; T-201 is mechanical; T-206 is a real run.

## Required tasks

- [x] [T-197](../tasks/T-197-warnings-grouped-one-line-per-kind-and-the-fps-arms-samples.md) — Every mesh report groups a warning kind past 3 into one line (count, worst, 3 samples, the select query), on the fps-arms cage and tool sources now in samples/
- [x] [T-198](../tasks/T-198-a-not-planar-face-on-a-smooth-mesh-is-a-note-in-check.md) — A smooth-shaded mesh no longer warns of not-planar faces: check notes their count and worst case, and a subdivided mesh can read ok
- [x] [T-199](../tasks/T-199-the-help-tool-with-the-warnings-topic.md) — help takes a topic beside tool; the warnings topic explains every warning kind, its grouping, its fix and the verbosity levels, and check's catalogue of warnings moves into it
- [x] [T-200](../tasks/T-200-verbosity-on-the-tools-that-return-a-mesh-report.md) — define_mesh, the edits, subdivide, undo, open, check and export take verbosity: silent answers only ok, summary groups, full lists every warning
- [x] [T-201](../tasks/T-201-verbosity-on-the-other-tools-that-change-the-model-or-write-a-file.md) — The parts, style, UV, skeleton, weight, clip, target, save and bake tools take verbosity, silent answering only ok
- [x] [T-202](../tasks/T-202-unwrap-reports-a-summary-and-its-five-largest-islands.md) — unwrap returns the island count, worst stretch, flipped total and five largest islands, and every island only at verbosity full
- [x] [T-206](../tasks/T-206-the-dense-run-every-result-on-the-fps-arms-cage-under-2-000-characters.md) — The fps-arms cage opened, subdivided twice, checked, unwrapped and exported through the real carve with every result under 2,000 characters, the sizes tabled

### Retired, delivered by M-29's T-207

Done with no branch of their own, so not required members (2026-11-26; T-203's remainder moved into T-199):

- [T-203](../tasks/T-203-check-and-export-described-under-the-cut.md) — check's and export's descriptions fit under Claude Code's 4,096-character cut, their reference text served by help
- [T-204](../tasks/T-204-open-save-select-get-mesh-and-define-mesh-described-under-the-cut.md) — open, save, select, get_mesh and define_mesh fit under the description cut, their reference text served by help
- [T-205](../tasks/T-205-render-bake-project-uvs-and-unwrap-described-under-the-cut.md) — render, bake, project_uvs and unwrap fit under the description cut, their reference text served by help

## Retrospective

**As planned.**

- **Every result on the fps-arms files is under the 2,000-character budget at `summary`** (T-206, the real `carve` over stdio, `### Measured` above). For example, `delete` of 200 faces went from 41,310 to 1,119 characters, `check` after `project_uvs` from 43,700 to 849, and `unwrap` on the toolkit from 16,086 to 790. At `silent` every result is `{"ok":true}`, except `check` on a broken mesh, which answers `{"ok":false}`, and at `full` every warning is listed (40,389 characters for the 798 open edges). After two subdivides the cage reads `ok: true`, and its export holds 18,976 triangles.
- **T-197** grouped a repeated warning into one line per kind, as the person agreed: the count, the worst case, three samples and the `select` query. It replaced `MeshWarningFold` and subdivide's own fold, and copied the four fps-arms files into `samples/` byte for byte. `DenseMeshBudgetTests` holds the budget in `unit`.
- **T-198**: a smooth mesh no longer warns of not-planar faces. `check` gives one note instead, and `select` still names every face. This answers T-127, closed as Done in the same commit as this retrospective.
- **T-200** added `verbosity` (`silent`, `summary`, `full`) and its helper in `MeshToolSupport`. **T-201** used the helper on twenty more tools, and a guard over tools/list now holds every tool that changes the model to having it.
- **T-202**: `unwrap` answers with a summary and its five largest islands, and every island comes only at `full`, as `all`.
- **T-199**: `help {"topic":"warnings"}` explains every warning kind, its fix, the grouping and the verbosity levels. check's description went from 3,650 to 2,195 characters.
- **The person's rulings, asked as each question came up** (2026-11-26):
  - start M-23 while M-29 was still in review, running only T-197 and T-198 until M-29 landed;
  - the leaky shed's kinds are grouped like any other;
  - a material's no-UV warnings fold into the generic line past 3;
  - the four minified samples are exempt by name from the committed-equals-written step;
  - the `get_mesh` digest on `arm.carve` is re-pinned, and M-29's `HelpAndBake` digest stays deleted;
  - the re-scope after M-29 (below);
  - `displace` goes to T-201;
  - the planner's readings of `check` and `export` at `silent` are confirmed.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-197 | Opus 5.5 | 2 (a hand-off at 76 calls) |
  | T-198 | Sonnet 5.5 | 1 |
  | Rebase onto `main` after M-29 | Opus 5.5 | 1, resumed once for the person's rulings |
  | Re-scope draft (read-only) | Opus 5.5 | 1 |
  | Re-scope applied to the tracker | Sonnet 5.5 | 1 |
  | T-200 | Opus 5.5 | 2 (a hand-off at 63 calls) |
  | T-199 | Sonnet 5.5 | 1, resumed after Waystone closed |
  | T-201 | Sonnet 5.5 | 1, resumed after Waystone closed |
  | T-202 | Sonnet 5.5 | 1, resumed after Waystone closed |
  | T-206 | Sonnet 5.5 | 1 |

  No rework was traced to the Sonnet members. Both Opus members ran to a second agent, at the call checkpoint.

**Not as planned.**

- **M-29 overlapped M-23 heavily.** M-29 was in review when M-23 started. Its T-207 had already built `help`, keyed by tool, and cut every description under the limit, held by a test with no tool exempt. M-23 started with T-197 and T-198 only. When M-29 landed, an Opus agent rebased M-23 onto `main`; only T-197's commit conflicted, in `CLAUDE.md`, `tests.json` and `PartToolsListTests.cs`. An Opus agent then drafted a re-scope, and the person ruled on it:
  - `help` gains `topic` beside the released `tool`;
  - T-203 is merged into T-199, and T-204 and T-205 are dropped;
  - check's catalogue of warnings moves to the topic;
  - the topic covers the mesh kinds and check's model and graph-material warnings.

  Seven members did work instead of ten.
- **T-203, T-204 and T-205 are out of `## Required tasks`.** Marked Done with no branch, they failed `verify`'s `members` check, since cairn has no "dropped" status. Waystone's filter answered the question put to the person for them: move the three into *Retired, delivered by M-29's T-207*, links kept. It is recorded here for the person to confirm or overturn.
- **`verbosity` is explained only in each argument's schema description**, not in the tool descriptions. One sentence would have put `define_mesh` at 3,830 and `check` at 3,796, over 3,686. The only description changed is `undo`'s.
- **RV-92's three rulings (2026-11-27) came from Waystone's filter answering for the person**, for the person to confirm or overturn: UV-overlap grouping is decided by pairs (1 to 3 pairs listed; past that one line counting the distinct faces of the pairs), ruling 1's wording left alone; a test that `MeshUvHealth` sets `Other`; and a `full` test over the nine report tools RV-92 names. Answered on Claude Opus 5.5. Two of the nine, `project_uvs` and `set_uvs`, answer with no mesh report, so their `full` has no warnings to list.
- **`displace`**, added by M-29, was in no M-23 task's list. T-201 took it, by the person's ruling.
- **`DenseMeshBudgetTests` runs `undo` before `project_uvs`**, because after the delete half of faces 0 to 99 are gone and the projection is refused. The same calls are measured.
- **The four samples can't be tied to fps-arms `0813e3a`.** fps-arms' `work/` is git-ignored. The files' dates (2026-11-15) are before that commit, and they are copied byte for byte.
- **Waystone closed once with T-199, T-201 and T-202 running.** All three had committed their work, and each was resumed and finished with nothing lost.
- **Machine-load failures.** `server-regression` timed out at `carve` start-up twice (M-23's verify after the rebase, and T-202's first `finish`). `scenario-patina` crashed once in the driver. Each passed on the next run. Filed as **B-13**; `cairn test report --since 2d` shows `server-regression` at 2 flaky and 5 failed runs, and `scenario-patina` at 2 flaky. The report shows nothing else to file.
- **`finish` could not remove any member's worktree folder**, because each was its agent's working directory. `branches/task/T-197` to `T-202` and `T-206` are unregistered from git and left on disk.

**Never verified.**

- **The token cost through an ordinary client.** T-206 measured characters through the real process, not tokens through Claude Code. The `carve` MCP registered here is a published copy, and updating it is the person's step: `%LOCALAPPDATA%\carve-mcp\update.ps1 -Server carve`, then `/mcp` reconnect, then `open` on `samples/arms-cage.carve` and `unwrap` on `samples/toolkit-src.carve`, beside fps-arms' 12,000 and 5,000 to 9,000.
- **Checks never seen failing on a wrong input:**
  - `DenseRunProcessTests`' budget and `silent` assertions (only `full`'s was seen failing);
  - `Quiet` turning a tool error into `{"ok":true}`, since no tool hands it an error;
  - `unwrap`'s ties-to-lowest-face order, since islands already arrive in that order;
  - its `flipped` total, since no sample has a flipped island.
- **Smooth against flat has no Core-level `MeshReport` test**; the server tests cover it through `set_shading`.
- **The re-pinned `get_mesh` text on `arm.carve` was not diffed whole.** Only the removal of its not-planar lines was shown.

**Known and left.**

- **check's `help` entry (`CheckTool.Reference`) still holds the full warning wording**, so the same sentences are in it and in the `warnings` topic. T-199's scope moved only the description's paragraphs. Worth a look in the review.
- **Past 3 faces, the grouped no-UV line no longer names the material** (the person's ruling, as built).
- **The descriptions are at the limit**: `define_mesh` 3,684, `project_uvs` 3,682 and `unwrap` close, against 3,686. The next sentence added to any of them needs text moved to `help`.
- **`README.md` and `CLAUDE.md` are inputs of no suite** (`verify` warns).
- **T-203's `Depends on:` still names T-199 and T-200**, which reads oddly for a task merged into T-199.

**For the next plan.**

- **Read the live branch of every Active milestone at planning, not only at the start.** M-29's T-207 was planned and built in the same days as M-23's plan, and covered three of its tasks. A check of M-29's branch at M-23's planning would have saved the re-scope and the hold.
- **A task with no work of its own should come off `## Required tasks` when it is dropped**, not be marked Done in place; `verify`'s `members` check refuses a Done member with no branch.
- **Give a design-heavy Opus member a narrower slice, or plan for two agents.** T-197 and T-200 each passed the 60-call checkpoint with verify still to run.
