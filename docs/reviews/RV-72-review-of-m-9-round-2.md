---
description: >-
  Review of M-9, round 2
---
# RV-72 — Review of M-9, round 2

**Status:** Answered

**Reviews:** M-9

**Round:** 2

**Previous:** RV-71

**Diff:** 61a0d053e849c3b8b354fa5fa1a3a1330666f644 against 631a596

**Reviewer:** claude-opus-5-5, one reviewer checking RV-71's 26 answers and the fix commits

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### get_mesh's shear test is looser than export's, so a transform export refuses as a shear still gets no warning

- **Where:** `src/Carve.Server/GetMeshTool.cs:40-46` (`Close`: 1e-4 per element, floor 1) against `src/Carve.Server/GlbParts.cs:97-104` (`Close`: 1e-5 times the trace) (rubric 1)
- **What is wrong:** The fix for "get_mesh reports a TRS world for a transform that export refuses as a shear" (464e64e) adds its own `Close`, ten times looser than export's. That leaves a band where export refuses and get_mesh does not warn. A probe I ran and then removed put a `Crate` turned 30° about Y under a `Base` scaled `[1.0002, 1, 1]`. `GlbParts.Plan` refuses it as a shear once `Crate` moves, but `get_mesh mesh=Crate` gives no `world:` warning. At `[1.001, 1, 1]` both agree.
- **Consequence:** For a near-uniform ancestor scale, the case RV-71 named (export refuses, get_mesh says nothing) can still happen, so that answer holds only in part. The band is narrow and the TRS error in it is about 1e-4, so the harm is small. One shared test (export's `Close`, or a helper both call) would close it, and the "exact cases unchanged" test would still guard the other side.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** get_mesh and export share one shear test, Core's `Trs.Decomposes` (export's tolerance, 1e-5 of the trace), and both private `Close` copies are gone, so the two can't drift again. `ExportPartsToolTests.A_shear_export_refuses_under_a_near_uniform_scale_is_one_get_mesh_warns_of` puts the reviewer's case (a part turned 30° under an ancestor scaled `[1.0002,1,1]`) through export (refused) and get_mesh (warns), and fails without the get_mesh change. `Get_mesh_gives_no_world_warning_when_the_transform_is_exact` now checks every rover mesh, not three (b3af71f). (claude-opus-5-5)

### `Skinning.Posed` has no product caller left and still skins a placed part in part space

- **Where:** `src/Carve.Core/Skinning.cs:151-177` (`Posed`); its doc references at `src/Carve.Core/Model.cs:67`, `:70` (rubric 1, 3)
- **What is wrong:** After c6b8d2a, render's pose and clip draw through `Skinning.PosedOf`, and nothing in `src/` calls `Skinning.Posed`. It is still public. It still skins each mesh's part-local vertices and then places them (`WithPartsFrom`), which is the reading the person ruled against ("match the export"), and its doc doesn't say so. Tests still use it, sometimes as the oracle for a baked, unplaced model (`GlbPartsTests`, `SkinningTests`, `LookMeshesTests`). RV-71's two `Model.Piece` findings were answered by removing a method left like this.
- **Consequence:** The next caller that wants "the model posed" finds `Posed` first, and on a placed rigged part it brings back the mismatch with the export that RV-71's Major fixed. Either make it internal or test-only, or say in its doc that it is for an unplaced (or `SkinnedExport.Baked`) model and that `PosedOf` is what render draws.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `Skinning.Posed` is removed, and its doc (weights dropped, corner normals skinned, morph before skin) moves onto `PosedOf`. Its tests in `ModelMaterialTests`, `MorphTargetsTests`, `ShadingFileTests`, `SkinningTests`, `GlbPartsTests` and `LookMeshesTests` now go through `PosedOf` and keep what they checked. Where `Posed` was the oracle, the oracle is now written out: the skin alone is `Skinning.Positions`, the baked file is `PosedOf` of `SkinnedExport.Baked`, and "part space then placed" and "drawn as before" are built by hand from `Skinning.Positions` and `Model.Join` (9bee533). By the person's ruling, the material-list assertion in `Posed_keeps_the_materials_and_the_assignment` (now `Posed_keeps_the_material_assignment`), which only restated the source model's list, is replaced by `RenderPumpTests.A_posed_draw_keeps_each_faces_material_from_the_models_materials`: a posed render of two rigged meshes, one of them placed, looks each drawn face's material up in the materials `RenderPump` gives the scene, and it fails if those materials are dropped when posed or if a mesh takes another mesh's or no assignment (e6c2ca4, 11ec89c). (claude-opus-5-5)

## Summary

I checked each of RV-71's 26 answers against its finding and its cited commits (`git diff e835a72..baa515b`), and the fix commits themselves for new problems. 25 hold in full. One holds in part, the second get_mesh shear answer; see the first finding above.

| # | RV-71 finding (short) | Holds | How checked |
|---|---|---|---|
| 1 | A bare empty part lost by save/open | yes | reverted `CarveFile.cs`: `A_bare_empty_part_comes_back…` and `Each_part_key_alone…(empty)` fail |
| 2 | Plain parts reordered across save/open | yes | same revert: `Plain_parts_in_another_order…` and `(order)` fail |
| 3 | Core lets `instance_of` change | yes | the ruling holds: the rule is in `Model.PartProblems`, first, and nothing changes an existing part's `instance_of`. With it disabled, `WithPart_refuses_a_change…` and three `Define_part_refuses…` cases fail |
| 4 | CarveFile version doc says 8 | yes | read |
| 5 | `Model.Piece` uncalled | yes | removed; its tests on `PartPiece` keep their materials, skeleton and version checks |
| 6 | Labels/highlight on one mesh with instances | yes | reverted `RenderTool.cs`: the `NamedMeshTests` instance test fails |
| 7 | Frame checks share Core's functions | yes | new `SubtreeOf`/`CombinedOf`/`OffsetOf` tests use hand-worked numbers, with live and level counts that differ |
| 8 | T-94 paragraphs unpinned | yes | `Each_T94_paragraph_is_in_its_tools_description_once` read |
| 9 | get_mesh `world` Decompose ignored | yes | the ruling holds: it warns when inexact, and the exact-case test (90° turn and three rover parts) passes. Reverted: the warn test fails |
| 10 | `store.Current` read more than once | yes | each tool now has a single read (grep). The reason given for having no test holds: telling the two reads apart needs another call to land between them, which a test can only do with a product seam or a flaky race |
| 11 | "naming the part to use instead" | yes | `PartSpace`/`PartTarget` and keep_level text read |
| 12 | Open question: rule belongs in Core | yes | as 3 |
| 13 | server-unit inputs miss six files | yes | each is in `tests.json` and linked in the csproj |
| 14 | PartsAcceptance Layout row and inputs | yes | row, `dotnet-closure` input, csproj comment |
| 15 | Tree limits / skinned moves untested | yes | I removed `Baked`'s `WithWeights`: the skinned tool test fails. The limits test asserts 576 with instances counted |
| 16 | Material-sharing test can't fail | yes | the claim now matches what the test sees, and the comment says why the `shared` dictionary is not observable. I did not re-check that SharpGLTF writes content-equal materials once |
| 17 | T-97 path control characters | yes | no control bytes; `repr` shows the backslashes |
| 18 | T-96 As built timeout wording | yes | read |
| 19 | Placed rigged part: part vs model space | yes | the ruling holds: `PosedOf` and `weigh` place before skinning or weighing. Reverted `RenderPump.cs` and `ModelStore.cs`: `Render_and_render_glb_agree_on_a_posed_placed_rigged_part` and `Weigh_weighs_a_placed_part…` fail. The Core `PosedOf` test compares with `Posed(Baked(model))` |
| 20 | `save mesh=` edit-tool hint | yes | reverted `SaveTool.cs`: two `PartPieceToolTests` cases fail. `open mesh=` texts unchanged |
| 21 | `export mode=piece` "no mesh named" | yes | reverted `PieceExport.cs`: two `PieceExportToolTests` cases fail |
| 22 | get_mesh TRS for export's shear | in part | warns for clear shears, but its tolerance is looser than export's (finding 1) |
| 23 | Part sentence missing on mesh tools | yes | grep: `PartTarget` on 11 tools, `PartSpace` on project_uvs, pinned by `PartToolsListTests`. The planar wording is fixed |
| 24 | CLAUDE.md layout row, frame count | yes | read |
| 25 | Retrospective/changelog wording | yes | read |
| 26 | `Model.Piece` no product caller | yes | as 5 |

**The two claims tested:**
- `WithPart_refuses_each_bad_field_by_name`'s changed line loosens nothing. `new Part("Body") { InstanceOf = "Wheel" }` is still refused; only the first problem's text changed. With the new rule disabled, the same call gives the old "an instance cannot also own geometry", and `Placement` still yields it second. The file path keeps that text (`PartTreeFileTests`).
- The `store.Current` reason holds (row 10).

**What I ran:** the Core and Server unit suites at `baa515b` (`Tier!=regression`): 1334 and 926 passed. I also ran each revert or sabotage above in place, built and ran the named tests, and restored with `git checkout HEAD -- src`. The shear probe (finding 1) was a temporary test file, deleted afterwards. The patch-id was recomputed and matches.

**What I did not cover:** no device or scenario runs. I did not check the claim that 155 frames and 17 `.glb` files of scenario-character, soft, rig, orcward and morph are byte-identical. I did not re-review the milestone outside the fix commits.
