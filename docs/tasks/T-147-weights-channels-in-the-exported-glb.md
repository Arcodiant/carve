---
description: >-
  export writes one glTF weights channel per clip that keys weights, on the node holding the morph mesh, skinned or not
---
# T-147 — Weights channels in the exported .glb

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-144

**Verify:** server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

A clip's weight tracks reach the `.glb` as a glTF `weights` channel, so a game plays them without patching the file. Every export of a model with no weight tracks writes the bytes it wrote before.

## Scope

- **Skinned `model` export** (`GlbRig.WriteClips`, `SkinnedExport.cs`):
  - Each clip that keys any weight gets one `weights` channel on the mesh node. It's written with SharpGLTF's `Animation.CreateMorphChannel` and `float[]` weights (probed at planning: 1.0.7 writes and reads LINEAR and STEP).
  - Its output covers every target of the glTF mesh, in `mesh.extras.targetNames` order. A target the clip doesn't key holds 0 at every key.
  - Key times are the union of the clip's weight key times, with 0 and the duration added, as `Keyed` does for bones. Each target's value at a time is `Clip` sampling (T-144), so a written key between a target's own keys carries the sampled value.
  - STEP and LINEAR: glTF has one interpolation per channel.
    - A clip whose tracks are all STEP writes a STEP channel, and all LINEAR writes LINEAR.
    - A clip that mixes them writes one LINEAR channel, each STEP track expressed by holding its value and adding a key 1e-4 s before each of its next keys (the person, 2026-11-19).
    - The tests sample the file at every key and between keys, and match Carve's sampling.
    - Stop and ask if 1e-4 s collides with a real key or the duration.
  - A weights-only clip also writes today's held bone-0 rest channel, so the engine's `Duration` (which counts bone channels only, probed on 0.129.0) is the clip's.
- **Unskinned `model` export** (the person, 2026-11-19: skeleton-less clips are allowed):
  - **Static** (`GlbExport.Write`): the one node holding the mesh gets one weights channel per clip, keyed at 0 and at the duration as above.
  - **Part tree** (`GlbParts`): when every target lands on a single glTF mesh used by one node, that node gets the channel. When targets span two or more glTF meshes, or a mesh shared by instances, `export` refuses, naming the clip and the parts (the person, 2026-11-19).
- **`clips` mode** (no mesh): bone channels are written as now. Each clip's weights are omitted, with a warning naming the clip and its targets, and a weights-only clip is a held rest channel carrying its duration (the person, 2026-11-19).
- **`piece` mode** writes no animations, as now.
- **The result text** names the clips that carry weights, only when some do, so other exports' text is unchanged (as T-107 did for targets).
- **Tests:**
  - `unit`: for each mode and case above, read the file back with SharpGLTF (`GetMorphSampler`). Every key time and every value equals the clip's sampling, target by target, and unkeyed targets are 0 (RV-24's lesson: the exported keys are compared with the model). Also each refusal and warning.
  - `ExportHashTests`' pinned hashes are unchanged, and so is every existing `.glb` the suites export.
  - `server-regression`: the engine's loader (0.129.0) parses each new file with no warnings. A skinned weights-only clip's `Duration` equals the clip's, and an unskinned file's targets read back through `ParsedMorphTargets`.
  - Show breaks failing: swapped target order, a dropped unkeyed target, keys off by one, STEP written as LINEAR.

## Notes

- **Decides the format the game reads:** Opus.
- **The engine doesn't read weights channels** (planning probe, 2026-11-19): `AnimationClip` holds only `BoneTrack[]`, and the split load ignores `weights` channels, in 0.129.0 and 0.131.0. This task writes standard glTF and checks it with SharpGLTF. T-148 records the engine need and draws the weights.
- **File ownership:**
  - This task owns `GlbRig.cs`, `SkinnedExport.cs`, `GlbExport.cs`, `GlbParts.cs`, `ClipsExport.cs`, `ExportTool.cs` and their tests.
  - Beside it run T-145 and T-146. Don't edit `CarveFile.cs`, `ClipTools.cs`, `ModelStore.cs` or the render files.
- **Stop and ask:** a collision of the 1e-4 s held key, any existing `.glb` whose bytes would change, and re-taking a pinned hash.
- **Write down for dependents (As built):** the channel's layout (node, key union, value order), and the mixed-interpolation rule as landed. T-148 samples exactly this.
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-147` line given.
