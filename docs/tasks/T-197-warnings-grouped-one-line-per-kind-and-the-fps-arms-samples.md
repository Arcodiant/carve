---
description: >-
  Every mesh report groups a warning kind past 3 into one line (count, worst, 3 samples, the select query), on the fps-arms cage and tool sources now in samples/
---
# T-197 — Warnings grouped one line per kind, and the fps-arms samples

**Status:** Open

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
