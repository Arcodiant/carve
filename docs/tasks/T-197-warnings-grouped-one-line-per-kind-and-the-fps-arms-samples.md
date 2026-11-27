---
description: >-
  Every mesh report groups a warning kind past 3 into one line (count, worst, 3 samples, the select query), on the fps-arms cage and tool sources now in samples/
---
# T-197 — Warnings grouped one line per kind, and the fps-arms samples

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Every mesh report says each kind of warning once, however many elements have it, and the fps-arms files the milestone is measured on are in `samples/`.

## Scope

- **Samples.** Copy byte for byte from the fps-arms repo on this machine (`D:\repos\fps-arms\branches\main`, `main` at `0813e3a`; written by nothing here, as `wrench.carve` was in T-122): `work/arms_cage.carve` to `samples/arms-cage.carve` (600 vertices, 590 quads, smooth, 44 bones), and `work/cutter_src.carve`, `work/toolkit_src.carve`, `work/scanner_src.carve` to `samples/cutter-src.carve`, `toolkit-src.carve`, `scanner-src.carve`. A unit test keeps each readable with the counts measured at planning (cage 600 vertices and 590 faces; cutter 232 and 170; toolkit 200 and 146; scanner 124 and 84). Add `samples/*.carve` to the inputs of `server-unit` and `server-regression` in `tests.json`; add the four to the `samples/` row of `CLAUDE.md`'s Layout.
- **Grouping, in `Carve.Core`.** Replace `MeshWarningFold` (delete it; do not keep it beside the new one) with one grouping of `MeshWarning`s by `MeshWarningKind`, used by every caller: `MeshToolSupport.Report` (so `define_mesh`, `get_mesh`, `open`, `undo`, `move` and every edit tool), `CheckTool`, `ExportTool` (per mesh, the `mesh 'Name': ` prefix kept, `N warning(s)` counting lines) and `SubdivideTool`, whose own fold ("N faces are not planar (expected after subdivide)") goes.
  - A kind with 1 to 3 warnings is listed one per line exactly as today, so the leaky shed's planted faults stay named.
  - A kind with 4 or more is one line, standing where the kind's first warning stood: the count and what is wrong, the worst case where the kind measures something, three samples (the three lowest ids), and the `select` query that names them all where `select` has the predicate today (`faces where non_planar`, `edges where open`, `edges where nonmanifold`, `vertices where unattached`; no clause for a kind without one). The agreed form:
    ```
    252 faces are not planar: worst faces[10], 6.03E-05 off its plane (limit 1.07E-05); e.g. faces[0], faces[1], faces[2]; select faces where non_planar names them
    798 edges belong to only one face: e.g. edges[0-1], edges[0-9], edges[1-2]; select edges where open names them
    136 faces have UVs that overlap another face: e.g. faces[3], faces[7], faces[12]
    ```
  - Worst (the planner's reading, to confirm with the person if it reads badly on the cage): not planar, the face with the largest distance off its plane divided by its limit, ties to the lowest id; non-manifold edge, the edge with the most faces. The other kinds have no worst (the sketch the person saw showed a worst pair for UV overlaps; it is left out because the overlap's size is not kept today, and adding it goes to the person).
  - Only `MeshWarning`s are grouped. `check`'s `problems`, envelope warnings, clip warnings and game-limit warnings are as they are; if one of them floods on the cage, report it to the person, do not group it here.
