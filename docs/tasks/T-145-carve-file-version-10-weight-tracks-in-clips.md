---
description: >-
  The Carve file holds a clip's weight tracks as version 10, written only when some clip keys a weight
---
# T-145 — Carve file version 10: weight tracks in clips

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-144

**Verify:** core-unit, server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

`save` and `open` carry a clip's weight tracks bit-exact, in Carve file version 10. Every model that has no weight tracks writes byte for byte what it writes today.

## Scope

- **`CarveFile` version 10** is version 9 (T-132: version 8's targets with version 7's `parts` and `levels`) plus one key on a clip: `weights`, after `channels`.
  - `weights` is an object of target name → `{interpolation, keys:[{time, value}]}`, in the clip's track order.
  - The number formatting is the bone channels'.
  - A weights-only clip writes `"channels": {}`.
  - With no part tree, version 10 writes no `parts` or `levels`, as version 8 does not. With no targets it can't occur, since a track needs a target.
- **The lowest version that holds the model** (the person, 2026-11-19: one version 10):
  - Version 10 only when some clip has a weight track.
  - Every other model writes what it writes today (9, 8, 7 or lower).
  - Every committed `.carve` is byte-identical when written again.
- **Reading** takes version 10. Every problem is named by where it is (`clips[2].weights.Smile.keys[0].value`), as T-101 and T-132 do. Reading refuses what `Model.Create` refuses, including a track keying an unknown target, and clips on a skeleton-less model (allowed only when every clip is weights-only).
  - Today's read refuses clips without a skeleton (`CarveFile.cs` near 652). It relaxes to exactly T-144's rule.
- **Pieces:** `save mesh=` / `open mesh=` of a mesh writes no clips, as today.
- **Descriptions:** add version 10 wherever `save`, `open` or CLAUDE.md's Carve file line name the versions. A wording change searches every text that says the same thing.
- **Tests (`unit`):**
  - a skinned model with a mixed clip round-trips by content and writes the same text twice;
  - so does a skeleton-less model with a weights-only clip, and a part-tree model with targets and a weight clip;
  - each writes version 10;
  - removing the weight tracks drops each one to its old version;
  - each version 10 refusal, one test each;
  - all samples are byte-identical.

## Notes

- **Decides a format others build on:** Opus.
- **File ownership:** `CarveFile.cs` and its tests, the version lines in `SaveTool.cs` and `OpenTool.cs`, and CLAUDE.md's Carve file line (this task alone edits it in M-32). T-146 and T-147 run beside it: don't touch `ClipTools.cs`, `ModelStore.cs`, the render files or export.
- **Stop and ask:** any existing file whose bytes would change, and anything the rulings don't settle.
- **Write down for dependents (As built):** the version 10 layout, with one short example of a weights-only clip.
- **Carried:**
  - every committed `.carve` byte-identical, by hash before and after;
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-145` line given.
