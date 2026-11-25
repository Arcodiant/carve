---
description: >-
  RV-82: the bake's key, validation of mesh parameters, and the graphs suites
---
# T-217 — RV-82: the bake's key, validation of mesh parameters, and the graphs suites

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, graphs-unit, graphs-regression, server-unit, patina-unit

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

### Hand-off

Written at the 60-call checkpoint by an agent on Claude Opus 5.5 (`claude-opus-5-5`). Commit `fb827d1`.

**Done (committed, `graphs-unit` and `server-unit` pass on it; `verify` not yet run, `core-unit` and `patina-unit` not run):**
- The key: `GraphSlotBake.Key(files, output, size, slot, model, mesh)` writes `metallic_roughness_texture` for that slot
  and `base_colour_texture` for every other (each other slot packs an output as base colour does, or refuses it, by
  `PackingOf`). `GraphBakes.Resolve` passes the field; `Image` (the `uvs` look) passes `BaseColour` and its `:image`
  suffix is gone. Tests: `GraphSlotBakeTests.The_key_tells_apart_slots_that_pack_one_output_differently_and_only_those`,
  `GraphBakesTests.Two_slots_that_pack_one_output_differently_bake_apart_and_alike_share` (copies
  `samples/graphs/planks.texgraph`, so server-unit needs `samples/graphs/**` in its inputs).
- Read then re-read: `GraphSlotBake.Unchanged(files)` re-reads and compares the hash; both `Put`s in `GraphBakes.cs`
  run only when it holds. Tests: `GraphSlotBakeTests.Unchanged_holds_until_a_file_is_edited`,
  `GraphBakesTests.A_file_edited_during_its_bake_is_not_cached_under_the_bytes_read_before` (the injected clock edits
  `inner.texgraph` as the base colour bake starts; restored, the next resolve must bake again).
- Absolute paths: `Read` hashes each file's path relative to the top file's folder.
  Test: `GraphSlotBakeTests.The_files_hash_follows_the_nesting_not_the_folder`.
- Mesh parameters (for T-219): a new virtual `NodeType.CheckParameters(NodeParameters)` in `src/Patina.Graphs/Nodes/NodeType.cs`,
  asked by `GraphValidator.Analyse` (`src/Patina.Graphs/GraphValidator.cs`, after a node's parameters resolve, only when
  none of them had a range fault). `MeshCurvatureNode`, `MeshOcclusionNode` and `MeshMaskNode` override it. The faults are
  ordinary `GraphFault`s, so `TexGraphFile.Read`'s faults, `GraphEvaluator`'s `GraphFaultException`, and so
  `GraphSlotBake.Validate`, `define_material`, `check`, and patina's `define_graph`/`edit_graph`/`open`/`save` all carry
  them. Messages: `node 'w' parameter 'width': is 0, but must be above 0 metres`, `node 'w' parameter 'reach': is 0, but
  must be above 0 metres`, `node 'k': give exactly one of params 'group' and 'material'` (`, not both` when both). The
  evaluation-time `MeshParameterException` is kept (same wording after its old `nodes['id']` prefix) but no evaluator
  reaches it now. Tests: `MeshNodesTests.Parameters_no_range_can_refuse_are_graph_faults_with_or_without_a_source`
  (replaces `Parameters_the_file_cannot_refuse_are_refused_with_or_without_a_source`),
  `GraphSlotBakeTests.Validate_refuses_a_mesh_parameter_every_bake_would`.

**Not checked yet:** that each new test fails without its fix (argued from the code, not run against the old code);
no `Patina.Graphs.Tests` test of `CheckParameters` with a non-mesh node; patina server tests (T-219's) may pin the old
"it evaluates" answer and fail now.

**Left:**
- Tiers: move `Each_node_equals_its_bake_at_every_tile_size_and_a_zero_cap` (at 1024; 256 is 1.6 s too), the occlusion
  crevice test, raised hoops, plain barrel occlusion (`MeshNodesTests.cs`) and the broken-copy "curvature read as
  occlusion" (`BrokenCopiesTests.cs:55`) to a `Tier` `regression` class; add `graphs-regression` to `tests.json` and to
  this file's `**Verify:**`; one line in `CLAUDE.md`'s Layout. Delete `Times_each_map_on_the_wrench` (asserts nothing).
- Inputs in `tests.json`: `graphs-unit` + `samples/graphs/**`; `server-unit` + `samples/*.carve`, `samples/graphs/**`,
  `tests/Carve.CharacterLoad.Tests/RunContent.cs`; `server-regression` + `samples/graphs/**`. Then search the test sources
  for `RepoRoot`/`samples` and each project's linked `Compile`/`Content` items for anything else.
- `### As built`, the eleven `**Outcome:**` lines in RV-82, `verify`, `cairn lint`, `finish`.

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.
