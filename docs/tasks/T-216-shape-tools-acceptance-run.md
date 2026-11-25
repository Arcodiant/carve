---
description: >-
  Fresh agents build a wheel and a barrel from primitives with carve's descriptions alone, and recover from a planted unpaired vertex
---
# T-216 — Shape tools acceptance run

**Status:** Open

**Pri:** P2

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** T-215

**Verify:** core-unit, server-unit

## Goal

A fresh agent with only carve's tools and their descriptions builds a wheel and a barrel from a brief using
`primitive`, `rotate`, `scale`, `mirror` and `symmetric`, and recovers from a planted fault. This finds the description
gaps before the tools ship.

## Scope

- **Two rounds**, each a fresh `claude -p` with these run conditions:
  - `--tools ""`
  - `--allowedTools "mcp__carve__*"`
  - `--mcp-config` beside `--strict-mcp-config`
  - `--model claude-opus-5-5` pinned
  - an absolute evidence folder outside the worktree's `artifacts/`
  The prompt asks for a final `get_mesh` (with B-10's `include` where the mesh is large) and `save`.
- **The brief:**
  - A wheel of radius 0.35 and width 0.12 on the X axis, 16 sides, with a hub boss on each face that is a mirror of
    the other.
  - A barrel of radius 0.36 and height 1, 16 sides, bulging to 1.17 at its middle and symmetric top to bottom.
  - Work its numbers against T-215's samples before the run, so nothing in the brief clashes. M-21 lost a round to a
    clash.
- **The planted fault:** the round opens `planted/barrel-start.carve`, a 4-band cylinder with one ring-3 vertex 0.004 m
  off its mirror place. The brief asks for the symmetric bulge. The first symmetric `scale` is refused, naming the
  unpaired vertex.
  - **Recovery** means the agent puts that vertex back (by `move`, or by `primitive` again) and the symmetric scale
    then succeeds. Say this up front in the checks.
  - Name the planted file in the brief; do not describe the fault.
- **Checks by numbers**, in a check script or test reading the round folder:
  - counts and `closed`;
  - bounds within 1e-4 of the brief;
  - the wheel symmetric in x and the barrel in y, every vertex paired within 1e-5;
  - `check` silent;
  - the refusal present in the log, and the recovery after it.
- **Report:** every refused call, every description gap the agents met, and the fixes. Description fixes land in this
  task, each worked example run as written, inside the 3,686 budget. A product fault found by a round goes back to the
  person as a bug, not a fix here.

## Notes

- Expect the first round to find product problems, not only wording ones (M-2, M-28).
- What counts as a pass, and the margins, are the person's. Bring measured figures with a recommendation.
- Not design-heavy. It is long-running and context-heavy (two real runs), so give it its own agent per round.
