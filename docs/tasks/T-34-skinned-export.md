---
description: >-
  export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
---
# T-34 — Skinned export

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-33

**Verify:** server-regression, scenario-cube, scenario-crate

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

### Hand-off

Checkpoint at about 60 tool calls (Opus 5.5). Code committed; not yet verified or finished.

**Done.**
- `src/Carve.Server/GlbRig.cs`: the reusable rig half (for T-46/T-50): `WriteArmature` (node `Armature` first, identity, then one node per bone in skeleton order, roots under `Armature`, rotations normalised in double), `WriteSkin` (joints in bone order, inverse binds = inverse rest world, composed in double), `WriteClips` (one animation per clip, LINEAR/STEP, each animated channel keyed at 0 and the duration holding the nearest key; a clip with no channels holds bone 0's rest rotation so it still loads with its duration).
- `src/Carve.Server/SkinnedExport.cs`: one glTF mesh named for the file, a primitive per piece and colour (meshes in order, colours by first face, uncoloured counted as a colour), materials `<mesh>` (uncoloured), `<mesh>:<group>` (a group holding exactly that primitive's faces, first such group), else `<mesh>:<rrggbb>` lowercase; `POSITION`, flat `NORMAL`, `JOINTS_0`, `WEIGHTS_0` (float rounding put on the largest weight); nodes `Armature`, bones, then the mesh node (identity, mesh 0, skin 0); default scene set (`scene: 0`, roots `[Armature, mesh node]`). Warnings (constants `MaxTriangles` 5000, `MaxSubmeshes` 8 for submeshes or colours, `MinHeight` 1.4 / `MaxHeight` 2.4 only when the skeleton holds every `SyntyRequiredBones`). Engine need 4 cited; engine need 6 recorded in D-1 (the Synty role-to-bone gap).
- `ExportTool`: `allow_unweighted` argument; with a skeleton the skinned path (unweighted vertices refused and listed per mesh, file untouched; allowed binds to bone 0 and says so); several meshes without a skeleton merge through `Model.Combined` into T-30's static path (refusal lifted); description updated. One-mesh no-skeleton path untouched.
- Tests: `SkinnedExportTests` (unit, 15, through `IModelGeometryLoader.Parse`) and `SkinnedExportToolTests` (regression, 3, incl. two `carve` processes) pass; `NamedMeshTests` export case changed from refusal to merge (the spec lifts it). Server unit tier: 255 pass.

**Left.** Expected-schema update for `export`'s new `allow_unweighted` (likely `EditToolTests.Tools_list_shows_each_schema` and any unknown-argument message for export, "its arguments are path"); a full `server-regression`; `scenario-export`/`scenario-cube`/`scenario-crate`/`scenario-shed` after-hashes against `M-16/artifacts/review/T-34/hashes-before.txt` (86 files, fresh, matching T-33's 79); break table; `### As built`; add `scenario-export` etc. to `**Verify:**` if verify asks; `verify`, `lint`, `finish`.

**Readings the spec left open (to report).** Uncoloured material named `<mesh>`; group naming rule above; several meshes with no skeleton use T-30's static path via `Model.Combined` (materials `carve`/`carve #RRGGBB`, not `<mesh>:`); warnings only on skinned exports; a channel-less clip holds bone 0's rest rotation; rotations written normalised; unweighted counts every vertex of the mesh (as `set_weights`' `unweighted`).

