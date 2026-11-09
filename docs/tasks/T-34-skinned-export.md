---
description: >-
  export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
---
# T-34 — Skinned export

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-33

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

`export` writes a skinned `.glb` of the whole model, and Empyrean's loader gets every piece, bone and clip.

## Scope

- **Structure:** as D-6's *A skinned export is one glTF mesh*, *The rest pose is the bind pose* and *Export modes* say: one mesh, one primitive per piece and colour, an `Armature` node, `skins[0]` with `inverseBindMatrices`, `JOINTS_0` and `WEIGHTS_0`, all in the default scene.
- **Clips:** one animation each, LINEAR or STEP samplers, keyed at 0 and at the duration on every animated channel.
- **Unweighted vertices:** a tool error listing them, the file untouched. `allow_unweighted: true` exports anyway, bound to the first root bone, and the result says so.
- **Warnings, never refusals,** as D-6's *Warnings for a game's limits* says: triangles, colours or submeshes, and humanoid height. The numbers are named constants.
- **A model with no skeleton** exports as T-30 left it, and with several meshes merges them the same way.
- **Result:** names the path, pieces, materials, bones and clips, and repeats every warning.
- **Tests, `unit`:** through `IModelGeometryLoader.Parse`, with no `Warnings`, submeshes equal to the pieces written, bone names and order equal to the given list, clip names, and `Duration` equal to the duration asked for. Inverse binds checked against rest world transforms. A deliberately breadth-first skeleton. Weights sum to 1 per vertex. Each warning at and just past its limit, and no height warning on a rig without the Synty bones.
- **Byte identity:** two exports in one process, and across two `carve` processes, including a model with several clips.

## Notes

- Run on the stronger model: the file layout is what the game depends on.
- Records **engine need 4**: cite it at the merge, `// Interim until engine need 4: ...`.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Two agents, both on Opus 5.5: the first built the code and tests to a hand-off; the second (this note) updated the expected schema, added three tests the break table asked for, ran the regression and scenario suites, the hashes and the break table, and finished.

**File layout** (what T-46, T-50 and T-36 build on; `GlbRig` is the shared half, `SkinnedExport` the model mode):
- **Nodes:** node 0 `Armature`, identity, no transform, not a bone. Nodes 1..n the bones in the skeleton's own order, each a child of its parent bone or, for a root, of `Armature`; local transform the rest position and rotation, no scale. Node n+1 the mesh node, named for the file (the path's stem), identity, `mesh: 0`, `skin: 0`. `scene: 0`, whose roots are `[0, n+1]`.
- **Skin:** `skins[0]`, named for the file, `joints` = nodes 1..n in bone order, `inverseBindMatrices` = the inverse of each bone's rest world transform, composed in double from the root down and inverted as a rigid transform, narrowed once.
- **Mesh:** one glTF mesh named for the file; one primitive per piece and colour, pieces in the model's order, colours in order of first face, uncoloured counted as a colour. Attributes `POSITION`, flat `NORMAL`, `JOINTS_0`, `WEIGHTS_0` (no UVs). Weights narrowed to float with the rounding put on the largest influence; a vertex with none is bound wholly to bone 0, the first root bone.
- **Materials** (ruled 2026-11-09, accepted as built): uncoloured faces `<mesh>`; `<mesh>:<group>` when a face group holds exactly that primitive's faces (the first such group); otherwise `<mesh>:<rrggbb>` in lower case. A group name that is another primitive's `<rrggbb>` in the same mesh would give two materials one name, so that primitive falls back to its own `<mesh>:<rrggbb>`, and a mesh name may not hold `:` (refused by `define_mesh`, by every `mesh` argument and on reading a Carve file), so the names are unique in the file (RV-24 B4, ruled 2026-11-09). Metallic-roughness, single-sided, T-30's base colour, metallic and roughness.
- **Animations:** one per clip in the model's clip order, named for the clip; a rotation and/or translation channel per animated bone, LINEAR or STEP as the channel says, keyed at 0 and at the duration (the first key held back to 0, the last held on to the duration). Pose values, clip keys and the sampler's output are absolute local transforms per bone. A clip with no channels holds bone 0's rest rotation at 0 and the duration so it loads with its duration (ruled: accepted). Bone rotations and rotation keys are written normalised (ruled: accepted).
- **No skeleton:** one mesh exports exactly as T-30 left it (scenario-export's `crate.glb` byte-identical); several meshes merge through `Model.Combined` into T-30's static path and keep T-30's static names (`carve`, `carve #RRGGBB`) (ruled: accepted). The `NamedMeshTests` export case changes from refusal to that merge, as the spec lifts it.

**`export`.** New argument `allow_unweighted`. With a skeleton: unweighted vertices are refused and listed per mesh, nothing written; "unweighted" counts every vertex of the mesh (ruled: accepted). `allow_unweighted: true` binds them to the first root bone and the result says how many. The result names the path, pieces, materials, bones and clips, triangles, size and bounds, and repeats every warning.

**Warnings** (ruled: skinned exports only, as named constants, as D-6 says): `SkinnedExport.MaxTriangles` 5000, `MaxSubmeshes` 8 (submeshes or colours), `MinHeight` 1.4 / `MaxHeight` 2.4 m only on a skeleton holding every `SyntyRequiredBones`; each past its limit, never at it. They are Orcward's numbers; T-51 (reserved, off-milestone) makes them per game, named in a comment at the constants.

**Engine needs.** Need 4 cited at `SkinnedExport` (`// Interim until engine need 4: ...`). Need 6 recorded in D-1: the Synty preset's bone per role, read from Synty's rig for `SyntyRequiredBones`.

**Tests.** `SkinnedExportTests` (unit, 17) through `IModelGeometryLoader.Parse`, SharpGLTF and the JSON chunk; `SkinnedExportToolTests` (regression, 3, incl. two `carve` processes). Expected-schema update allowed by the brief: `ExportToolTests.Tools_list_lists_export_with_a_closed_schema` now expects properties `path, allow_unweighted` and the message "its arguments are path, allow_unweighted" (the brief named `EditToolTests.Tools_list_shows_each_schema`; export's schema is checked in `ExportToolTests` instead). The second agent added `Bone_rotations_and_rotation_keys_are_written_normalised`, `Each_channel_keeps_its_interpolation` and a narrowing case in `Four_influences_narrowed_to_float_still_sum_to_1`, since the first break run showed those three breaks caught by nothing.

**Evidence** (in `M-16/artifacts/review/T-34/`): `hashes-before.txt` and `hashes-after.txt`, 86 files each from scenario-cube, scenario-crate, scenario-shed, scenario-export (incl. `crate.glb` and `crate.carve`) and viewer-cube: none changed, none missing, none new (T-34 adds no frames, so no new pixel check). `break-table.txt` (23 breaks) and `break-table-rerun.txt` (the four the first run left uncaught or unbuilt, each failing a test after the added tests). Not broken separately: rotation keys left unnormalised (covered by the same normalisation test). `server-regression` PASS (352 s).