- **Descriptions.** The sentences in `check`, `export`, `move`, `subdivide` and `define_mesh` that state "up to 10 ... the rest in one line" state the new rule instead, in no more characters than they take today (T-199 puts a ceiling on each description's length).
- **`PartToolsListTests`.** The person retired the SHA-256 digest of tools/list (2026-11-24): delete `Existing_tools_are_unchanged_apart_from_the_agreed_sentences`, `Before`, `Digest` and what only they use. Keep the companion test that pins where each agreed sentence is.
- **Pins that move** (search for more): `MeshWarningFoldTests`, `NotPlanarFoldCheckTests`, `NotPlanarFoldToolTests`, `SubdivideToolTests`, `scenarios/Carve.Scenario.Soft/NotPlanarFold.cs` with `SoftScenario.cs` and `tests/Carve.SoftLoad.Tests`, `scenarios/Carve.Scenario.Morph/ArmStages.cs` with `tests/Carve.MorphLoad.Tests`, `scenarios/Carve.Scenario.Shed/ShedFacts.cs`. Each still holds the count equal to `select faces where non_planar`.
- **Tests** (`unit`): the grouping at 0, 1, 3, 4 and 504 of a kind, two kinds at once, order, the singular; `DenseMeshBudgetTests` on `ToolHarness` with `samples/arms-cage.carve`: `open`, `check`, `subdivide` twice (9,492 vertices, 9,488 faces), `check`, `export`, then `delete` of faces 0, 2, ... 398 (798 open edges; 41,310 characters at planning), `check`, and `project_uvs box` on faces 0 to 99 then `check` (280 overlaps; 43,700 at planning), every result 2,000 characters or less. `Budget = 2000` is one constant T-202 and T-206 use.
- **`README.md`**: a new section `## Dense meshes` after `## Asking the mesh`: how a repeated warning reads and that `select` names every element. Later M-23 tasks add their lines to it.

## Notes

- **Design-heavy, and others build on it**: this task fixes the wording of a grouped line. Write the exact final wording of each kind's line in `### As built`; T-198, T-199 and T-200 read it there.
- Replaces T-72's rule ("Name 10, fold the rest", the person, 2026-11-13) by the person's ruling of 2026-11-24.
- `select faces where non_planar` names every face as before.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Claude Opus 5.5, by two agents (both Claude Opus 5.5): the first built the grouping, samples, pins and budget test; the second moved the last pins, built the wrong-input table and finished.

**Grouping.** `src/Carve.Core/MeshWarningGroups.cs` (`Lines`, `ListedLimit = 3`, `Samples = 3`) replaces `MeshWarningFold` (deleted). `MeshWarning` gained `Measure` and `Limit` (default 0), set by `MeshReport` for not-planar faces (the distance off the plane and its limit) and non-manifold edges (the face count). Callers: `MeshToolSupport.Report`, `CheckTool`, `ExportTool` (both paths; per mesh with `mesh 'Name': ` kept), `SubdivideTool` (its own fold and `Warnings` deleted) and `DisplaceTool`, which used subdivide's fold ("expected after displace") and now groups the same way.

A kind of 1 to 3 is listed as before. A kind of 4 or more is one line where its first warning stood. The count is of distinct elements: a UV-overlap face warned once per other face counts once. The samples are the three lowest ids; edges sort by both ids, so `edges[4-5]` comes before `edges[4-10]`. Worst: not planar, the largest `Measure / Limit`; non-manifold, the most faces; a tie goes to the lowest id. The exact line of each kind, with N at least 4:

| Kind | Line |
|---|---|
| NonPlanarFace | `N faces are not planar: worst faces[i], D off its plane (limit L); e.g. faces[a], faces[b], faces[c]; select faces where non_planar names them` (D and L `G3`, as the single line has them) |
| OpenEdge | `N edges belong to only one face: e.g. edges[a-b], ...; select edges where open names them` |
| NonManifoldEdge | `N edges are shared by more than two faces: worst edges[a-b], shared by K faces; e.g. ...; select edges where nonmanifold names them` |
| InconsistentWinding | `N edges have two faces crossing them in the same direction: e.g. edges[a-b], ...` |
| UnusedVertex | `N vertices are used by no face: e.g. vertices[a], ...; select vertices where unattached names them` |
| UvUnmapped | `N faces have no UVs, so they sample the texture's corner: e.g. faces[a], ...` |
| UvFlipped | `N faces have flipped UVs, so the texture is mirrored on them: e.g. ...` |
| UvZeroArea | `N faces have UVs of zero area, so each is one smeared texel: e.g. ...` |
| UvOverlap | `N faces have UVs that overlap another face: e.g. ...` |
| MaterialNoEffect | `N materials set occlusion that has no effect without an occlusion_texture: e.g. materials['a'], ...` |

**Descriptions** (each sentence no longer than before): `check`, `export`, `move`, `subdivide` and `displace` say the new rule. `define_mesh` has no "up to 10" sentence today, so it is unchanged.

**PartToolsListTests**: deleted the digest test, `Before`, `Digest`, `NewTools`, `Agreed`, `T94Added` and `T94Reworded`, and the usings only they needed. The two sentence-placement tests are kept.

**Samples**: `samples/arms-cage.carve`, `cutter-src.carve`, `toolkit-src.carve` and `scanner-src.carve`, compared with `cmp` against fps-arms' `work/` files. `work/` is git-ignored there, so the files are not in any commit. Their dates (2026-11-15) are before `0813e3a` (2026-11-22), and fps-arms `main` is at `cf5d8db` now. `SampleMeshTests` reads each with its counts. `samples/*.carve` was added to `server-unit` and `server-regression`, and the four were added to CLAUDE.md's `samples/` row.

**README**: a new `## Dense meshes` section after `## Asking the mesh`. It gives the rule, two example lines from the agreed sketch, and the four `select` queries.

**Pins that moved** (each still asserts the same thing in the new form):
- `MeshWarningFoldTests` became `MeshWarningGroupsTests`: 0, 1, 3, 4 and 504 of a kind, order, the tie, worst over its own limit, the edge order, the non-manifold worst, the overlap counted once, two kinds at once, and every kind having a line.
- `NotPlanarFold` (Soft, linked by Morph and the load tests): the count is select's, the samples are select's 3 lowest, the worst is a face select finds, and no other warning is there. `NotPlanarFoldCheckTests` was rewritten to match.
- `NotPlanarFoldToolTests`, `SubdivideToolTests` and `DisplaceToolTests`: the grouped line's count equals `select faces where non_planar`.
- The shed's pinned tool warnings are now `TestMeshes.LeakyShedLines` and `ShedFacts.Warnings`, five lines. `LeakyShedWarnings`, the raw `MeshHealth` list, is unchanged.

**DenseMeshBudgetTests** (`Budget = 2000`): the run the scope lists. One change: `project_uvs` on faces 0 to 99 is refused after the delete, because half of those faces are gone. So the test undoes the delete, projects, checks, then deletes the 200 odd faces and checks again ("both").

**The leaky shed (the person's ruling, 2026-11-26).** The shed has 8 inconsistent-winding, 8 open and 4 non-manifold edges, so each of those kinds is one grouped line like any other, and the winding edges (which have no `select` predicate) are named only by their three samples. The ruling: accept the grouped lines; the pins hold the exact grouped lines and counts; no new `select` predicate is added.

**Pins moved by the second agent:**
- `scenario-uvs` (`UvsScenario.cs`): the eight `faces[34..41]: no UVs` lines became the one line `8 faces have no UVs, so they sample the texture's corner: e.g. faces[34], faces[35], faces[36]`, pinned exactly.
- `MaterialToolDefinitionTests`: `set_material`'s example leaves four faces unmapped, so `check`'s warnings are pinned to the single line `4 faces have no UVs, so they sample the texture's corner: e.g. faces[0], faces[1], faces[4]`, still nothing else (RV-47). `set_material`'s description ("check warns only of the other four faces, 0, 1, 4 and 5") is unchanged and still true.
- `MaterialToolTests.Check_names_the_material_...`: the partly mapped step now maps faces 0 to 2 instead of 0 and 1, so three faces stay unmapped and listed, and the test still shows the material's warning replacing the generic one for face 4. With four or more, a material's "has a texture but the face has no UVs" warning is the same kind (`UvUnmapped`) and goes into the grouped line, so the material's name is not in it. That follows the scope's grouping by kind; flagged here in case the person wants it otherwise.
- `MorphTargetFileTests.Every_committed_file_rereads_and_rewrites_equal`: fixed on the test's side. fps-arms wrote the four samples minified, so Carve's writer does not give their bytes back, and the samples stay as copied. Those four (by name) skip only the committed-equals-written step. Every file still reads, and Carve's own writing of each file must reread and write back equal (that last step now compares with the first write, which is the committed text for every other file).

**Failing on wrong input.** Each wrong input was applied, the tests run, then reverted (`git checkout`, the samples rewritten from their own bytes and checked against fps-arms with `cmp`).

| Wrong input | Fails |
|---|---|
| `ListedLimit` 4 | 6 of `MeshWarningGroupsTests` (four faces, edge order, non-manifold worst, worst over limit, two kinds, every kind) |
| `Samples` 2 | 7 of `MeshWarningGroupsTests` |
| worst tie goes to the highest id | 4 of `MeshWarningGroupsTests` (the tie, non-manifold worst, 504, two kinds) |
| worst by distance, not over its own limit | `The_worst_face_is_the_most_over_its_own_limit_not_the_farthest_off` |
| edges sorted by their text | `A_face_warned_twice_counts_once`, `Many_faces_...`, `Two_kinds_...` |
| count warnings, not elements | `A_face_warned_twice_counts_once` |
| no `select` clause | 6 of `MeshWarningGroupsTests` |
| grouped line at the end, not where its first warning stood | `Two_kinds_group_each_where_its_first_warning_stood_...` |
| no grouping at all (`ListedLimit` 100000) | 9 of `MeshWarningGroupsTests`; in Server, `DenseMeshBudgetTests`, `CheckToolTests` (both shed tests), `DisplaceToolTests`, `MaterialToolDefinitionTests`, 4 of `NotPlanarFoldToolTests`, `SubdivideToolTests`; `scenario-uvs`; `scenario-shed` (expected 5 lines, got 22) |
| a Carve-written sample (`gatehouse.carve`) one byte off | `MorphTargetFileTests.Every_committed_file_...`, `MaterialFileTests.Every_committed_file_...` |
| an fps-arms sample cut short | `MorphTargetFileTests.Every_committed_file_...`, `SampleMeshTests` |
| the cage replaced by the cutter | `SampleMeshTests` |

Not shown by that run: `Carve.SoftLoad.Tests` and `Carve.MorphLoad.Tests` gave no test result under `dotnet test` with no filter, so the shared `NotPlanarFold` was not proven by mutation there. `NotPlanarFoldCheckTests` has its own wrong inputs built in, and verify ran `scenario-soft` and `scenario-morph`.
