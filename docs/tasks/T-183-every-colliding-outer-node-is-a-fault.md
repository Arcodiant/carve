---
description: >-
  Any outer node whose id collides with an id a nested graph's expansion makes is a fault on that node, whatever its type, and validation never throws on it
---
# T-183 — Every colliding outer node is a fault

**Status:** Done

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

### As built

One agent, Claude Opus 5.5 (`claude-opus-5-5`; Sonnet was under its weekly limit).

- `GraphValidator.Kinds` reads a source's kind only when the source is a resolved node (`a.Nodes.TryGetValue`), as `Cycles` and the walk in `Kinds` already did. A use output resolving to an outer node that is in `nodes` but not in `a.Nodes` (an unknown type, faulted and skipped) is an input of unknown kind, so the reading node is kept out of the order and nothing throws. The other lookups the collision reaches were read and hold: `Cycles` and `Kinds`' walk are guarded, `Analyse`'s source resolution uses `nodes.TryGetValue` and marks an unknown-type target's input at fault, `Expand` reads only the inner analysis's own nodes, and the evaluator is never reached (the id fault refuses evaluation). RV-77's early return for an outer `graph` node is kept.
- `NestedGraphTests.An_outer_node_of_any_type_with_an_expanded_id_is_faulted_not_thrown`: RV-78's probe shape through `ReadFile`, with an outer `g/c` of every built-in type, `graph` and `nosuch`. Each is faulted on `g/c` for its id, every fault is on `g/c` (an outer `output` also takes `o`'s role, said on `o`), and none throws. Without the fix it threw `KeyNotFoundException` for `nosuch`; every built-in type already passed.
- RV-78's probe `p3.fsx`, copied to `%TEMP%\t183-probe` against this build's `Patina.Graphs.dll`: `unknown` loads with 2 faults (the id fault and `unknown type 'nosuch'`); the `colour` and `graphgraph` controls give the single id fault, as before.
- RV-78's first finding: `fixed`, with a `**Fix:**` line.
- README: nothing needed to change; no tool, argument, node or output a user or agent reaches changed, only a throw became the fault the README already describes.
