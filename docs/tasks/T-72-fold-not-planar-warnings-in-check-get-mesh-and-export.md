---
description: >-
  Fold not-planar warnings in check, get_mesh, export, open and move
---
# T-72 — Fold not-planar warnings in check, get_mesh, export, open and move

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-43

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A subdivided mesh's report stays readable: Catmull-Clark faces are almost never planar, and one warning per face buries every other warning.

## Scope

- `check`, `get_mesh`, `export`, `open` and `move` fold not-planar warnings by the person's rule (2026-11-13): up to 10 not-planar faces are listed one per line as today; past 10, the first 10 are listed and the rest become one line: "and N more faces are not planar; select faces where non_planar names them". `subdivide` keeps its own fold (T-43). `select faces where non_planar` still names every face.
- The soft gardener's export (T-44: 504 not-planar warnings) then carries one folded line, and `scenario-soft` and `tests/Carve.SoftLoad.Tests` assert that one folded line and no other warning. Today, by the person's ruling in RV-27, they assert that every warning is a not-planar face, with the count equal to `select faces where non_planar`.

## Notes

- From the person's ruling on T-43 (2026-11-10): the fold went into `subdivide` only, and a task of its own if T-44 showed the flood getting in the way in `check` and export. T-44 did: its scenario asserts that every export warning is a not-planar face instead of none.
- **Pulled into M-19 (2026-11-13)** on the person's ruling, after T-49's acceptance run: on the subdivided character, `move`, `check`, `open` and `export` returned 74 to 120 thousand characters of not-planar warnings, which the client cut. The scope was widened to `open` and `move` then.
- **The fold count, the person's ruling (2026-11-13):** "Name 10, fold the rest", as the Scope says. The leaky shed's planted faults stay named.
- **Open rules: stop and ask.** Anything else the spec does not settle, and any loosened check.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by Sonnet 5.5 (claude-sonnet-5-5).

- `Carve.Core/MeshWarningFold.cs`: `MeshWarningFold.Lines(warnings)` returns the warnings as text, the first 10 not-planar faces one per line, every other warning kept in report order, and the rest of the not-planar faces as one last line, "and N more faces are not planar; select faces where non_planar names them" (N = 1: "and 1 more face is not planar; ..."; the spec's quoted wording has no singular, so this is my reading).
- "First 10" is read as the 10 lowest face ids (the fold sorts by id, and the report already comes in ascending order). The fold line comes last, after the other warnings, as subdivide's does.
- Used by `MeshToolSupport.Report` (so `get_mesh`, `open`, `move`, and also `define_mesh`, the other edit tools and `undo`, which share that report: the spec lists five tools, but they are one code path and the same flood; flagging it as a reading of the scope), `check`, and `export` (per mesh, the lines prefixed `mesh 'Name': ` as before, so a model with several meshes names 10 and folds per mesh; the `warning(s)` count counts lines). `subdivide` keeps its own fold (T-43). Tool descriptions of `check`, `export`, `move` and `subdivide` state the rule.
- Note for readers of an export line: the fold line contains "; " itself, so splitting an export's warnings on "; " cuts it in two; `NotPlanarFold.Check` lifts it out first.
- `scenarios/Carve.Scenario.Soft/NotPlanarFold.cs` (linked by `tests/Carve.SoftLoad.Tests`): per mesh, the named lines (ascending, at most 10) plus the folded count equal `select faces where non_planar` of that mesh, and no other warning. As strong as before (before: count equal, all not-planar); it also now catches a wrong fold count and a stray warning. Used by `scenario-soft` and `SoftLoadTests`.

Tests: `unit` `MeshWarningFoldTests` (0, 1, 10, 11, 504 faces, order independence, other warnings kept), `regression` `NotPlanarFoldToolTests` (get_mesh, check, move, open, export through the real `carve` with 10, 11 and 12 faces; the leaky shed's warnings unchanged), `SubdivideToolTests` updated (check now folds), `unit` `NotPlanarFoldCheckTests`.

Changed checks shown failing (`NotPlanarFoldCheckTests`; the other changed assertions are shown by the same code paths):

| Check | Wrong input | Result |
|---|---|---|
| soft export check | fold says 2 more, select says 3 | fails |
| soft export check | no fold line | fails |
| soft export check | all 13 faces listed one by one | fails |
| soft export check | a triangle-limit warning added | fails |
| soft export check | fold line for a mesh whose faces all fit | fails |
| soft export check | the right text | passes |

Not checked: byte hashes of frames and exported files before and after were not compared by hand; the change touches only warning text (not in the bytes), and the scenario suites that compare pixels and file content passed in `verify`.
