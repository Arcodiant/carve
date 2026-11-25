---
description: >-
  RV-82: patina's findings
---
# T-219 — RV-82: patina's findings

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-217

**Verify:** patina-unit, scenario-patina, server-unit

## Goal

patina tells an agent the truth about a mesh-reading graph: one that nests a mesh-reading graph carries the
"mesh inputs neutral" note everywhere and is not called broken by `check`, an evaluation that fails says why, and
the checks of the note and of the worked examples can fail.

## Scope

Each finding of RV-82 named here, by its heading there:

- "A mesh node with a bad parameter is reported as evaluating, then swatch, export and check fail with no message" (patina)
- "A graph that nests a mesh-reading graph gets no note in any answer, and check calls its mask broken" (patina)
- "The scenario's break cannot fail on its own: it negates a looser form of the check on the same two numbers" (patina)
- "The worked-example test only validates the examples, so it cannot catch what the changed `mesh_curvature` caption claims" (patina)
- "swatch's mesh note is in no unit test, and the sphere, tiled and sheet captions' note in no test at all" (patina)
- "`CheckSamplesTests` skips by file name, and its stand-in covers only the two names it lists" (patina)
- "As built leaves open what patina's output gained; Carve.Core is new to it" (patina)

- **Waits on T-217,** which makes a mesh node's bad parameter a validation fault in both servers. This member
  makes sure patina's tools then report that fault as any other, and that a mesh-node exception that still reaches
  `swatch`, `export` or `check` at evaluation is answered with its own message naming the node, never the SDK's
  generic error; the first finding's Outcome is this member's, naming T-217's commit beside its own.
- **The note looks into nested files:** `get_graph`, `open`, `export`, `check`, `swatch` and `save` say "mesh inputs
  neutral" for a graph whose nested `graph` files hold mesh nodes, naming the node as expanded (`<node>/<id>`), and
  `check`'s `one-value` advice is not given for an output that is flat only because its mesh inputs are neutral
  (say what it reports instead, and hold it with a test). The retrospective's "never verified" line is corrected by
  the orchestrator from this member's `### As built`.
- **Checks that can fail:** the scenario's break proves the title-note check by a prediction the check can fail on
  its own; the worked examples of the four mesh nodes are evaluated, not only validated, and the `mesh_curvature`
  caption's two claims are held (the mask is 0 on the neutral stand-in; on a band of real curvature the mask comes
  and goes along the edge); `swatch`'s note is held in a unit test for every view's text.
- **Owns:** `src/Patina.Server`, `tests/Patina.Server.Tests`, `scenarios/Carve.Scenario.Patina`, T-162's `### As built`.

## Notes

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.

### Hand-off

Agent one (claude-opus-5-5) stopped at the 60-call checkpoint. Code and tests committed in one commit; `cairn test
run patina-unit` passes (505 + all of Patina.Server.Tests). `verify` not yet run (server-unit, scenario-patina left).

**Done:**
- *Bad parameter (Major):* T-217's faults hold in define_graph, open, save (refused) and edit_graph (refused, "would
  leave 1 new fault"): `MeshGraphTruthTests.A_bad_mesh_parameter_is_a_fault_in_define_edit_open_and_save` (four
  probe rows) and `Edit_graph_answers_a_width_of_0_as_a_fault`, which passed before any change (T-217's fix). The path
  that still reached evaluation: an exposed parameter's override inside its range (0) driving `mesh_curvature.width`;
  export, swatch and check answered "An error occurred invoking '...'" (seen failing). Fixed in
  `ParameterOverrides.TryApply`: the overridden graph is validated, refused as "<tool>: with parameters w = 0 the graph
  has 1 fault: - node 'm' parameter 'width': ...". Test: `A_parameter_value_a_mesh_node_refuses_is_answered_naming_the_node`.
  No other evaluation-time throw found (unknown group/material is accepted by the stand-in, as the reviewer says).
- *Nested note (Major), the note half:* `ToolSupport.MeshNote` recurses over `GraphValidator.Uses` (reads each
  expanded use's file, prefixes `<node>/`); `save` now carries the note too. Test:
  `A_graph_nesting_a_mesh_graph_says_so_in_every_answer` (get_graph, save, open, export, check's notes, and
  `h/g/edges` two levels down). Not yet seen failing without the fix (the first "before" run failed for another
  reason, the graph's version); to do: disable the recursion once and see it fail, or say so.
- *swatch note:* the harness takes `blankEngine: true` (a ready `EngineStatus` by reflection on internal `SetReady`,
  marked `shortcut:`; a loop answers each `SwatchRequest` with blank frames and keeps the sheet's labels).
  `Every_swatch_view_says_the_mesh_inputs_are_neutral` holds flat, tiled, sphere, cube and sheet captions and the
  sheet title label.
- *Worked examples:* `NodeToolTests.Every_types_worked_examples_run_as_written` now exports each evaluating example
  (size 32). `MeshGraphTruthTests.The_mesh_curvature_example_is_the_base_colour_on_the_stand_in_and_wears_in_runs_along_a_real_edge`
  holds both caption claims (a test `IMeshMaps` band).
- *CheckSamplesTests Nit:* both sides key on "a node type `NodeRegistry.BuiltIn` does not know";
  `MeshNodeToolTests.MeshSamples` is MemberData, with `The_mesh_samples_are_the_two_weathered_graphs`.
- README's weathered paragraph names save and the nested case.

**Left:**
- **Open question for the orchestrator (not decided):** check's `one-value` "clipped or flat; widen its levels" on an
  output flat only because its mesh inputs are neutral. Any change applies equally to the committed weathered graphs'
  `wear`/`grime` (direct mesh nodes, flat for the same reason), so per the brief it is a change to an existing check's
  target. Option A: withhold `one-value` for such an output (check then reports `ok: true`, no problems, for
  weathered-iron and weathered-staves, the note in `notes`; T-162's `Check_reports_the_one_empty_mask_and_nothing_else`
  is on `CheckRules` and would still hold, but README line "check reports one-value on those two outputs" and T-162's
  record change). Option B: keep `one-value` but replace its Fix text for such an output with the reason ("flat
  because its mesh inputs are neutral stand-ins; the mesh gives the real maps in carve"); same problem list, new
  message. As committed, nested graphs now get the note and the same `one-value` as the direct ones.
- The scenario's break (MeshNodesStage), T-162's `### As built` (Carve.Core.dll is new to patina's output), the
  Outcome lines, `### As built`, frames compared by hash against `origin/milestone/M-29`, verify, lint, finish.
- Not touched: scenarios/, RV-82.
