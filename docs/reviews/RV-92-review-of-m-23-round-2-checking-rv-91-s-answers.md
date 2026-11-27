---
description: >-
  Review of M-23, round 2: checking RV-91's answers
---
# RV-92 — Review of M-23, round 2: checking RV-91's answers

**Status:** Answered

**Reviews:** M-23

**Round:** 2

**Previous:** RV-91

**Diff:** 988a9e0548668139fc4b175b69ac9fbc4d2d3be6 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### Grouping now waits on faces, not warnings, so two or three overlapping pairs group, against ruling 1 and the warnings topic

`src/Carve.Core/MeshWarningGroups.cs:22`-`:29` (`counts` from `ElementsOf`), with `src/Carve.Server/WarningsTopic.cs:31` and the class comment at `MeshWarningGroups.cs:7`. Rubric 1 (Correctness) and docs untrue. Ruling 1 says "A kind with 1 to 3 warnings is listed as before", and the topic and the class comment say the same. RV-91's fix decides grouping on the distinct faces of the pairs. Failing case: UV overlaps (0,1) and (2,3). That is two warnings, which the ruling and the topic say are listed. Now they become the one line `4 faces have UVs that overlap another face: e.g. faces[0], faces[1], faces[2]`, which drops `faces[3]` and says nothing of which faces pair up. The same happens with three disjoint pairs. No test has 2 or 3 pairs over more than 3 faces: `Overlaps_among_three_faces_stay_listed` uses three pairs among three faces. The fix should either decide grouping on the warning count while still counting faces in the line, or the topic and comment should say the rule is by face for UV overlaps. Whether to depart from ruling 1 here is the person's call.

**Outcome:** fixed

Ruling of 2026-11-27, from Waystone's filter answering for the person, for the person to confirm or overturn: UV-overlap grouping is decided by pairs, and the grouped line counts the faces involved. `MeshWarningGroups.Lines` now groups a kind past 3 warnings (`kind.Count`), so 1 to 3 overlapping pairs are listed one line each; the grouped line still counts and samples the distinct faces of the pairs, as the sketch's "136 faces have UVs that overlap another face" shows. Ruling 1's wording is unchanged. The class comment and the warnings topic now say grouping is by pairs and the count is of faces. New tests: 2 and 3 disjoint pairs are listed (`Two_or_three_disjoint_overlapping_pairs_stay_listed`), 4 disjoint pairs group as "8 faces" (`Four_disjoint_overlapping_pairs_group_on_their_eight_faces`), and one face overlapping four (4 pairs) groups as "5 faces" (`One_face_overlapping_four_groups_on_the_five_faces`, already there). Putting the face-count threshold back fails both rows of the first test.

### Nothing tests that `MeshUvHealth` sets `Other`, the half of the UV-overlap fix that real output depends on

