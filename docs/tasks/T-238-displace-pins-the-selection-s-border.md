---
description: >-
  displace takes pin_border: a vertex also on a face outside the selected faces or group does not move, so the faces around a worn patch stay flat and unmoved
---
# T-238 — displace pins the selection's border

**Status:** Open

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
