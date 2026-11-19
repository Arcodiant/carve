---
description: >-
  A clip keys morph target weights by target name, LINEAR or STEP, beside or without bone channels, sampled and checked in the Core
---
# T-144 — Weight tracks in a clip, in the Core

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

A `Clip` in `Carve.Core` can carry weight tracks, keyed by target name, beside its bone channels or on their own. It is sampled to weights at a time, checked for loop closure, and validated against the model's targets. A model with no skeleton may hold clips that key only weights. Every tool, the file format and the export build on this type.

## Scope

- **Type** (`src/Carve.Core/Clip.cs`):
  - `Clip` gains `Weights`: an ordered list of `(Target, ClipChannel<float>)`, in the order given. It is empty for every existing clip.
  - It reuses `ClipChannel<T>`, `ClipKey<T>` and `ClipInterpolation`, so LINEAR mixes linearly and STEP holds the earlier key. Times are clamped to the end keys, as bone channels are.
  - The spec type gains a weights part beside `BoneChannelsSpec`.
- **`Clip.Create` validation** adds these, each worded as the bone refusals are and placed by path (`weights.<target>.keys[i]`):
  - a target given twice;
  - a track with no keys;
  - a time outside the clip;
  - keys out of order or repeated;
  - a value that is not finite or is outside [0, 1], naming the target;
  - an interpolation other than LINEAR or STEP.
  - A clip with no bone channels and at least one weight track is valid.
- **Against the model** (`Model.WithClip` / `Model.Create`):
  - A weight track's target must be a name in `Model.TargetNames`. A target name is one dial across the model (D-6). The refusal lists the targets, as the bone refusal lists the bones.
  - With no skeleton, a clip with only weight tracks is valid. A clip with bone channels still needs a skeleton (the existing refusal).
- **Sampling:** `Clip.SampleWeights(time)` returns `TargetWeights`-shaped name → weight for the keyed targets only. Unkeyed targets are not in it, so callers hold them at 0. It is bit-exact and engine-free (no `Math.Pow`; mix as `a + (b - a) * t`, matching the bone path's style).
- **`ClipChecks`:** a looping clip whose weight track's last key differs from its first by more than **0.001** warns, naming the clip, the target and the difference (the person, 2026-11-19). The constant sits beside the 1° and 1 mm constants.
- **A keyed target can't be lost** (the person, 2026-11-19). Any model edit that would leave a clip keying a target name no mesh has any more is refused, naming each such clip. That covers `WithoutTarget`, a mesh replaced with its targets dropped, and a mesh removed. The refusal sits in Core, where the model is assembled, so every tool path meets it.
- **Tests (`unit`):**
  - sampling LINEAR and STEP at keys, between keys and before and after the end keys;
  - a weights-only clip on a skeleton-less model;
  - a mixed clip;
  - each refusal, one test each;
  - the closure warning at 0.0009 (silent) and 0.0011 (warns);
  - the lost-target refusal through each removal path;
  - every existing clip test unchanged.

## Notes

- **Decides an API others build on** (T-145, T-146, T-147 and T-150 read `Clip.Weights` and `SampleWeights`): Opus.
- **Rulings that bind (2026-11-19, the person, at planning and in issue #1):**
  - weights are a separate part beside the bone channels, not a reserved name inside them;
  - they're keyed by target name across the model;
  - weights are in [0, 1];
  - skeleton-less weights-only clips are allowed;
  - closure tolerance 0.001;
  - losing a keyed target is refused.
  - Layering a weights clip over a bone clip waits: one clip is sampled at a time.
- **File ownership:** `Clip.cs`, `ClipChecks.cs`, `Model.cs`'s clip and target paths, and their tests. Don't touch `CarveFile.cs` (T-145) or anything in `Carve.Server`.
- **Stop and ask:** any rule the above doesn't settle, for example whether STEP's last key holds past the duration (it should, as bones do), or a renamed target if a rename exists anywhere. Also ask before changing an existing refusal's words.
- **Write down for dependents (As built):** the type's shape, the `SampleWeights` contract (keyed targets only), the exact refusal texts, and the lost-target rule's entry point.
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-144` line given.
