---
description: >-
  A smooth-shaded mesh no longer warns of not-planar faces: check notes their count and worst case, and a subdivided mesh can read ok
---
# T-198 — A not-planar face on a smooth mesh is a note in check

**Status:** Done

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-197

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

A smooth-shaded mesh, which is what a subdivided cage is, does not warn of not-planar faces: `check` notes them, and the mesh can read `ok: true`. Answers T-127.

## Scope

- **The rule (the person, 2026-11-24).** On a mesh whose shading is smooth (`set_shading`), a not-planar face is not a warning: `define_mesh`, `get_mesh`, `open`, `undo`, `move`, the edits, `subdivide` and `export` say nothing of it, and `check` gives one note, never a warning, so `ok` can be true: the count and the worst case in T-197's words for the worst, and that export triangulates every face (e.g. `2264 faces are not planar: worst faces[10], ... ; expected on a smooth mesh, which export triangulates`). A flat-shaded mesh warns as T-197 left it.
- `select faces where non_planar` still names every not-planar face on any mesh. Today `MeshQuery.NonPlanar` reads the report's warnings, so the measure moves out of the warning: one place in `Carve.Core` says which faces are not planar and how far, and the report, the note and `select` all ask it.
- **Pins that move**: the soft gardener (`scenario-soft`, `NotPlanarFold.cs`, `tests/Carve.SoftLoad.Tests`) and the arm (`scenario-morph`, `ArmStages.cs`, `tests/Carve.MorphLoad.Tests`) are smooth, so their exports carry no not-planar line; they assert no warning at all, and `check`'s note with its count equal to `select faces where non_planar`. A flat mesh with 4 or more not-planar faces still asserts T-197's line.
- **Tests** (`unit`): a flat and a smooth copy of the same lifted quad through `check`, `get_mesh`, `export`, `move` and `subdivide`; `set_shading` flat to smooth and back changing the answer; `DenseMeshBudgetTests`' cage `check` reads `ok: true` after two subdivides; the note's count against `select`.
- **Descriptions and README**: the not-planar sentence of `check`, `subdivide`, `move` and `export` says the rule, in no more characters than today; a line in `README.md`'s `## Dense meshes`.

## Notes

- Supersedes T-127 (off-milestone): say in `### As built` that T-127 is answered by this, for the orchestrator to close it.
- `PolygonMesh.PlanarityTolerance` (1e-4 of the face's diagonal) is not changed.
- Depends on T-197 because both rewrite the same pins and `MeshReport`.
- **Read M-23's *Decided at planning (2026-11-24)* first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Claude Sonnet 5.5, by one agent.

**The rule.** `MeshReport.NonPlanarFaces` (`Carve.Core`) is the one place that says which faces are not planar and how far, on any mesh. `MeshReport.Warnings` carries them only while `mesh.ShadingMode` is Flat, in the position they always had, so every tool (`define_mesh`, `get_mesh`, `open`, `undo`, `move`, the edits, `subdivide`, `displace`, `export`) says nothing of them on a smooth mesh and groups them as T-197 left it on a flat one. `MeshQuery.NonPlanar` (`select faces where non_planar`) reads `NonPlanarFaces`, so it names every face on either mesh. `MeshHealth` adds the note for a smooth mesh with any: `MeshWarningGroups.SmoothNote`, T-197's grouped line (count, worst, three samples, select query) at any count, then `; expected on a smooth mesh, which export triangulates`. `ok` ignores notes, so a smooth cage reads true. `PlanarityTolerance` is unchanged.

**Note wording** (N faces, even N = 1 or 2): `N faces are not planar: worst faces[i], D off its plane (limit L); e.g. faces[a], faces[b], faces[c]; select faces where non_planar names them; expected on a smooth mesh, which export triangulates`.

**Descriptions** (each sentence no longer than before): `check`, `subdivide`, `move` and `export` say the rule. `displace`'s sentence is about the grouping in general and is unchanged.

**README**: one paragraph added to `## Dense meshes`.

**T-127 is answered by this**, for the orchestrator to close.

**Pins that moved** (each still asserts the same thing, for a smooth mesh):
- `NotPlanarFold` (`scenarios/Carve.Scenario.Soft`) gained `CheckSmoothExport` (the export has no warning at all, and select finds some, so the rule is exercised) and `CheckSmoothNote` (no warning says not planar; exactly one note, its count select's, its worst face one select finds). The flat-mesh `Check` and `CheckExport` are kept, unchanged, with their tests, for T-197's line.
- Soft gardener: `SoftScenario` asserts both with a `check` per mesh and keeps `check-Body.txt`/`check-Hat.txt` in the run folder; `SoftLoadTests` reads them.
- Arm: `ArmStages` (scenario-morph) and `MorphLoadTests` assert the same; `A_wrong_count..._fails_the_fold_check` became `..._fails_the_note_check`.
- `DenseMeshBudgetTests`: the cage's `check` after two subdivides asserts `ok: true` and the note.
- New `SmoothNotPlanarToolTests`: flat and smooth rows of lifted quads (12 and 2) through `check`, `get_mesh`, `move` and `export`; `set_shading` smooth and back flips the answer; a subdivided smooth cube reads ok; the note's count against `select`.
- `scenario-soft` and `scenario-morph` (real-server runs) ran in verify and passed. The note checker first failed there: its regex lacked the `; select faces where non_planar names them` clause the note carries; `SmoothNotPlanarToolTests` now pins the note's ending exactly.

**Wrong inputs** (each check, fed the wrong thing, fails):

| Check | Wrong input | Result |
|---|---|---|
| `CheckSmoothNote` | note counts 12, select finds 13 | problem returned (`A_note_that_does_not_count_select_s_faces_fails`) |
| `CheckSmoothNote` | worst face not among select's | "worst face" problem |
| `CheckSmoothNote` | no note | problem |
| `CheckSmoothNote` | a "not planar" warning on the smooth mesh | "check warns" problem |
| `CheckSmoothExport` | any `warning(s)` in the export | problem |
| `CheckSmoothExport` | select finds none | problem (nothing tested) |
| `MorphLoadTests` note check | count one more, one fewer, a warning added | each fails |
| `SmoothNotPlanarToolTests` | the same quads left flat | warns in check, get_mesh, move and export (the flat half of each test) |
