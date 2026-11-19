---
description: >-
  define_clip takes a weights argument keyed by target name, get_clips returns it, a model with no skeleton may have weights-only clips, and render with clip and time draws the weights
---
# T-146 — define_clip keys weights, and render draws them

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-144

**Verify:** server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent keys morph target weights with `define_clip` and reads them back with `get_clips`. `render` with `clip` and `time` draws the sampled weights, as it already draws a skinned clip.

## Scope

- **`define_clip`** (`src/Carve.Server/ClipTools.cs`):
  - A new optional argument `weights: {<target>: {interpolation: LINEAR|STEP, keys: [{time, value}]}}`, beside `channels` (the person, issue #1). `channels` becomes optional.
  - A clip needs one of the two, and the refusal says so.
  - With no skeleton, a clip with `weights` alone is accepted. `channels` given without a skeleton keeps today's refusal.
  - Errors are placed by path (`weights.Smile.keys[1]`) in the existing style. Core's refusals (T-144) come through in its words.
- **`get_clips`** returns `weights` in the same shape, omitted when the clip keys none. A bone-only clip's result text is unchanged.
- **The store** (`ModelStore`): `delete_target`, `define_mesh` and `open` with `drop_targets`, and any replace that would lose a target a clip keys are refused, naming the clips (T-144's rule surfaced in each tool's words).
- **`render`** (`RenderTool.cs`, `RenderSpec`, `RenderPump`): with `clip` and `time`, the clip's sampled weights are the targets' weights. Unkeyed targets are 0, and the bone pose is as today.
  - `targets` naming a target the clip keys is refused, naming the clip and the target (the person, 2026-11-19).
  - `targets` naming targets the clip doesn't key still applies, so the arm's "Curl at t=1 with Bulk 1" frame is unchanged.
  - A weights-only clip on a skeleton-less model draws unposed with its weights.
- **Content:**
  - Rewrite `define_clip`'s and `get_clips`' descriptions with a worked weights example: a `Blink` keying `Blink_L` and `Blink_R`, STEP and LINEAR. Mark it and run it, as `get_mesh`'s target example is.
  - `render`'s `targets` text gains the clip rule.
- **Tests:**
  - `ToolHarness` (`unit`): each new argument and refusal, the get_clips round trip, the lost-target refusals through each tool, and the clip-plus-targets rule.
  - `server-regression`: a real `render` of a two-target mesh at a weights clip's key and between keys, equal pixel for pixel to `render` with the same weights given as `targets`. Also a shown-failing break (the clip's weights ignored).
- **Pins:** `tools/list` digests (`PartToolsListTests` and the like) will move with the new descriptions. Show that only these tools' text moved, and ask before re-taking a digest.

## Notes

- **Mostly surface work on T-144's rules:** Sonnet. The render path change is small, but it is rendering, hence `server-regression`.
- **Rulings that bind:** as in T-144, plus the clip-plus-targets rule above.
- **File ownership:**
  - This task owns `ClipTools.cs`, `ModelStore.cs`'s clip and target paths, `DefineTargetTool.cs`, `DefineMeshTool.cs`'s drop path, `OpenTool.cs`'s drop path, `RenderTool.cs`, `RenderSpec`, `RenderPump.cs` and their tests.
  - Beside it run T-145 (`CarveFile.cs`, and the version lines of `SaveTool.cs`/`OpenTool.cs`) and T-147 (export). Don't edit those lines or files. A conflict that only adds is settled by keeping both sides.
- **Every existing frame stays byte-identical:** hash every frame suite's frames before and after (render is something everything draws through).
- **Write down for dependents (As built):** the argument shape, as a schema excerpt; the exact refusal texts; and how `RenderSpec` takes a clip's weights (T-148 matches it on the `.glb` side).
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-146` line given.
