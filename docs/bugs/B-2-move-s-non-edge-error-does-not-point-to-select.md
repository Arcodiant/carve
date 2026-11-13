---
description: >-
  When move is given a pair that is not an edge, the error names the pair but not what to call next; found by T-28's acceptance run
---
# B-2 — move's non-edge error does not point to select

**Status:** In Progress

**Severity:** Major

**Milestone:** — (off-milestone)

## Symptom

`move` with `edges` holding a pair that is not an edge of the mesh fails with `move failed with 1 problem(s); the current mesh is unchanged: edges[3-9]: the 1st listed edge, [3, 9], is not an edge of the mesh`. It says what is wrong but not how to find a real edge. In T-28's acceptance run (rounds 1 and 2, artifacts/m4-acceptance/) the agents understood the error and stopped at it, asking the person which edge was meant, instead of calling `select` for edges.

## Cause

The error text (`MeshEdits` in Carve.Core, via `MoveTool`) states the fault only. The tool descriptions mention `select`, but an agent reading the error does not follow them.

## Fix

Let the error point to `select`, e.g. append "select with kind edges lists the mesh's real edges", and perhaps name the edges at the pair's vertices (for [3, 9], the edges at 3 and at 9). A code change to the error text and its tests (`MoveTests`, `RenderTests` highlight case); not made in T-28, which changes text only.

## Verified by
