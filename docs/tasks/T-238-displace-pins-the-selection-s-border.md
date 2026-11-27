---
description: >-
  displace takes pin_border: a vertex also on a face outside the selected faces or group does not move, so the faces around a worn patch stay flat and unmoved
---
# T-238 — displace pins the selection's border

**Status:** In Progress

**Pri:** P2

**Milestone:** M-41

**Batch:** — (standalone)

**Depends on:** T-237

**Verify:** core-unit, server-unit, scenario-cuts

## Goal

Displacing a face group leaves every face outside it planar and unmoved, and the edge around the worn patch crisp.

## Scope

- `displace` takes `pin_border` (boolean, default false). With it, a vertex that is a corner of any face outside
  the selected `faces` or `group` does not move; every other selected vertex moves as today.
- `pin_border` with `vertices`, or with no selection, is a tool error that changes nothing (there is no border).
- `moved` and `largest` count only the vertices that moved.
- Tool description and schema updated.
- README: `displace`'s entry names `pin_border`.
- Content: the Cuts scenario's tessellate stage (T-237) goes on to displace its group with the border pinned, seen before and after, the faces outside the group held unmoved in the frame.
- Tests (Core and Server): a group on a tessellated slab with `pin_border` leaves every face outside the group
  planar, with the same positions, and the mesh closed; without it, the border moves as today; the error cases.
  Once T-237 has landed, the milestone's `D-DAIS`-shaped fixture, tessellated at `max_edge` 0.75, with `displace` on
  group `wear` and the border pinned.

## Notes

- Raised from the Shoalward terrain project's request (2026-11-27); reserved by the person as part of M-41.
- `falloff` (easing to zero over a distance) was left out: the request wants the crisp edge, which pinning gives.
  Add it if someone asks.
- Works without T-237 (on any selection), but its milestone test needs T-237's tessellate.

### As built

Ran on Sonnet 5.5. Verify unchanged (core-unit, server-unit, scenario-cuts); the coverage check did not need it widened.

- Core: `Displacement.Displace` takes `pinBorder` (last parameter). With `faces`, it drops every corner of a live face outside them from the selection; with `vertices`, or with neither, it is a `pin_border` refusal that changes nothing. `moved` and `largest` come from the vertices left, so they count only those that moved. A selection that is all border is refused as "matches no vertex off the border".
- Server: `pin_border` in `DisplaceTool` (schema, description, `OptionalBool` so a non-boolean is an error); `group` passes its faces as before.
- Tests: Core, the tessellated dais (0.75) with `wear` pinned (every border vertex and every outside face unmoved, closed, and the same displace unpinned moves the border) and the refusals; Server, the dais through the tools (errors leave `get_mesh` byte-equal, pinned moves fewer vertices than unpinned).
- Content: `TessellateStage` goes on to displace group `wear` along z with the border pinned (frame `tessellate-slab-worn`, beside `tessellate-slab-wireframe`). It checks every border vertex unmoved, the faces outside the group keep their loops, `moved.count` equals the interior vertices that changed, and the unmoved faces' probes in the frame. The front's own pixels are not probed (the displaced front is no longer flat). No check was loosened.
- Frame review (a subagent): the rim and outer border are unchanged and straight, interior vertices visibly shifted though subtly (bumps are +-0.06 at an oblique view), no gaps or stray lines. The numeric border check is in the stage.
- README: `displace`'s entry names `pin_border`. CLAUDE.md's Cuts row notes the displace in `TessellateStage.cs`.
- Note: one unrelated flake seen in a first verify, `Patina.Server.Tests ExportToolTests.Other_tools_answer_while_an_export_runs` (timing); it passed on rerun.
