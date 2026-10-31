---
description: >-
  The cube scenario renders a cube and an L-block from both views, proves byte-equal frames and runs in verify
---
# T-6 — Cube scenario and verify wiring

**Status:** Open

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
