---
description: >-
  edit_graph's set_params and set_node take list parameters (Floats, Colours) as define_graph does, so gradient_map's stops can be edited in place
---
# T-171 — edit_graph takes list parameters

**Status:** In Progress

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

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`), in one agent.

- `ParameterOverrides.Read` (`src/Patina.Server/ParameterOverrides.cs`) now reads a value as the file does: a number, a string, or an array of numbers or of strings (an empty array is numbers). A mixed array, or a non-finite number, is refused; `ParameterOverrides.WhyNot` gives the words after the name (`: an array is all numbers or all strings`, as the file's load error, or ` is true or false; a value is a number, a string, or an array of numbers or of strings`). `edit_graph`'s `Params` (`set_params`, `set_node`) now goes through it, as do `set_parameters`, `set_parameter`'s `value` and the `parameters` argument of swatch, export and check (they accept lists too; the one message changed from "an array of numbers" to "an array of numbers or of strings").
- The faults for a bad list are the validator's, as `define_graph` reports them, so the two cannot drift: for example `node 'tint' parameter 'positions' is [0, 0.5, 2], but must be a list of 2 to 8 numbers [a, b, ...], each from 0 to 1`, `node 'tint' input 'in': parameters 'positions' and 'colours' must be the same length, but positions has 3 and colours has 2`, `... must run from low to high ...`. The wrong kind (numbers for colours) is a fault on the parameter; a mixed array is refused at the operation (`operation 1 (set_params): parameter 'colours': an array is all numbers or all strings`). A list is given whole and replaces the list, so a change of count needs both lists in one operation.
- `EditGraphTool.WorkedList` / `WorkedListResult`: the T-143 session 4 case, a gradient map's middle stop moved and lightened in place, run as written by a test; the description gains the sentence and the example.
- Tests: `tests/Patina.Server.Tests/EditGraphListParameterTests.cs` (same graph byte for byte as `define_graph`; `set_node` with lists; each fault: wrong kind, out of range, count mismatch, out of order, bad colour, mixed array; nothing changes on refusal). `GraphToolTests`' one expected message changed with the wording above.
- Not checked: no real-agent run of session 4 again. Written outside the worktree: nothing but a scratch file in the Temp folder.
