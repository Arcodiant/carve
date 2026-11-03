---
description: >-
  The crate scenario builds a crate from one cube with move and extrude only, and shows it from five views in its own suite
---
# T-12 — Crate scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-2

**Batch:** — (standalone)

**Depends on:** T-9, T-11

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The milestone's sample, the crate, is built through the real tools in its own suite, and its frames show it from every side and close up.

## Scope

**`scenarios/Carve.Scenario.Crate`** (new, one project, laid out as `CLAUDE.md`'s *Writing a scenario* says):

1. `define_mesh` with the example cube, then only edit tools: the crate's 12-call sequence (T-10's Notes). Assert after every call: no tool error, and the counts expected at that step. Assert at the end: closed, 72 vertices, 70 faces, no warnings, bounds `(-0.02, 0, 0)` to `(1.02, 0.8, 1)`.
2. Capture, all agreed by the person:
   - `view front`;
   - `view back`;
   - `direction [-2.2, 1.2, 1.6]`;
   - `direction [0.4, 2, 0.8]`;
   - `direction [2, 1, 1], zoom 3, target [1.02, 0.4, 0.5]`.
3. Render `front` again and `AssertBytesEqual` against the first.
4. **Undo**: undo the last extrude. The +X bar's standing part is gone, so the front frame differs from step 2 and has no bar pixels at the sampled point. Repeat that extrude, and the frame is byte-identical to step 2's front.
5. **Error path**: `extrude` the +Y cap with an inset of 0.6, then with an unknown face id. Each is a tool error naming the face, and the next render is byte-identical to the last.

**A positive pixel check on every frame** (the M-1 retrospective's rule). Background in the corners for each whole-mesh frame, and in addition:

- **front**: the top, +Z and +X frame faces in M-1's order, at least 10/255 apart. The +X bar's top differs from the +X panel behind it by at least 10/255.
- **back** and **[-2.2, 1.2, 1.6]**: the -X bar shows: a sampled pixel on its top differs from the panel behind it by at least 10/255.
- **tilted top**: the lid's recess wall is visible: a pixel on it differs from the lid panel by at least 10/255.
- **zoom**: the mesh fills the frame's centre, and the bar's top and front faces differ by at least 10/255. Its corners are not checked as background, because the zoomed frame may run off the mesh's edge.

Say why each pixel was chosen.

Pick the sample pixels from the first good run, record them as constants with a comment, as `CubeScenario` does, and **show each check failing** on a broken product, such as the bar extrude skipped or the zoom ignored.

**`Carve.slnx`** lists the new project, and **`tests.json`** gains a `scenario-crate` suite, `regression` tier, on the model of `scenario-cube`. Add the `Crate` row to `CLAUDE.md`'s *Layout* table.

**Second run**: run the scenario twice and compare the two runs' frames byte for byte. Record it in the closing report.

**Show the person** all the frames (`mcp__waystone__review` where available, kind `approve`) before this task lands. Their approval is the gate. The planning previews in `artifacts/m2-probe/view-*.png` are approximate: say how the real frames differ from them.

## Notes

- **Decided by the person:**
  - The crate is 1 x 0.8 x 1, frame 0.12, panels recessed 0.08, a handle bar on each X side standing 0.02 proud.
  - It is built from one example cube with edit tools only.
  - The views listed above. The head-on top view was rejected for being flat.
- **Open rules: stop and ask.** Any change to the crate's shape or the sequence, any view or zoom not listed, any pixel tolerance looser than 10/255, and any golden or cross-machine comparison. The sixth view (`[0, -1, 0]`) is in T-9's cube scenario, not here.
- **A fixer may not change** the crate, the views, the light or the framing to make a check pass. If a frame is not what the person agreed, stop and report.
- This task is the run and its frames, split from the code on purpose. A defect found in a tool is a bug filed under M-2, not a fix here, unless it is a one-line fix in this task's own files.
- Five frames, plus the undo and error frames: look at them together at the end.
- Nothing here has sound or motion, so no listening or real-time pass is planned.
- Leave the worktree before `finish`.
