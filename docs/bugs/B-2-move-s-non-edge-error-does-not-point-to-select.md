---
description: >-
  When move is given a pair that is not an edge, the error names the pair but not what to call next; found by T-28's acceptance run
---
# B-2 — move's non-edge error does not point to select

**Status:** In Progress

**Severity:** Major

**Milestone:** — (off-milestone)

**Changelog:** `move` (and `render`'s highlight) now ends its error for a pair that is not an edge with "select with kind edges lists the real edges", as `loop_cut`'s does.

## Symptom

`move` with `edges` holding a pair that is not an edge of the mesh fails with `move failed with 1 problem(s); the current mesh is unchanged: edges[3-9]: the 1st listed edge, [3, 9], is not an edge of the mesh`. It says what is wrong but not how to find a real edge. In T-28's acceptance run (rounds 1 and 2, artifacts/m4-acceptance/) the agents understood the error and stopped at it, asking the person which edge was meant, instead of calling `select` for edges.

## Cause

`MeshEdits.UnknownElements` in Carve.Core, the one check `move` and `render`'s highlight share for listed elements, wrote the non-edge message as the fault alone (`the 1st listed edge, [3, 9], is not an edge of the mesh`). Pointing to `select` lived only in `MoveTool`'s description, which an agent holding the error does not reread. `loop_cut`, added later (T-66), already ends its own non-edge message with the pointer, so the two disagreed.

## Fix

`UnknownElements` now appends `; select with kind edges lists the real edges` to the non-edge message, the same words `loop_cut` uses, facts only as ruled in RV-20 (2026-11-07). The edges at the pair's vertices are not named: the bug's Fix offered that as a "perhaps", and the pointer alone matches the ruling and `loop_cut`. Every caller of the check, `move` and `render`'s highlight, gets the new text.

## Verified by

- `MoveTests.RefusesAPairThatIsNotAnEdge` now asserts the message ends with the pointer; it failed before the fix (message ended at `is not an edge of the mesh`) and passes after.
- `RenderTests.A_bad_highlight_is_a_tool_error_and_the_mesh_is_untouched` (the `[[0,6]]` case) asserts the full new text through the real `carve`.
- The shed scenario's error path (`ShedScenario.cs`, `[3, 9]` and `is not an edge`) still holds; it is text-only, so no frames changed.
- `cairn bug B-2 verify`.
