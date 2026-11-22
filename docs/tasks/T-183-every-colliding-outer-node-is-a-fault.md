---
description: >-
  Any outer node whose id collides with an id a nested graph's expansion makes is a fault on that node, whatever its type, and validation never throws on it
---
# T-183 — Every colliding outer node is a fault

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

Validation faults every outer node whose id collides with an expanded id, of any type, known or not, and never throws,
as the person ruled.

## Scope

- Guard the lookup in `GraphValidator.Kinds` (RV-78: `GraphValidator.cs:555`, `a.Nodes[from.Node]`) and any other
  lookup the collision reaches, so an outer `"g/c": {"type": "nosuch"}` beside a `graph` node `g` is a fault, not a
  `KeyNotFoundException`.
- A test of the collision with an outer node of every built-in type, of the `graph` type, and of an unknown type, each
  faulted and none throwing, read through `ReadFile` as the reviewer's probe was. Show it failing without the fix.
- Fill RV-78's first finding's `**Outcome:**` line with exactly `fixed`, the detail on a `**Fix:**` line under it.

## Notes

- **Decided (2026-11-22, by the person):** RV-78 found that the colliding-node fix (RV-76, then RV-77) still throws for an
  outer node of an unknown type. The person chose to fix it in M-28, with a reviewer checking only this fix and running
  the probe, rather than a full round 4.
- The reviewer's probe source and its log `p3b.log` are in the temp folder's `m28-review3` folder
  (`%TEMP%` is `C:/Users/andre/AppData/Local/Temp`).
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