`src/Carve.Core/MeshUvHealth.cs:72` (`Other: $"faces[{ids[j]}]"`). Rubric 2. The new `MeshWarningGroupsTests` build their warnings with `Other` already set (`MeshWarningGroupsTests.cs:104`). `MeshUvHealthTests` compares only `ToString()`, which leaves `Other` out. `DenseMeshBudgetTests.cs:78` checks only that the grouped line is present. `DenseRunProcessTests` checks `full >= summary`, and dropping `Other` lowers the summary count, so that check still passes. Failing case: delete `Other: ...` from `MeshUvHealth.cs:72`. Real `check` output goes back to counting only the lower face of each pair (RV-91's "1 faces" for one face overlapping four), and every test still passes. A pin on real output, such as `check` after `project_uvs box` on the cage reading `372 faces have UVs that overlap`, or a `MeshUvHealthTests` assert on `Other`, would catch it.

**Outcome:** fixed

Ruling of 2026-11-27, from Waystone's filter answering for the person, for the person to confirm or overturn: test that `MeshUvHealth` sets `Other` on real output. `MeshUvHealthTests.An_overlap_names_the_higher_face_as_its_other` runs `MeshHealth.Create` on two faces with the same UVs and asserts the overlap's `Element` and `Other` (`faces[0]`, `faces[1]`). Deleting `Other: ...` from `MeshUvHealth.cs` fails it.

### RV-91's `full` finding is fixed for six tools; the other report tools still pass with a dropped level

`tests/Carve.Server.Tests/VerbosityToolTests.cs` (`Full_on_the_holed_cage_lists_every_open_edge`, and the theory rows at :29-:33). Rubric 2. The answer says "a dropped level fails". That now holds for delete, check, export, open, move and undo, and it already held for subdivide and displace. `define_mesh`, `extrude`, `merge_faces`, `bridge`, `knife`, `loop_cut`, `plane_cut`, `project_uvs` and `set_uvs` still reach `full` only through the theory's warning-free cube, where `full` equals `summary`. Failing case: change `DefineMeshTool.cs:191` `Report(mesh, level)` to `Report(mesh)`, or drop `level` from `ExtrudeTool.cs:75`. `define_mesh` or `extrude` at `full` on a mesh with more than 3 warnings then returns the grouped line, and no test fails. A theory row whose start mesh has 4 or more open edges would cover them all.

**Outcome:** fixed

Ruling of 2026-11-27, from Waystone's filter answering for the person, for the person to confirm or overturn: fix the `full` gap in M-23. `VerbosityToolTests.Full_lists_four_unused_vertices_one_per_line_on_each_report_tool` runs the theory's rows for define_mesh, extrude, merge_faces, bridge, knife, loop_cut, plane_cut, project_uvs and set_uvs at `full`, with four vertices no face uses added to the start mesh. It asserts all four `vertices[n]: no face uses this vertex` lines and no `e.g.` (grouped) line. Changing `DefineMeshTool.cs:191` to `Report(mesh)` fails it, and so does dropping `level` from `ExtrudeTool.cs:75`; both were restored. project_uvs and set_uvs answer with no mesh report at any level (no `warnings` in their results), so the test asserts that there is no warning in their `full` result; there is no level for them to drop into a grouped line. The finding's premise for those two does not hold, and they were not given a report.

## Summary

I checked RV-91's 14 answers against `b8c7a024..3b3b62f6` on `3b3b62f6`, and ran the touched classes: `MeshWarningGroupsTests` and `MeshReport*` in Core (22 tests), and `DenseRunProcessTests`, `VerbosityToolTests`, `SmoothNotPlanarToolTests`, `HelpToolTests`, `UnwrapToolTests` and the not-planar, export and check tests in Server (108 tests). All passed.

- **Export drops a flat mesh's not-planar warnings beside a smooth one** (both reports): holds. `MeshReport.Create` decides per face through `Shading.FaceHasCornerNormals`, which reads `FlatFaces` on the joined mesh and the mesh's own mode otherwise. So a single mesh behaves as before, and `select` and check's smooth note still read the full `NonPlanarFaces`. The new export test fails under the old whole-mesh rule, as the answer reports.
- **UV-overlap group counts only the lower face** (both reports): the count and samples are fixed. The new finding above covers the fix's change of grouping threshold, and the one before it covers `Other` going untested on real output. `Other` has no other reader, and record equality on `MeshWarning` is not used anywhere.
- **"1 faces" in the smooth note**: holds (`Says` has singular and plural forms; `A_note_of_one_face_is_singular`; `NotPlanarFold`'s regex takes both).
- **Skinned per-mesh line unpinned**: holds. The summary export now matches `mesh '...': 798 edges belong to only one face ...`. Dropping the prefix or passing `Full` at `ExportTool.cs:302` would fail it.
- **"; " separator** (skipped): the reason holds. No reader in the repository splits the text on it, and `NotPlanarFold` uses a regex.
- **`full` untested on most tools**: partly. See the third finding.
- **Warnings topic and MaterialNoEffect**: holds. It has its own heading, the duplicate sentence is gone, and the first sentence and check's description and `Reference` agree.
- **unwrap's `all` and `silent`**: holds. Each `all` entry's fields and `largest`'s identity are asserted, the comment is corrected, `seams` is gone, and silent is held by `Unwrap_at_silent_...`. The `flipped` total is still 0 = 0, and the answer says so.
- **help with a non-string topic**: holds (the error lists the topics, and the test asserts the ending).
- **check's description and `Reference`**: holds (verbosity, silent's and full's results, and "past 3" for every kind).
- **README and displace**: holds.
- **Measured row, Retrospective silent sentence, cited TSV**: holds. `artifacts/M-23/T-206/dense-run.tsv` exists (ignored by `.gitignore:25`, as the answer says). Its summary rows match `### Measured` and the README table: `open` the cage 248, `check overlaps` 849, export 1,039, delete 1,119, and so on. No summary row is over 1,119.

**The `DenseRunProcessTests` change is not loosened.** Before, summary's count (lower faces) and full's count (pair lines) were in different units, and pairs are never fewer than lower faces, so the `>=` had slack. Now both sides count the same thing, open edges plus distinct overlapping faces. A `full` that fell back to the grouped line still fails: the grouped line reads "UVs that overlap", not "UVs overlap", and "edges belong", not "belongs", so it adds nothing. Like the old check, it is a floor. It does not pin the 280 pair lines, and it cannot see `Other` being dropped (the second finding).
