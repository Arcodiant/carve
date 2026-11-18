---
description: >-
  The gardener's shoulder is coloured as its sleeve
---
# T-84 — The gardener's shoulder is coloured as its sleeve

**Status:** Done

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-47

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The gardener shows no stray skin colour at the top of the arm.

## Scope

- **The fault:** in T-47's review round 1 (`~\artifacts\carve\M-19\T-47\round-1`), the person noted on "glb Walk 0 s +x": "Something wrong with the top of the arm - there's a little piece of flesh colour at the top instead of green". `render`'s own frame of the same `.carve` at the same clip and time shows the same patch, so it comes from the gardener as built in `scenarios/Carve.Scenario.Character/GardenerSpec.cs` (T-47 suspected the arm lathe or the torso's neck band), not from either renderer. Find the cause and fix it there.
- **Everything built from it** follows: `samples/gardener.carve` and `samples/gardener-soft.carve` (kept equal by content), and the character, soft and rig scenarios' frames and their pixel checks, and T-47's `RenderGlbTests` if they read the region.
- **Existing frames:** only frames showing the fixed region may change. Hash every suite's frames before and after, and list each changed frame with why. Every other frame stays byte-identical.
- **Review:** the changed frames, before and after, cropped to the shoulder, through `mcp__waystone__review`.

## Notes

- Added 2026-11-12 on the person's ruling: a member of its own, before T-82, so T-82 re-baselines every frame once, after this fix.
- **Carried from the M-1 to M-4 plans:** a positive pixel check on the fixed region, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Sonnet 5.5.

- **Cause:** not the arm lathe. The torso lathe's last segment, from the chest ring (y 1.02, r 0.15) to the neck ring (y 1.12, r 0.07), was grouped `skin`. It is the slope of the shoulders, the sleeves start on it, so a cone of flesh showed at the top of each arm. Fix: `GardenerSpec.ShoulderSegment = "shirt"` for that segment (colour only: the face count, vertices, bones and weights are unchanged; 8 faces move from the skin group to shirt). The cap above stays skin (the neck).
- **Rebuilt from it:** `samples/gardener.carve` and `samples/gardener-soft.carve` (copied from the scenarios' `work/` files, byte-equal to them as before). `tests.json`: `server-regression` now lists `samples/gardener.carve` as an input (RenderGlbTests reads it; the first verify carried a stale pass for it).
- **New check** (`CharacterScenario.CheckRest`, rest front): for each side, the shoulder-slope face (found by nearest shirt face to (+-0.04, 1.07, 0.1), failing if its centroid is not between y 1.02 and 1.12) projected under the rest-bounds framing reads shirt; the same point flipped upside down is not shirt. *Brought up to date (2026-11-17, RV-52, 45ac156):* the shoulder slope's face now reads against the palette lit at its normal (`ExpectGroup` with the face's normal, `GardenerPixels.ClassifyLit`), as the rest checks do since the sky rebase; the flipped point's `ExpectNot` keeps the bare palette.
- **Break table:**

| Mutation | Result |
|---|---|
| slope segment back to skin | fails at gardener-rest-front: no shirt face on the slope |
| camera flipped in Y (point sampled upside down) | fails at gardener-rest-front: (280, 341) reads trousers, expected shirt |
| camera mirrored in X | not shown failing for this check (the slope is symmetric); the existing can-on-the-left check catches it |

- **Hashes** (png and glb, baseline = a run of the milestone head 91438b0 of character, soft and rig; every other suite's frames were byte-identical to the previous job's): changed frames all differ only in the shoulder region (bbox about x 223-308, y 157-191 of 512; the hat-close frames at their bottom edge):
  - character: 19 full-body frames (idle x3, pose, rest front/side/three-quarter and reopened, walk x4 plus reopened and loop-end, walk-pose x2, bones x2, weighed x2) and `gardener.glb`, `gardener-unweighted.glb` (a colour group changed); `gardener.hat.glb` unchanged.
  - soft: flat rest, flat walk, smooth rest and walk (and reopened), the four hat-close frames (the shoulder at their bottom edge), `gardener-flat.glb`, `gardener-soft.glb`.
  - rig: `rig-bones-before/after`, `gardener-rigged.glb`.
  - cube, crate, shed, export, viewer-cube: all identical.
- **Review:** person approved the shoulder crops (round-1).
- `verify` passed with all 11 suites run fresh.
