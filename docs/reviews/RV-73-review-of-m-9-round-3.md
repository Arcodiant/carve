---
description: >-
  Review of M-9, round 3
---
# RV-73 — Review of M-9, round 3

**Status:** Answered

**Reviews:** M-9

**Round:** 3

**Previous:** RV-72

**Diff:** 5995362897a6cb090210410bd0e376ee4dbf5049 against 631a596

**Reviewer:** claude-opus-5-5, one reviewer confirming RV-72's two answers

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Summary

Both of RV-72's answers hold, and nothing else in the fix commits (`c8198f2..9502dea`) is wrong. No findings.

**Nit 1, the shear test (`b3af71f`).** `Trs.Decomposes` (src/Carve.Core/Trs.cs) is export's old `GlbParts.Close` check moved
unchanged: the 3x3 linear part, 1e-5 of the trace (at least 1). get_mesh and `GlbParts` now both call it, and both private
`Close` helpers are gone. Dropping get_mesh's old translation comparison loses nothing, because Decompose returns the
matrix's own translation. To check the test, I put the old `GetMeshTool.cs` back in a throwaway worktree:
`ExportPartsToolTests.A_shear_export_refuses_under_a_near_uniform_scale_is_one_get_mesh_warns_of` then fails, and every
other test in `ExportPartsToolTests` and `RenderPartArgumentTests` still passes. That includes the every-rover-part
no-warning check, so the stricter tolerance gives no false warnings on the rover.

**Nit 2, `Skinning.Posed` (`9bee533`, `e6c2ca4`, `11ec89c`).** `Posed` is gone, and no `.cs` file under src or tests still
refers to it. The doc comments that cited it now cite `Morphed` or `PosedOf`. The moved tests:
- `ModelMaterialTests`: the assignment check stays on the same unweighted mesh, both whole and by part. The materials-list
  check moved to `RenderPumpTests.A_posed_draw_keeps_each_faces_material_from_the_models_materials`, as ruled, and that
  test now checks per face, through the pump, the material looked up by name.
- `MorphTargetsTests`: skin-only is now compared with `Skinning.Positions` directly, which is stricter than comparing
  `Posed` with itself. Morph-before-skin is unchanged. The no-skeleton `Assert.Same` now checks the mesh instance in place
  of the model.
- `ShadingFileTests`, `LookMeshesTests`: the same assertions, with `PosedOf(..., null, null)` in place of `Posed(...).Combined`.
- `SkinningTests`: the per-mesh checks (plain mesh `Same`, colour kept, vertex moved, weights dropped) now run per part.
  There is one new check: the whole model is the posed part followed by the plain one.
- `GlbPartsTests`: the expected draw is still the baked model skinned, now through `PosedOf` on `Baked(model)`, an input
  that is already in model space. It still differs from the placed model's input, so the test still catches skinning done
  in part space. The two "as before" baselines (part-space skin, then place; each mesh skinned, joined at identity) are now
  built by hand from `Positions` and `Model.Join`. Leaving out skinned corner normals there changes nothing, because the
  test compares `Shading.Corners`, which ignores `PosedCorners`.

To check the new material test, I made `PosedOf` drop a skinned occurrence's style in the throwaway worktree. The RenderPump
test fails, and so does `SkinningTests.Posed_model_moves_only_weighted_meshes_and_keeps_colours`. `ModelMaterialTests` still
passes because its mesh is unweighted, which is what the old test checked too.

**Ran.** The full `dotnet test Carve.slnx` at `9502dea` passes: Carve.Core 1334, Carve.Server 1163, ModelViewer 26,
Patina.Graphs 98 and Patina.Server 103, with 0 failures. The patch-id was recomputed and matches the **Diff:** line.
