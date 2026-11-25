---
description: >-
  RV-82: the bake's key, validation of mesh parameters, and the graphs suites
---
# T-217 — RV-82: the bake's key, validation of mesh parameters, and the graphs suites

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, graphs-unit, server-unit, patina-unit

## Goal

The bake cache never serves one slot's bytes to another, a mesh node's bad parameter is a fault when the graph is
validated (in carve and in patina) and not a failure at the first bake, and the suites that hold `Carve.Graphs` are
honest about their tier and their inputs.

## Scope

Each finding of RV-82 named here, by its heading there:

- "The cache key leaves out the slot, but the bake's bytes depend on it, so two slots of one graph output share an entry" (the Core and Carve.Graphs)
- "The cache key has no slot, so two slots that name the same graph output share one bake packed for whichever slot baked first" (the server)
- "`graphs-unit` reads the planks sample and its PNGs, but neither is in the suite's inputs" (the Core and Carve.Graphs)
- "`Validate` passes mesh-node parameters that every bake then refuses, so `define_material` and `check` accept a graph that never renders" (the Core and Carve.Graphs)
- "Seven `unit` tests run well over the tier's 1 s, the slowest at 22 s, and the suite exceeds its estimate" (the Core and Carve.Graphs)
- "`Read` hashes the bytes once and `Rgba` reads the files again, so an edit during a bake is cached under the old content's key" (the Core and Carve.Graphs)
- "`Times_each_map_on_the_wrench` asserts nothing and writes into the shared temp folder" (the Core and Carve.Graphs)
- "The files' hash takes each file's absolute path, so the same graph in a moved folder misses the cache" (the Core and Carve.Graphs)
- "The server suites read sample files their `tests.json` inputs leave out" (the server)
- "Suite inputs miss sample files that M-29's new tests read at run time" (the scenarios)
- "Two graphs-unit tests take 21 s and 12.7 s in the unit tier, one of them "not a check"" (the scenarios)

- **The key:** what makes two bakes' bytes differ is in the key: the slot's packing above all (the two reviewers'
  probes are the tests), and the `uvs` look's `:image` suffix is folded into the same rule so there is one. A cache
  hit after `undo`, and two materials that name the same graph, output, size and packing sharing one bake, still hold.
- **Mesh parameters:** `width` 0, `reach` 0 and a `mesh_mask` naming both or neither of `group` and `material` are
  faults naming the node and the parameter wherever a graph is validated: `define_material` and `check` in carve
  (T-166's Scope gives `width: 0` as its example), and `define_graph`, `edit_graph`, `open` and `save` in patina, so
  patina no longer says "it evaluates" of such a graph. Do it where both servers' validation reaches it, keeping
  `Patina.Graphs` engine-free and node types registered, never switched on. The messages patina shows when an
  evaluation still throws are T-219's, which waits on this member.
- **Tiers:** a test that takes more than about 1 s is not `unit`. Move the slow ones to a `regression` class and
  suite of their own (`graphs-regression`, in `tests.json`, named in this file's `**Verify:**`), keeping what each
  asserts and the sizes its spec names (T-161's 1024 equality test stays at 1024 and 32 rays); a test that asserts
  nothing is deleted or made to assert. No assertion is weakened to make a test faster.
- **Inputs:** every sample file a suite's tests read at run time, and every linked file it compiles, is among that
  suite's inputs in `tests.json`; say how you found them (a search of the test sources for sample paths, and each
  project's linked items).
- **Owns:** `src/Carve.Graphs`, `src/Patina.Graphs` (validation only), the key's use in `src/Carve.Server/GraphBakes.cs`
  (T-218 works in the rest of that file), `tests/Carve.Graphs.Tests`, `tests/Patina.Graphs.Tests`, `tests.json`.

## Notes

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.
