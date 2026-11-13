---
description: >-
  One suite proves the request's done-when: both exports, Load, LoadAnimationSet, and a loaded frame matching Carve's preview
---
# T-48 — Orcward done-when scenario

**Status:** In Progress

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

### Hand-off

Model: Sonnet 5.5 (claude-sonnet-5-5). Stopped at the spec's stop: **the small twisted limb passes Orcward's rule.**

**Done and committed (verified by `scripts/run-orcward-scenario.ps1`, 6 checks passing; not yet `cairn task T-48 verify`):**
- `scenarios/Carve.Scenario.Orcward` (`OrcwardScenario.cs`, `GlbEdit.cs`, `ComparisonPng.cs`), `tests/Carve.OrcwardLoad.Tests`, `scripts/run-orcward-scenario.ps1`, suite `scenario-orcward` in `tests.json`, both projects in `Carve.slnx`. `ScenarioDriver.CaptureToolAsync(tool, name, args)` added to the debug extension (`CaptureAsync` is `render` through it) so `render_glb` frames are captured like any other.
- The scenario builds the gardener with both looping clips through the tools, exports model, clips and hat piece with no warnings, and compares `render` with `render_glb` of the exported file at rest (front, side), Idle 0.5/1.0/1.5 s (front) and Walk 0, 0.125, 0.25, 0.5, 0.875 s (side). 0.125 and 0.875 (Walk) and 0.5/1.5 (Idle) lie between keys. Each frame (both sides) has a positive pixel check from the framing's bounds (can face, its mirror in X and its flip in Y from the front; near hand, its flip and its mirror from the side). Table: `differences.tsv`; triptychs in `compare/`.
- Load checks: Load (22 bones, Idle and Walk, no warnings), `LoadAnimationSet` of the clips file (same bones incl. inverse binds, same clips), the hat piece (same bones and inverse binds, no clips), no export warnings.

**Measured (512 x 512 = 262,144 px; fail past 1,311 px):** all ten good frames 0 px except Walk 0.25 s side, 2 px. Breaks: reversed winding 39,415 (rest), 31,622 (Walk 0.5 side), 38,184 (Walk 0.5 front); dropped piece (body only) 26,979; swapped bone order (UpperLeg_L/R in `skins[0].joints`) 0 at rest, 4,027 (side), 3,147 (front); swapped inverse binds (UpperLeg_L/R) 0 at rest, 697 (side, passes), 9,809 (front). An untouched rewrite of the file: 0.

**QUESTIONS for the person:**
1. **The small twisted limb passes.** Hand_L turned in `render`'s pose against the clean `render_glb` frame at rest, front, the rule counting the whole frame: Y 45 deg 159 px (0.061 %), Y 90 244 (0.093 %), X 45 339 (0.129 %), Z 45 284 (0.108 %), Z 90 599 (0.229 %); the character covers about 42,200 px of that frame, so counting only the character's pixels the same twists are 0.38 %, 0.58 %, 0.80 %, 0.67 % and 1.42 % (the Y 45 one still under 0.5 %). Y 180 could not be drawn: `render` refuses the posed mesh (a quad of the hand ring cannot be triangulated). D-6 says this must fail. Options are the person's: count only the character's pixels (D-6's named alternative, which needs a threshold the person sets), or a smaller region or a lower limit. I changed nothing in the rule. The twist rows are measured and written to the table but not asserted.
2. Rest frames carry no palette (`render_glb` with no clip draws the bind pose), so a wrong bone order or wrong inverse binds is invisible at rest by construction (0 px) and only a clip frame catches it; and seen from the side a shift in X is along the view, so the swapped inverse binds pass from the side (697 px) and fail from the front (9,809). The checks therefore require each wrong file to fail at one frame at least, not at every frame. Say if that is acceptable.

**Left:** the person's answer on the twist (then assert the twisted-limb row), the review set of comparison frames (`C:\Users\andre\artifacts\carve\M-19\T-48\round-1` holds a first selection: rest, Walk 0.125 side, Idle 0.5 front, the twists Y 90 and Z 90, swapped inverse binds from the front, dropped piece), the byte-identical guard by hash of every other suite's frames and files against the milestone head (not run: the debug extension changed, but only by an added method), the `CLAUDE.md` layout rows, `cairn task T-48 verify`, `### As built`, `cairn lint`, `finish`.
