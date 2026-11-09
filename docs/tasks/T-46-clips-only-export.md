---
description: >-
  export in clips mode writes an armature, skin and animations with no mesh, which LoadAnimationSet reads with the same bones and clips
---
# T-46 — Clips-only export

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-34

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

One file of clips serves every character in the game.

## Scope

- **`export` with `mode: clips`:** armature, `skins[0]` and animations with no mesh, as D-6's *Export modes* says. It refuses a model with no skeleton or no clips.
- **Tests, `unit`:** `ParseAnimationSet` gives the same bones in the same order and the same clips as the model file, including a breadth-first skeleton; `Parse` throws `glTF contains no meshes.`; byte identity. `ClipRetargeter` takes the clips onto a second Synty-named skeleton of other proportions without throwing, so the file is proved as a retargeting source.
- **Description:** says what the file is for, and that role maps and events are sidecars the game writes.

## Notes

- Moved into M-16 on Orcward's answer: M-16 is meant to prove retargeting in the game, so the clip file cannot wait for M-19.
- The skin stays even with no mesh: the probe in D-6 shows why. A fixer may not drop it.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Sonnet 5.5.

**`export` with `mode: clips`.** New argument `mode`, `"model"` (the default, byte-identical to T-34's) or `"clips"`; `piece` is T-50's and is refused for now ("mode: expected ..."). Expected-schema update allowed by the brief: `ExportToolTests.Tools_list_lists_export_with_a_closed_schema` now expects properties `path, allow_unweighted, mode` and "its arguments are path, allow_unweighted, mode". `ClipsExport` (`src/Carve.Server/ClipsExport.cs`) writes the file with `GlbRig.WriteArmature`, `WriteSkin` and `WriteClips`, the same steps as `SkinnedExport`, so bones, inverse binds and animations are the model file's. A model with no skeleton or no clips is a tool error that writes nothing ("mode clips: the model has no skeleton ..." / "... no clips ..."). The result line: `exported <file> as clips, with no mesh: <n> bone(s) '...', <m> clip(s) '...'`. An existing file is replaced (reported), as for the model mode. No game-limit warnings in this mode (no mesh; T-51 owns the limits).

**Clips file layout (T-36 checks it).**
- `nodes`: node 0 `Armature` (identity: no translation, rotation or scale, not a bone); nodes 1..n the bones in the skeleton's own order (breadth-first kept), each a child of its parent bone or, for a root, of `Armature`; local transform = rest position and normalised rest rotation, no scale. No other node, and no node has `mesh` or `skin`.
- `scenes`: one scene, `scene: 0`, roots `[0]`.
- `skins`: exactly one, `skins[0]`, named for the file (path stem), `joints` = nodes 1..n in bone order, `inverseBindMatrices` = the inverse of each bone's rest world transform (as in the model file).
- `animations`: one per clip in the model's clip order, named for the clip; per animated bone a rotation and/or translation channel, LINEAR or STEP, keyed at 0 and the duration (first key held back to 0, last held on to the duration); a clip with no channels holds bone 0's rest rotation at 0 and the duration. Rotations written normalised.
- No `meshes`, `materials`, `textures` or `images`. The bytes do not depend on the model's meshes, only on the skeleton, clips and file name.

**Tests.** `ClipsExportTests` (unit, 6): `ParseAnimationSet` has the model file's bones (equal `SkeletonBone` records, breadth-first, 18 Synty-named bones) in order and the same clips (names, durations, every channel's times, values, interpolation); `Parse` throws `glTF contains no meshes.`; JSON layout above; byte identity and mesh independence; refusals; `ClipRetargeter.Retarget` of each clip, read back from the clips file, onto a second Synty-named skeleton (hips 0.7 against 1.0, shorter legs, spine and arms) without throwing, with unit rotations, finite translations and the hips height scaled to the target. Two regression tests in `SkinnedExportToolTests` through the real `carve` (clips mode round trip against the model file, `mode: model` byte-identical to the default, replaced, bad mode, no clips, no skeleton). The test project now references `Arcodiant.Empyrean.Animation` (for `ClipRetargeter`) and links `ClipsExport.cs`.

**Role map.** The Synty preset needed `Clavicle_L/R` as well as the bones in `SyntyRequiredBones` for the second skeleton (engine need 6's list names the role bones; the clavicles are in the test rig). Engine docs for `ParseAnimationSet`, `Parse`'s message and `ClipRetargeter` were read from the 0.119.0 Geometry cache and the Animation `.nupkg` fetched from the feed (not in the package cache). No new engine need.

**Evidence** in `M-16/artifacts/review/T-46/`: `break-table.txt` (each new check failing on a broken product), hash lists.

**Hashes** (own jobs `J-20261109-145432-695fc0` scenario-cube, `-145434-c3747b` scenario-crate, `-150030-1c8cf3` scenario-export, all at the rebased commit, checked against T-34's `hashes-after.txt`): every frame and `crate.glb` unchanged; the only difference is `crate.carve`, now version 2 (sha256 `4fc73e0f...fa95e`) from T-35, not this member. No new frames, so no new pixel check. Post-rebase `verify` passed (`server-regression` 266 s).
