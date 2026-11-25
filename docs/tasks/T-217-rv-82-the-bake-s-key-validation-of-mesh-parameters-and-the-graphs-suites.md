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

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.

### As built

Two agents, both on Claude Opus 5.5 (`claude-opus-5-5`): the first made the code fixes (`fb827d1`) and handed off at its
checkpoint; the second moved the slow tests, fixed the inputs, added the slot-table and `CheckParameters` tests, ran each
new test against its fix reverted, and finished (`9a98e89`, `88cb9b4`).

**Findings, what was done, the test that fails without it:**
- *The cache key leaves out the slot* (Core) and *The cache key has no slot* (server): `GraphSlotBake.Key(files, output,
  size, slot, model, mesh)` writes `metallic_roughness_texture` for that slot and `base_colour_texture` for every other;
  `GraphBakes.Resolve` passes the slot's field, and the `uvs` look passes `BaseColour` (its `:image` suffix is gone, one
  rule). `fb827d1`. Tests: `GraphSlotBakeTests.The_key_tells_apart_slots_that_pack_one_output_differently_and_only_those`,
  `GraphBakesTests.Two_slots_that_pack_one_output_differently_bake_apart_and_alike_share`, and (`9a98e89`) the claim
  "every other slot packs as base colour does or refuses" proved over the whole slot table:
  `GraphSlotBakeTests.A_slot_keyed_as_base_colour_packs_every_output_as_base_colour_does_or_refuses_it` (every
  texture-slot constant of `GraphSlotBake`, found by reflection so a slot added later is in it, against every output of
  the planks, grey and colour: keyed apart, or a refusal, or base colour's bytes). A cache hit after `undo` and two
  materials sharing one bake: the existing `GraphBakesTests` still pass.
- *`Read` hashes the bytes once and `Rgba` reads the files again:* `GraphSlotBake.Unchanged(files)` re-reads and compares
  the hash; both `Put`s in `GraphBakes.cs` run only when it holds. `fb827d1`. Test:
  `GraphBakesTests.A_file_edited_during_its_bake_is_not_cached_under_the_bytes_read_before`;
  `GraphSlotBakeTests.Unchanged_holds_until_a_file_is_edited` tests the new function itself (it cannot run on the old code).
- *The files' hash takes each file's absolute path:* each file's path is hashed relative to the top file's folder.
  `fb827d1`. Test: `GraphSlotBakeTests.The_files_hash_follows_the_nesting_not_the_folder`.
- *`Validate` passes mesh-node parameters that every bake then refuses:* see "Mesh parameters" below. `fb827d1`. Tests:
  `MeshNodesTests.Parameters_no_range_can_refuse_are_graph_faults_with_or_without_a_source` (four rows),
  `GraphSlotBakeTests.Validate_refuses_a_mesh_parameter_every_bake_would`, and (`9a98e89`) `CheckParametersTests` in
  `tests/Patina.Graphs.Tests` (a test type `pair` through `GraphValidator.Validate` alone: a parameter's fault, a
  whole-node fault, no fault, and not asked while a parameter is out of its range).
- *Seven `unit` tests run well over the tier's 1 s* and *Two graphs-unit tests take 21 s and 12.7 s*: moved, unchanged
  in what they assert and at their sizes (T-161's equality test at 256 and 1024, 32 rays), to `regression` classes:
  `MeshNodesRegressionTests` (each node equal to its bake, M-24's curvature and occlusion numbers, the raised hoops, the
  plain barrel, two sources' equal bytes) and `BrokenCopiesRegressionTests` (the "curvature read as occlusion" row, which
  bakes the wrench's occlusion). After the first move a timed run still had the curvature test at 1.57 s and two sources
  at 1.32 s, so they moved too. New suite `graphs-regression` in `tests.json` (the same inputs as `graphs-unit`, est 60 s;
  it ran in 28 s to 39 s), named in this file's `**Verify:**` and in `CLAUDE.md`'s Layout row for
  `tests/Carve.Graphs.Tests`. `9a98e89`, `88cb9b4`. **`graphs-unit` now:** 36 tests, 1 s by `dotnet test` (2.6 s summed
  per test) in a scratch copy, the slowest `BrokenCopiesTests.All_open_catches_curvature_read_as_occlusion_on_the_plain_barrel`
  at 0.51 s; in `verify`, beside the other suites on one machine, 24 s with the host's start-up. Its estimate went from
  40 to 10.
- *`Times_each_map_on_the_wrench` asserts nothing:* deleted (`9a98e89`). T-161's and T-163's recorded map times came from
  it; it is not kept as a script.
- *`graphs-unit` reads the planks sample and its PNGs*, *The server suites read sample files their inputs leave out* and
  *Suite inputs miss sample files*: inputs added in `tests.json` (`9a98e89`). How they were found: a script (in `%TEMP%`,
  not committed) follows each suite's `dotnet-closure` projects and their `ProjectReference`s, checks every linked
  `Compile`/`Content`/`None` item outside each project, and every `"samples"` path in the test sources of the test and
  scenario projects, against the suite's inputs. It added: to `graphs-unit` and `graphs-regression` `samples/graphs/**`;
  to `server-unit` `samples/*.carve` (in place of its four), `samples/graphs/**`, `samples/textures/*.png` (loaded by
  `barrel-textured.carve`, which the every-sample tests open) and `tests/Carve.CharacterLoad.Tests/RunContent.cs`; to
  `server-regression` the same three sample globs (in place of its three samples) and six linked files it compiled but did
  not list (`ColourRule.cs`, `ShedGeometry.cs`, `CutsGeometry.cs`, `RoverSpec.cs`, `RoverSpec.Model.cs`,
  `PartTreeSample.cs`); to `patina-unit` `samples/textures/*.png` (`CheckSamplesTests`). It now reports nothing for these
  suites. What it still prints is false: core-unit's and patina's interpolated message strings and `samples/<name>`
  dictionary keys, and the Weathered project's semicolon-joined `Content` list (the reviewer found those suites complete).

**Shown to fail without the fix** (a scratch clone under `%TEMP%`, each hunk reverted alone, then restored): the key with
no slot written failed both key tests in `Carve.Graphs.Tests` and `GraphBakesTests.Two_slots_...`; the guarded `Put` made
plain failed `GraphBakesTests.A_file_edited_during_its_bake_...`; the path hashed absolute failed
`The_files_hash_follows_the_nesting_not_the_folder`; `GraphValidator` not asking `CheckParameters` failed
`Validate_refuses_...`, all four `Parameters_no_range_...` rows and two of the four `CheckParametersTests` (the other two
pass either way by design).

**Mesh parameters (for T-219):** a virtual `NodeType.CheckParameters(NodeParameters)` (`src/Patina.Graphs/Nodes/NodeType.cs`)
is asked by `GraphValidator.Analyse` (`src/Patina.Graphs/GraphValidator.cs`) once a node's parameters resolve, and only
when none had a range fault; it returns (parameter or null, message), and the validator prefixes `node '<id>' parameter
'<p>': ` or `node '<id>': `. `MeshCurvatureNode`, `MeshOcclusionNode` and `MeshMaskNode` override it, so their faults are
ordinary `GraphFault`s wherever a graph is read or validated: `TexGraphFile.Read`, `GraphEvaluator`'s
`GraphFaultException`, `GraphSlotBake.Validate`, carve's `define_material` and `check`, patina's `define_graph`,
`edit_graph`, `open` and `save`. The faults say: `node 'w' parameter 'width': is 0, but must be above 0 metres`; `node
'w' parameter 'reach': is 0, but must be above 0 metres`; `node 'k': give exactly one of params 'group' and 'material'`
(with `, not both` when both). The evaluation-time `MeshParameterException` stays, with the same wording, but no
evaluation reaches it now. Patina's server tests pass on this (`patina-unit` in `verify`): none pinned the old "it
evaluates" answer.

**Not checked:** `server-regression` (it needs the GPU; its inputs changed only by additions, and `GraphBakes.cs` only in
the key's use and the guarded `Put`s); the `uvs` look's guarded `Put` by a test of its own (that hunk was not reverted
alone); `patina-unit`'s estimate (121 s in `verify` against 15; not this member's tests).

**README.md:** nothing needed changing here (README's suite list is T-220's finding); `CLAUDE.md` changed only in the one
Layout row, for `graphs-regression`.
