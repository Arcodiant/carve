---
description: >-
  An agent defines named clips of rotation and translation keys and renders any clip at any time
---
# T-33 — Clips and the sampler

**Status:** Open

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
