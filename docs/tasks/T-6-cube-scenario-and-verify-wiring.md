---
description: >-
  The cube scenario renders a cube and an L-block from both views, proves byte-equal frames and runs in verify
---
# T-6 — Cube scenario and verify wiring

**Status:** Done

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** T-5

## Goal

The milestone's whole path runs as a scenario on every verify, and its frames show a cube and a second shape from both sides.

## Scope

**`scenarios/Carve.Scenario.Cube`** (new, one project per scenario, referencing `Carve.Debug` and `Carve.Extensibility` only), using only `define_mesh` and `render`:

1. Cube, with the same vertices and faces as the `define_mesh` description's example. Capture `front`, `back`.
2. Render `front` again; `AssertBytesEqual` on the decoded pixels against the first.
3. An L-block: a closed prism with two concave hexagon caps and six quad sides, so an n-gon and ear clipping are seen. Capture `front`, `back`.
4. A long box, 4 x 0.5 x 0.5, off-origin, proving framing. Capture `front`.
5. A cube with one face wound backwards: `define_mesh` warns; capture `front` as a kept record of what that looks like.
6. Error path: a bad index returns a tool error, and the previous mesh still renders.

Pixel checks, in the scenario: background in the four corners; in both cube views three sampled faces differ by at least 10/255 in the expected order; the L-block's notch shows background in `front`; the long box touches no image edge.

**`scripts/verify.ps1`**: after the tests, runs `run-scenario.ps1 cube` and fails if it does, with a short comment that verify needs a Vulkan device.

**Second run**: `run-scenario.ps1` is run twice in this task and the two runs' frames compared byte for byte, recorded in the closing report.

Add the `scenarios/` row to `CLAUDE.md`'s *Layout* table and remove its *The first milestone adds...* line.

Look at every frame, kept in `artifacts/`, and show the person the cube `front`, cube `back` and L-block `front` frames for the look's approval before this task lands.

## Notes

- **Decided by the person:** byte equality on one machine, plus pixel checks; frames kept to be looked at; no committed goldens in M-1; verify runs the scenario, so verify needs a Vulkan device.
- No golden tolerance is chosen. Anything that would add one, or loosen the 10/255 check, goes back to the person.
- Six frames at one size: look at them together at the end.
- Say which frames were looked at in the closing report.

### As built

`CheckDrawn` has one floor per frame (RV-2): L-block back 47,000, long box 14,000, flipped cube 26,000, about half each. The L-block front is 50,000, 56% of its 88,829, because half would pass the L-block cut to its caps (49,732); with that patch the scenario fails.

Ran on Sonnet 5.5 (`claude-sonnet-5-5`).

**Files.** `scenarios/Carve.Scenario.Cube` (`CubeScenario`, `CubeExtension`), added to `Carve.slnx`; `scripts/verify.ps1` runs `run-scenario.ps1 cube` after the tests (comment: needs a Vulkan device); `CLAUDE.md` Layout row added and the "first milestone adds" line removed. `run-scenario.ps1` found the project under `scenarios/` as is.

**Frames (8, `artifacts/scenarios/cube/<n>/frames/`).** cube-front, cube-back, cube-front-again, lblock-front, lblock-back, longbox-front, cube-flipped-front, after-error-front. All eight were looked at (one contact sheet, plus the person's review in `G:\My Drive\artifacts\carve\M-1\T-6\round-1`): **approved** with no notes, judged on cube front, cube back and L-block front. The flipped-face cube loses its +Z face (back-face culled); that is what the record shows.

**Checks in the scenario.** Four background corners on every frame; cube front top > +Z > +X and back -Z > -X > underside, each step at least 10 in G (T-4's sample points, hard-coded; `back` is the as-built order, per the person); cube front repeat byte-equal; L-block closed, no warnings, notch pixel (321, 185) is background in front (the sight line through (1.8, 1.8, 1) never meets the solid; position computed from T-4's framing maths); long box: every border pixel is background; flipped cube warns; a bad index is a tool error and the next render is byte-equal to the frame before it.

**Drawn-pixel checks (review RV-1).** The L-block front and back, long box, flipped cube and after-error frames each also need at least 14,000 non-background pixels of 262,144 (measured 88,829 and 94,688, 28,563, 53,660 and 53,660; about half the smallest). With a temporary patch that turned the camera 1000 units aside from the fourth render on, the scenario failed at `lblock-front` (0 drawn) and the patch was reverted. The checks were not each shown to fail on their own frame. The frames stayed byte-identical.

**Two runs.** `run-scenario.ps1 cube` run twice (folders 1 and 2), exit 0 each: all 8 PNGs identical byte for byte (`cmp`), and `run.json` identical. Same machine (RTX 5070 Ti) only.

**Not checked:** other GPUs or machines; the L-block back view by pixels (looked at only); a hang's timeout.
