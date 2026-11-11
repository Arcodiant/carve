---
description: >-
  Review of M-18
---
# RV-27 — Review of M-18

**Status:** Answered

**Reviews:** M-18

**Round:** 1

**Previous:** —

**Diff:** `910a59a6ea122045d5da2d4f5aed0ffb796a799c against e9f4e8e` (the milestone at 46da626)

**Reviewer:** claude-opus-5-5 (three reviewers: T-42's range, T-43's range, the whole diff with T-44's range and the refactor)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: T-42's range (claude-opus-5-5)

### The normals look swaps the colours of corners B and C on smooth meshes

- **Where:** `src/Carve.Server/LookMeshes.cs:144-147` (`CornerColours`), against `src/Carve.Server/MeshBridge.cs:45-47`
- **Rubric:** 1 (correctness: the normals look)
- **What is wrong:** `MeshBridge.ToMeshData` reverses each triangle's winding for the engine: vertex `at` gets A, `at+1` gets C (`NormalC`) and `at+2` gets B. `CornerColours` writes its colours in A, B, C order, and `RenderSystem.Make` passes the two arrays to `CreateMesh(data, colours)` index for index. So on a smooth mesh each triangle's corner C is coloured with B's normal, and B with C's. Flat meshes are unaffected because their three colours are equal.
- **Concrete failure:** on the smooth cube's top quad, v1 is drawn in v2's colour. The gradient is permuted and does not match the spikes.
- **Why the tests miss it:** `LookMeshesTests.A_smooth_mesh_in_normals_carries_its_corner_normals_in_colour_and_spikes` (`tests/Carve.Server.Tests/LookMeshesTests.cs:136`) checks colours in the same A, B, C order. The frame checks compare only the channel order between distant points.
**Outcome:** fixed

`CornerColours` now writes each triangle's colours in `MeshBridge`'s A, C, B order. `LookMeshesTests.A_smooth_mesh_in_normals_carries_its_corner_normals_in_colour_and_spikes` compares each colour with the normal of the drawn vertex it lands on, and fails on the old order.

### A flat mesh beside a smooth one changes its normals-look spikes

- **Where:** `src/Carve.Core/Model.cs:266,280` (`Combine`) with `src/Carve.Server/LookMeshes.cs:175` (`Spikes`)
- **Rubric:** 1 (flat meshes stay bit-identical; ruling 5), 2
- **What is wrong:** if any mesh is smooth, `Combine` gives the combined mesh a `Smooth` shade, with every edge of each flat piece marked hard. `Spikes` then sees `HasCornerNormals(combined)` and draws corner spikes for the flat pieces' faces too, instead of centre spikes. `PosedCorners` does the same.
- **Concrete failure:** `define_mesh` A and B, then `set_shading smooth` on A only. B's `normals` render changes from a centre spike per face to a spike at every corner.
- **Why the tests miss it:** `ShadingTests.A_model_of_a_smooth_and_a_flat_mesh_combines_each_as_it_is` checks the normals only, and no frame shows a mixed model.
**Outcome:** fixed

`Model.Combined` now marks a flat piece's faces (`PolygonMesh.FlatFaces`, read through `Shading.FaceHasCornerNormals`), and `Spikes` draws those from their centres. `ShadingTests.A_flat_mesh_beside_a_smooth_one_keeps_its_faces_flat_in_the_combined_mesh` and `LookMeshesTests.A_flat_mesh_beside_a_smooth_one_keeps_its_centre_spikes_at_rest_and_posed` (at rest and posed) fail without it.

### Ruling 3 (a mis-wound shared edge is hard) has no test

- **Where:** `src/Carve.Core/Shading.cs:148` (`!edge.IsManifold`), and `tests/Carve.Core.Tests/ShadingTests.cs`
- **Rubric:** 2
- **What is wrong:** no test builds two faces that share an edge but are wound the same way.
- **Concrete failure:** replacing the check with `edge.Faces.Count == 2` would smooth across a flipped face, and every test would still pass.
**Outcome:** fixed

`ShadingTests.A_shared_edge_wound_the_same_way_by_both_faces_is_hard` turns the hinge's wall over and asserts that each face keeps its own normal to the bit. It fails with the check replaced by `edge.Faces.Count != 2`.

### Skinned `.glb` corner normals are untested in T-42

- **Where:** `src/Carve.Server/SkinnedExport.cs:81-95`
- **Rubric:** 2 (Scope: Skinning and Export)
- **What is wrong:** skinned export writes `Shading.CornersOf(mesh)` per corner. The render-against-loaded test (`ShadingToolTests.cs:30`) covers only the unskinned `GlbExport.Write`.
- **Concrete failure:** a wrong corner index or a fall-back to face normals in skinned export passes every T-42 test. T-44's SoftLoad checks may cover it.
**Outcome:** fixed

`SkinnedExportTests.A_smooth_mesh_is_written_with_its_corner_normals` asserts that every loaded normal of a skinned smooth cube points out through its corner. It fails when skinned export falls back to face normals.

### Verify omits the suites for the changed skinning and export paths

- **Where:** `docs/tasks/T-42-smooth-normals-and-hard-edges.md`, `**Verify:** server-regression, scenario-cube, scenario-crate`
- **Rubric:** 4
- **What is wrong:** the diff changes `Skinning.Posed`, `SkinnedExport`, `GlbExport` and `MeshBridge`, which `scenario-character` and `scenario-export` exercise, but the Verify line names neither. As built says those were guarded only in ad-hoc runs.
**Outcome:** no change needed: T-42 is Done and its Verify line is history. The milestone's own verify requires every regression suite, `scenario-character` and `scenario-export` among them, and the refactor pass ran both fresh at 46da626. `finish` runs them again after the RV-27 fixes.

### `MarkEdges` repeats a refused pair given in both orders

- **Where:** `src/Carve.Core/Shading.cs:80` (kept by refactor `edaa7db`)
- **Rubric:** 1 (error path)
- **What is wrong:** `edges.Distinct()` runs before the pairs are put lower id first, although the doc comment says each such pair is listed once.
- **Concrete failure:** `mark_edges {"edges":[[9,8],[8,9]]}` on the cube returns `edges[8-9]: not an edge…` twice.
**Outcome:** fixed

The refused pairs are now put lower id first before `Distinct`. `ShadingTests.A_refused_pair_given_in_both_orders_is_listed_once` fails on the old order.

### As built's ruling 4 wording differs from what `save` writes

- **Where:** `src/Carve.Core/CarveFile.cs:102`
- **Rubric:** 7
- **What is wrong:** As built says version 3 is written "only when a mesh is shaded". The code writes version 3 whenever `Shade` is non-null, which includes a flat mesh with only hard marks. The tool text, the remarks and `ShadingFileTests.Flat_with_marked_edges_is_kept_for_later` document the code's behaviour.
**Outcome:** fixed

T-42's As built now says that version 3 is written whenever a mesh has a shade, smooth or flat with hard marks, and that a file with neither stays version 2.

Refactors `edaa7db` and `46da626` keep T-42's behaviour. Core Shading unit tests: 24 passed. Not checked by this reviewer: the server tests, the scenarios and the frames; the cube-scenario thresholds.

## Findings: T-43's range (claude-opus-5-5)

### Subdivide gives weights to old vertices that had none, a rule nobody was asked about

- **Where:** `src/Carve.Core/Subdivision.cs:150-157` and `:199-206` (`Stencil.Blend`); As built in `docs/tasks/T-43-subdivide.md` says "an unweighted source adds nothing … landed and not asked".
- **Rubric:** 5, and the task's Notes ("Open rules: stop and ask").
- **What is wrong:** D-6 says only "blend weights with the same stencil". The member skips unweighted sources and renormalises what is left. As a result, an old vertex can change from unweighted to weighted.
- **Concrete failure:** a cube with vertices 0 to 3 on bone 0 and vertices 4 to 7 unweighted. After `levels 1`, vertex 4 comes out 100% bone 0. Skinned export, which refuses an unweighted vertex and used to name vertex 4, no longer does. A missing weight becomes a rigid bind without anyone noticing. Treating an unweighted source as zero weight, or refusing, would give different answers. The choice belonged with the person.
**Outcome:** fixed

The person ruled on 2026-11-10 that a vertex with no weights stays unweighted after `subdivide`, so skinned export still names it, and that a new point blends only its weighted sources, renormalised. `SubdivideTests.An_old_vertex_with_no_weights_stays_unweighted_and_a_new_point_blends_only_its_weighted_sources` (the reviewer's cube) and `SkinnedExportTests.A_subdivided_mesh_still_names_a_vertex_that_had_no_weight` fail without the fix.

### The "five-bone blend capped" assertion blends only four bones

- **Where:** `tests/Carve.Core.Tests/SubdivideTests.cs:213-214`; As built, Tests.
- **Rubric:** 2, 7
- **What is wrong:** the edge point of 0-4 blends bones {0,1,3,4}: four, so nothing is dropped. `Count == MaxInfluences` passes with or without the cap, and the comment "five bones, the smallest dropped" is wrong. The cap is exercised only indirectly, by the level-3 `InRange(1, 4)` at `:204`.
**Outcome:** fixed

The old assertion now names the four bones it blends. `SubdivideTests.Past_four_bones_the_smallest_are_dropped_and_the_rest_renormalised` blends seven bones at vertex 0 and asserts bones 0, 1, 3 and 4 at 5/11 and 2/11; it fails without the cap.

### Several sharp-edge, isolated-vertex and budget rules have no test that fails without them

- **Where:** `src/Carve.Core/Subdivision.cs:83`, `:93-105`; `src/Carve.Server/SubdivideTool.cs:62`; `tests/Carve.Core.Tests/SubdivideTests.cs`; `tests/Carve.Server.Tests/SubdivideToolTests.cs`.
- **Rubric:** 1, 2
- **What is wrong:**
  - **Dart:** a vertex on one sharp edge is never asserted. `creases.Length >= 1` as the corner test would pass.
  - **Isolated vertex:** dropping the `around.Count == 0` guard gives `(n-3)/n` with `n = 0`, a NaN position, and no test catches it.
  - **Triangle faces:** not tested.
  - **Budget boundary:** "past, never at" 5000 is tested only at 5376, so `>=` would pass.
  - **Whole-model `triangles` count:** never checked with two meshes.
**Outcome:** fixed

New tests: a dart (fails with `creases.Length >= 1`), a weighted vertex on no face (it loses its weight without the guard, and never a NaN, since a one-term stencil returns its vertex as it is), a tetrahedron's triangles, the budget at 5000 and 5001 (fails with `>=`), and `triangles` over two meshes (fails when it counts the edited mesh alone).

The face, edge and vertex points, the smooth, crease, boundary and corner rules, the D-1 id order, and the carrying of hard marks, groups and colours were found correct. Refactor `2969314` keeps the old behaviour. `core-unit` passes (J-20261110-231856-ac8a17). Not checked by this reviewer: server-regression and the scenarios; the frames; non-manifold "bowtie" vertices.

## Findings: the whole diff, T-44's range and the refactor (claude-opus-5-5)

### `scenario-soft` does not fingerprint the files it links in from other projects

- **Where:** `tests.json:198-222` (suite `scenario-soft`); the links are in `scenarios/Carve.Scenario.Soft/Carve.Scenario.Soft.csproj:16-20` and `tests/Carve.SoftLoad.Tests/Carve.SoftLoad.Tests.csproj:22-25`.
- **Rubric:** 6
- **What is wrong:** cairn's `dotnet-closure` does not follow `<Compile Include=... Link=...>` items. `scenario-soft` lists `GardenerSpec.cs` by hand but not the other linked files: `scenarios/Carve.Scenario.Crate/CrateBuild.cs`, `CameraPixels.cs`, `scenarios/Carve.Scenario.Character/GardenerPixels.cs` (refactor f3f5a06) and `tests/Carve.CharacterLoad.Tests/RunContent.cs` (refactor cf96c46).
- **Concrete failure:** an edit to `GardenerPixels.cs`'s 0.98 threshold changes what the soft scenario checks, yet `verify` carries `scenario-soft`'s old pass. `scenario-character` has the same gap for `CrateBuild.cs` and `CameraPixels.cs`, from before this branch.
**Outcome:** fixed

`scenario-soft` now lists `GardenerPixels.cs`, `CrateBuild.cs`, `CameraPixels.cs` and `RunContent.cs` by hand, and `scenario-character` lists `CrateBuild.cs` and `CameraPixels.cs`, which it links.

### CLAUDE.md layout rows do not reflect the two refactors

- **Where:** `CLAUDE.md`, the layout rows for Soft, SoftLoad.Tests, Character and CharacterLoad.Tests.
- **Rubric:** 7
- **What is wrong:**
  - The Soft row names only `GardenerSpec.cs` as linked. `GardenerPixels.cs` is linked too.
  - The Character and CharacterLoad.Tests rows do not say that they own files other projects link: `GardenerPixels.cs` and `RunContent.cs`.
  - The SoftLoad.Tests row drops ", so `dotnet test Carve.slnx` passes without it".
**Outcome:** fixed

The Soft row names every file it links. The Character and CharacterLoad.Tests rows name `GardenerPixels.cs` and `RunContent.cs` as linked by the soft projects. The SoftLoad.Tests row has its "so `dotnet test Carve.slnx` passes without it" back.

### The milestone's verify claim was made before the five refactor commits

- **Where:** `docs/milestones/M-18-soft-characters.md`, Retrospective ("`cairn milestone M-18 verify` (2026-11-10) passes … 1 ran, 9 carried"), written in 954faca.
- **Rubric:** 4
- **What is wrong:** commits f3f5a06, cf96c46, edaa7db, 2969314 and 46da626 came after that line. The retrospective should say so, or be re-dated. `scenario-soft` passed fresh at 46da626 (J-20261110-231935-f579f6).
**Outcome:** fixed

The retrospective now says the verify line was written at 954faca, before the refactor pass and the RV-27 fixes, and that the run that counts is `finish`'s.

### The soft export's "every warning is not-planar" check was not ruled on, and does not pin a count

- **Where:** `scenarios/Carve.Scenario.Soft/SoftScenario.cs:178-185`; `tests/Carve.SoftLoad.Tests/SoftLoadTests.cs:116-123`; the milestone Retrospective.
- **Rubric:** 5, 4
- **What is wrong:** T-43's ruling did not settle what T-44 should assert in place of "no warnings". T-44 chose "every warning contains `face is not planar`" without asking. That holds for any count, including a regression that triples it. T-72's Scope also says the checks "go back to asserting no warning but that one", which they never asserted.
**Outcome:** fixed

The person ruled on 2026-11-10 that, until T-72 lands, the check asserts that every export warning is a not-planar face and that their count equals `select faces where non_planar` on the same model. `SoftScenario.cs` selects on Body and Hat after the export and keeps the results in the run folder, and `SoftLoadTests` sums those results and asserts the count.

### Refactors f3f5a06 and cf96c46 change no behaviour (no defect)

- **Where:** `scenarios/Carve.Scenario.Character/GardenerPixels.cs`, `tests/Carve.CharacterLoad.Tests/RunContent.cs`
- **Rubric:** 1
- **What is wrong:** nothing. The moved code is token for token, and only the soft scenario's failure text now includes the pixel.
**Outcome:** no change needed: the reviewer reports no defect.

### T-44's checks can fail on the three named mutations (no defect)

- **Rubric:** 2
- **What is wrong:** nothing.
  - A mirrored camera is caught by the can-left and `ExpectNot` checks.
  - A skipped `subdivide` is caught by `triangles <= 680` and the load test's range.
  - A skipped crease is caught by `CheckHat(creased: true)`, the soft-against-uncreased byte check and the rim normals.
**Outcome:** no change needed: the reviewer reports no defect.

`cairn lint` was clean. Not checked by this reviewer: `scenario-character` and `CharacterLoad.Tests` were not run after the refactors; the content of `samples/gardener-soft.carve`; T-42 and T-43 internals.

## Summary

Round 1 over `git diff origin/main...46da626` (the milestone after its refactor pass). It was split three ways, all on Opus 5.5 at least as strong as every implementer:
- T-42's range (`origin/main...5c2428d`) plus refactors `edaa7db` and `46da626`, for shading, the file format, export and skinning;
- T-43's range (`5c2428d...5510213`) plus refactor `2969314`, for the Catmull-Clark and id rules;
- the whole diff, owning T-44's range, refactors `f3f5a06` and `cf96c46`, and what falls between the parts.

The person ruled on two findings on 2026-11-10:
- subdivide keeps an unweighted vertex unweighted;
- the soft export check pins the count of not-planar warnings to `select faces where non_planar`.

**Answers (2026-11-10).**
- All twelve code findings were fixed by an Opus 5.5 agent in commits e9f2d69, bac210c and 74aa87a, each with a test shown failing against the old behaviour.
- Of 121 frames, two changed, both M-18's own: `cube-smooth-normals` and `cube-creased-normals`, from the B/C fix. Every frame from before M-18 is byte-identical.
- The person approved the two changed frames in Waystone ("the smooth meshes look correct now, the old may have been bugged").
- Because code was fixed, a re-review follows in a new round.
