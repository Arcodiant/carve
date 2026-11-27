---
description: >-
  displace and move report moved as {count, first} at summary, the first 10 ids, and the whole list only at full
---
# T-239 — moved is short at summary verbosity

**Status:** In Progress

**Pri:** P2

**Milestone:** M-41

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit

## Goal

A `displace` or `move` of thousands of vertices answers with a short result at the default `summary` verbosity.

## Scope

- At `summary`, `displace` ([DisplaceTool.cs](../../src/Carve.Server/DisplaceTool.cs)) and `move`
  ([MoveTool.cs](../../src/Carve.Server/MoveTool.cs)) report `moved` as `{count, first}`: the number of vertices
  moved and the first 10 ids, ascending. At `full`, `moved` is the whole list as today. `silent` is unchanged.
- Tool descriptions, the `verbosity` schema text if it needs a word, and any doc or sample that reads `moved` as an
  array at `summary`.
- Tests: a large move at `summary` and at `full`, and one of fewer than 10 ids.
- README: wherever it describes `moved` or the `verbosity` levels; or the task's As built says nothing needed to.

## Notes

- Raised from the Shoalward terrain project's request (2026-11-27): 1,120 moved ids listed at `summary`.
- The shape changes at `summary`, which breaks a script reading `moved` as an array. Proposed in discussion with
  that named; the person reserved the milestone without choosing otherwise. The alternative was the array cut short
  plus a separate `moved_count`.
- The requested `select` query that names every moved vertex is left out: `select` cannot express an arbitrary set
  of vertices, so it would only repeat the selection given.
- `created`/`deleted` on other edits are left as they are until one is found to be large; T-237's tessellate
  reports `created` in `subdivide`'s `{first, count}` form.
