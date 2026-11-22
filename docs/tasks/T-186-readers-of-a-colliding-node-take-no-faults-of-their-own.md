---
description: >-
  When an outer node collides with an expanded id, nodes inside the nested graph that read the collided node take no faults of their own, so the graph is refused with the one fault that names the cause
---
# T-186 — Readers of a colliding node take no faults of their own

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

A graph refused for an outer node whose id collides with an expanded one is refused with the fault on that node only,
not with follow-on faults on the nested nodes that read it.

## Scope

- In `GraphValidator.cs` (around lines 142 to 176 at M-28's finish), an input whose source is a colliding outer node is
  kept quiet, as `sourceOfUnknownType` already keeps an unknown type quiet, whatever the colliding node's type.
- A test: a nested `c -> d -> o` beside an outer `g/c` of type `output` gives one fault, on `g/c`, and none on `g/d`.
- Correct the comment at `tests/Patina.Graphs.Tests/NestedGraphTests.cs:209` ("Every fault is the outer node's own"),
  which holds today only because its graph's `c` is read by the output alone.

## Notes

- **Filed (2026-11-22, by the person):** RV-79's Nit, the narrow review confirming T-183. The cascade predates T-183,
  since RV-76's fix. Nothing throws, and the graph is still refused, with 5 faults where 1 would do. The person chose
  to file it and close M-28.
