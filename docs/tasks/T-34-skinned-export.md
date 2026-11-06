---
description: >-
  export writes a skinned .glb with one mesh, a primitive per piece and colour, skins[0] and the clips, byte-identical run to run, which Empyrean's loader reads
---
# T-34 — Skinned export

**Status:** Open

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
