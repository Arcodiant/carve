---
description: >-
  One suite proves the request's done-when: both exports, Load, LoadAnimationSet, and a loaded frame matching Carve's preview
---
# T-48 — Orcward done-when scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-45, T-47, T-50, T-82, T-83

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Every line of Orcward's *Done when* passes in one suite.

## Scope

- **`scenarios/Carve.Scenario.Orcward`**, suite `scenario-orcward`, on the gardener with both looping clips:
  1. export in both modes, with no warnings;
  2. `IModelGeometryLoader.Load` gives the expected bone count and clip names;
  3. `LoadAnimationSet` reads the clips-only file with the same bones and clips;
  4. a piece file from T-50 loads with the same bones and inverse binds;
  5. `render_glb` and `render` frames compared at the rest pose, and at several times of each clip, including times between keys.
- **The comparison:** start from D-6's rule, Orcward's: a pixel differs past 8 of 255 in R, G or B, and a frame fails past 0.5 % of its pixels. Give a table of measured differences by frame. If a good frame fails, stop and ask the person. Do not loosen it yourself.
- **Show it failing:** a swapped bone order, wrong inverse binds, a dropped piece, reversed winding, and **one small limb twisted**, such as a hand. If the small twist passes the rule, stop and ask: D-6 says it must fail, and a whole-frame count may be too coarse.

## Notes

- If the frames differ at a key, that is a bug and not a tolerance: find it.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Models: the first agent and this one ran on Sonnet 5.5 (claude-sonnet-5-5).

**What exists.** `scenarios/Carve.Scenario.Orcward` (`OrcwardScenario.cs`, `GlbEdit.cs`, `ComparisonPng.cs`), `tests/Carve.OrcwardLoad.Tests`, `scripts/run-orcward-scenario.ps1`, suite `scenario-orcward` in `tests.json`, both projects in `Carve.slnx`, and `ScenarioDriver.CaptureToolAsync` in the debug extension (`CaptureAsync` is `render` through it). The scenario builds the gardener with Idle and Walk, exports model, clips and the hat piece with no warnings, and compares `render` with `render_glb` of the exported file at rest (front, side), Idle 0.5/1.0/1.5 s (front) and Walk 0, 0.125, 0.25, 0.5, 0.875 s (side); 0.125, 0.875 (Walk) and 0.5, 1.5 (Idle) lie between keys. Every frame, both sides, has a positive pixel check from the framing's bounds (the can face and its X mirror and Y flip from the front; the near hand and its flip and mirror from the side). The load checks: `Load` (22 bones, Idle and Walk), `LoadAnimationSet` (same bones with inverse binds, same clips), the hat piece (same bones and inverse binds, no clips).

**The rule, as the person ruled on 2026-11-13.** Orcward's 8/255 and 0.5 % stay for shading noise; a frame also fails with a connected patch of differing pixels past 50 px. Connectivity is 8 (diagonal neighbours join): a limb's outline is diagonal, so 4-connectivity would split one patch into pieces and weaken the rule, and 8 is the stricter reading. 50 px separates with a wide margin: the largest patch in any good frame is 2 px; the smallest in any twist is 130 px. Written into D-6 *A loaded frame against the preview* with its date and the table. Second ruling, recorded there too: the suite fails on every wrong file, but which frame catches it may vary (rest frames carry no palette, so a swapped bone order or inverse bind needs a clip frame).

**Measured (512 x 512 = 262,144 px, 1,311 px is 0.5 %)**

| Frame | Differing px | Largest patch |
|---|---|---|
| ten good frames | 0 each, Walk 0.25 s side 2 | 0 each, Walk 0.25 s 2 |
| untouched rewrite of the file (control) | 0 | 0 |

**Shown failing (each fails; a wrong file at one frame at least)**

| Wrong | Frames: differing px / largest patch |
|---|---|
| Hand_L twisted Y45, Y90, X45, Z45, Z90 (rest, front) | 159/130, 244/135, 339/249, 284/275, 599/597 (all fail by the patch; none by count) |
| swapped bone order | rest 0/0 (passes); Walk 0.5 side 4,027/4,027; front 3,147/2,456 |
| swapped inverse binds | rest 0/0 (passes); Walk 0.5 side 697/268; front 9,809/5,484 |
| reversed winding | rest 39,415/39,401; side 31,622/31,604; front 38,184/38,184 |
| dropped piece (body only) | rest 26,979/26,589 |

Y 180 of the hand could not be drawn: `render` refuses the posed mesh (a quad of the hand ring cannot be triangulated).

**Hash guard.** Against `origin/milestone/M-19` (exported to a temp folder and built there), the frames and every `.glb` of scenarios cube, rig, crate, shed, export, character and soft are byte-identical (same SHA-256 on every file: 32, 10, 55, 10, 6, 25, 13 files).

**Review.** Waystone round-2 (12 images: good frames, five twists, swapped bone order and inverse binds, dropped piece, reversed winding) approved; the person's notes ("what's up with these legs", "missing a hat") are the expected failures shown.

**Not checked:** a fresh-agent run (T-49).
