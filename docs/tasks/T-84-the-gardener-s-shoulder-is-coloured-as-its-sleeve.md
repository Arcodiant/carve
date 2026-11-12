---
description: >-
  The gardener's shoulder is coloured as its sleeve
---
# T-84 — The gardener's shoulder is coloured as its sleeve

**Status:** Open

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-47

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The gardener shows no stray skin colour at the top of the arm.

## Scope

- **The fault:** in T-47's review round 1 (`C:\Users\andre\artifacts\carve\M-19\T-47\round-1`), the person noted on "glb Walk 0 s +x": "Something wrong with the top of the arm - there's a little piece of flesh colour at the top instead of green". `render`'s own frame of the same `.carve` at the same clip and time shows the same patch, so it comes from the gardener as built in `scenarios/Carve.Scenario.Character/GardenerSpec.cs` (T-47 suspected the arm lathe or the torso's neck band), not from either renderer. Find the cause and fix it there.
- **Everything built from it** follows: `samples/gardener.carve` and `samples/gardener-soft.carve` (kept equal by content), and the character, soft and rig scenarios' frames and their pixel checks, and T-47's `RenderGlbTests` if they read the region.
- **Existing frames:** only frames showing the fixed region may change. Hash every suite's frames before and after, and list each changed frame with why. Every other frame stays byte-identical.
- **Review:** the changed frames, before and after, cropped to the shoulder, through `mcp__waystone__review`.

## Notes

- Added 2026-11-12 on the person's ruling: a member of its own, before T-82, so T-82 re-baselines every frame once, after this fix.
- **Carried from the M-1 to M-4 plans:** a positive pixel check on the fixed region, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.
