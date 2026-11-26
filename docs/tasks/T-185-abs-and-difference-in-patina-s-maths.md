---
description: >-
  A way to take an absolute value or a difference in a texture graph, for thin lines such as veins and cracks, which today take two levels nodes multiplied
---
# T-185 — abs and difference in patina's maths

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A thin line (a marble vein, a crack in wood) is one node away from a noise: the distance of its value from a level. Today that takes two `levels` nodes multiplied together.

## Scope

- Decide the shape with the person before building: a `difference` mode on `blend` (the absolute difference of `a` and `b`), an `abs` about a level on `levels`, or a small maths node.
- Tests worked by hand, tiling and size-invariance as the other nodes have, and a sample graph that uses it (veined marble).

## Notes

- Raised by the stone-room project's feature request (2026-11-22, item 9).
- As of M-28's branch on 2026-11-22, `blend` has `mix`, `multiply`, `add`, `subtract`, `min` and `max`, and no node takes an absolute value. M-28's `distance` (T-138) measures from a mask's edge, which is a different thing.
- The same item's other three asks are covered: `tiles` has `stagger` and a `random` output that is one value for a tile across the stagger and the wrap (T-136, tested in `PatternNodesTests`), `distance` is T-138, and a curvature input is T-161 in M-29.
- **Open rules: stop and ask.** The node's shape is the person's.
