---
description: >-
  edit_graph's set_params and set_node take list parameters (Floats, Colours) as define_graph does, so gradient_map's stops can be edited in place
---
# T-171 — edit_graph takes list parameters

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-143

**Verify:** patina-unit

## Goal

An agent can change a list parameter, such as `gradient_map`'s `colours` or `positions`, with `edit_graph`, as it can
with `define_graph`, so fixing one stop does not mean redefining the whole graph.

## Scope

- `edit_graph`'s `set_params` and `set_node` accept `Floats` and `Colours` parameters in the same JSON shape
  `define_graph` reads, through the same parsing (T-134's list parameters), so the two cannot drift apart.
- The faults are the same as `define_graph`'s for a bad list (wrong kind, out of range, a count mismatch, out of order),
  and name the node and the parameter.
- **Tests:** a list set through `edit_graph` gives the same graph, written byte for byte, as the same graph through
  `define_graph`; each fault; and the T-143 session 4 case (one stop of a gradient map changed in place).
- `edit_graph`'s description gains a worked example that changes a gradient map's stops, run as written by a test.

## Notes

- **Decided (2026-11-20, by the person):** fixed in M-28 rather than filed. Found by T-143's session 4, which worked
  round it by redefining the whole graph.
- Runs beside T-170, which owns `DefineGraphTool.cs` and `NodeCatalogue`. This task owns `EditGraphTool.cs`. If the
  parsing to share lives in a file T-170 also edits, keep both sides on a rebase.
- Sonnet: concrete.
