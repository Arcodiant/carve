---
description: >-
  An agent defines named clips of rotation and translation keys and renders any clip at any time
---
# T-33 — Clips and the sampler

**Status:** In Progress

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-32

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent writes named clips and looks at any moment of them.

## Scope

- **Core:** `Clip` as D-6's *Clips* says, and a sampler from clip and time to `Pose`. Refuses: an interpolation other than LINEAR or STEP, an unknown bone, a key outside `[0, duration]`, duplicate or unsorted key times, a non-positive duration, a duplicate clip name.
- **Sampler:** read the engine's docs and match its interpolation, including the sign of a rotation between keys. Record what it does in *As built*: T-47's frames depend on it.
- **Tools:** `define_clip(name, duration, loop, channels)`, where a repeated name replaces the clip; `delete_clip(name)`.
- **Render:** `clip` and `time`, exclusive with `pose`.
- **Sample:** the two-bone chain's wave clip, with a worked example in the description.
- **Tests:** `unit` for each refusal, LINEAR and STEP at and between keys, and a bone with no keys holding rest; two frames at different times.

## Notes

- Run on the stronger model: a mismatch here shows up only at T-48.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

One agent, on Opus 5.5.

- **Core:** `Clip` (`src/Carve.Core/Clip.cs`): name, duration (seconds, > 0), `Loop`, and per bone `BoneChannels` of an optional rotation and an optional translation `ClipChannel<T>` (keys sorted by time, LINEAR or STEP). `Clip.Create(name, duration, loop, channels, skeleton)` returns the clip or every problem by place (`channels.joint.rotation.keys[1]`): an empty name, a duration not > 0 or not finite, an unknown bone (the bones listed), a bone given twice or with neither part, an interpolation other than `LINEAR` or `STEP` (exact, as glTF writes them; left out is LINEAR, as glTF assumes), a channel with no keys, a key time outside `[0, duration]`, a time the same as or before the key before it, a non-finite value, a rotation not unit within the skeleton's 1e-3. `Model` keeps clips in definition order through every mesh and skeleton change: `WithClip` replaces a clip of the same name in its place, `WithClips` refuses two clips of one name (the core refusal of a duplicate name), `WithoutClip`, `FindClip`.
- **Sampler (`Clip.Sample(time)` to a `Pose`), matched to the engine's docs** (Empyrean 0.119.0, read in the package cache and, for Animation, the 0.119.0 package from the local feed): `Geometry/docs/api/AnimationInterpolation.md` (LINEAR: translation lerps, rotation *slerps* along the shortest arc; STEP holds the *earlier* key until the time reaches the next; both clamp outside the keys, holding the first or last key, with no wrap), `AnimationChannel_T_.md` and `BoneTrack.md` (a channel left null holds the bind pose), and `Animation/docs/api/ClipPoseProvider.md` (`Time` is clamped to `[0, Duration]` at evaluation; `Loop` wraps only in `Advance`). So: the time is clamped to `[0, duration]`; before the first key and after the last a channel holds that key; at a key time it is that key exactly; between keys STEP gives the earlier key and LINEAR lerps translation and slerps rotation by `System.Numerics.Quaternion.Slerp`, which negates the later key when the dot product is negative (shortest arc: a key's sign never sends a bone the long way round), lerps when the keys are almost equal, and does not normalise. A bone, or a part, with no keys is left out of the pose, so it rests. Values are absolute local transforms (the person's ruling), as `Pose`'s are. The docs do not give the slerp's numerics (the near-equal fallback, normalising): recorded as **engine need 5** in D-1, cited at `Clip.Slerp`; the gap is at float rounding.
- **Store:** `ModelStore.DefineClip` (built from the current skeleton under the lock, one undo step, says whether it replaced) and `DeleteClip` (one step; an unknown name lists the clips). T-31's rule, clips half: `DefineSkeleton` is refused when clips exist and the bone names or order differ (same names, new rest transforms accepted), naming the clips and saying to `delete_clip` them.
- **Tools** (`src/Carve.Server/ClipTools.cs`): `define_clip(name, duration, loop, channels)`, `channels` as bone name to `{rotation, translation}`, each `{interpolation, keys: [{time, value}]}`; a repeated name replaces; returns `{name, duration, loop, bones, replaced, clips}`. Its description carries the worked example, the two-bone chain's `wave`: `joint` from rest to a quarter turn about +Z at 0.5 s and back at 1 s, looping. `delete_clip(name)` returns `{deleted, clips}`.
- **Render:** `clip` and `time`; the clip is sampled in `RenderTool` and drawn as a pose, so framing stays on the rest bounds. Errors: clip with pose, clip without time, time without clip, an unknown clip (the clips listed), a time outside `[0, duration]`.
- **Sample:** `ChainPose.WaveAsync` in `scenario-crate`, after every T-32 frame: the wave clip, the render errors, frames `chain-wave-0.25-z` and `chain-wave-0.5-z`.
- **Pixel check** (head-on from +Z, framing on the rest bounds, points on the +Z faces at z 0.2 projected by `CameraPixels`): at 0.25 s the slerp is half way, an eighth turn, so the upper box leans up and LEFT along (-sin 45, cos 45) from the joint at (0, 1). Points 0.5 and 0.8 m up that axis must be the lit face colour (within 12 of the rest frame's lower box), and their mirrors in X (a turn the other way, or a mirrored camera) background; (0, 1.7), where the box stands at rest (a STEP or a time ignored), and (-0.7, 1.0), where it lies at 0.5 s, are at least 0.29 m off the leaning box's centre line (its half width is 0.2) and must be background; the lower box is unchanged; corners in the image. At 0.5 s the clip is the bent pose of T-32 exactly, so `chain-wave-0.5-z` must be byte-identical to `chain-bent-z`.
- **Readings the spec left open:** `render` refuses a time outside the clip rather than clamping or wrapping it (the sampler itself clamps, as the engine does); `time` is required with `clip`; `loop` defaults to false; `interpolation` is matched exactly (`linear` is refused) and defaults to LINEAR; a bone with neither part, a channel with no keys and a bone given twice are refused; the duplicate-name refusal lives in `Model.WithClips`, since `define_clip` replaces by name; clips are not saved by `save` (as weights are not). `EditToolTests.Tools_list_shows_each_schema` gained `clip` and `time` in render's argument list (new arguments, as T-29's ruling 2). The loop and in-place warnings of D-6 are T-45's.

**Checked.** All 79 existing frames of `scenario-cube`, `scenario-crate`, `scenario-shed` and `scenario-export` hashed before any change (`M-16/artifacts/review/T-33/hashes-before.txt`, matching T-30's `hashes-after.txt` and T-32's `hashes-crate-after.txt`); after the change all 42 existing crate frames are byte-identical (`hashes-after-crate.txt`), and of the two new ones `chain-wave-0.5-z` is byte-identical to `chain-bent-z`, as the scenario asserts. Cube, shed and export use no code this changed (their frames come from the shared render path, untouched without `clip`); `verify` runs what its inputs call for. The person approved both wave frames (2026-11-09). Break table (`break-table.txt`, script `breaks.py`, same folder): 19 product breaks, each failing at least one new unit test or the crate scenario: slerp without the sign flip, STEP as LINEAR, LINEAR as STEP (also fails the 0.25 s arm check), STEP taking the later key, the fraction from the wrong end, the sampled rotation conjugated (fails the 0.5 s byte check), render ignoring `time` (fails the 0.5 s byte check), the first key not held, the time wrapped instead of clamped, an unkeyed bone set to identity, each refusal removed, the duplicate-name refusal off, a repeated name adding, and the clips half of the skeleton rule off. A posed result mirrored in X stops the scenario at T-32's earlier checks; the wave's mirror checks fail under the conjugate break, which draws the same image for this chain. `server-regression`'s only failure before the schema update was `Tools_list_shows_each_schema` (render's new `clip` and `time`).

### Hand-off

Opus 5.5, handed off at the tool-call checkpoint (about 75 calls), with everything but landing done.

- **Done:** the Core clip and sampler, the store, `define_clip`/`delete_clip`, render's `clip`/`time`, the wave sample, unit tests, engine need 5, As built. `cairn task T-33 verify` passed (5 suites ran, none carried) on commit `99e2d48`; the verify run's 44 crate frames are byte-identical to `hashes-after-crate.txt`. The person approved both wave frames. Break table complete (19 breaks, each caught).
- **Left:** commit this section, `cairn lint`, `cairn task T-33 finish` from inside this worktree (read its output whole), then from `M-16` `git pull --rebase` and confirm `Merge task/T-33` in `git log`.
- **Surprising:** the Animation package's 0.119.0 docs were not in the package cache (only 0.114/0.115); they were read from the 0.119.0 `.nupkg` on the local feed. The break script's first run lost three rows to warnings-as-errors (`if (false)` is unreachable code); they were rerun with non-constant patches.
