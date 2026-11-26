---
description: >-
  RV-82: patina's findings
---
# T-219 — RV-82: patina's findings

**Status:** Done

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
- **The person's ruling (2026-11-25):** `check` keeps `one-value` for an output that reads a mesh node, with the true
  reason in its text in place of "clipped or flat; widen its levels"; the problem list and `ok` are unchanged.
  Withholding `one-value` was declined.

### As built

Three agents, all on claude-opus-5-5. Commits: `2e502508` (agent one: the nested note, overrides validated, swatch's
views, the examples evaluated), `1f4ebd5c` (agent two: check's neutral reason, the scenario's break, T-162's record),
`08a82f54` (agent two: check's description within its length test); agent three ran the suites, compared the frames
and wrote this. RV-82's seven Outcome lines are `fixed`.

- **A bad mesh parameter (Major).** T-217's validation faults (merged as `091a4d57`, "Merge task/T-217") already held in
  patina's `define_graph`, `open`, `save` and `edit_graph`:
  `MeshGraphTruthTests.A_bad_mesh_parameter_is_a_fault_in_define_edit_open_and_save` (four probe rows) and
  `Edit_graph_answers_a_width_of_0_as_a_fault` passed before any change here. The one path that still reached
  evaluation was an exposed parameter's override, inside its declared range (0), driving `mesh_curvature.width`:
  `export`, `swatch` and `check` answered "An error occurred invoking '...'". `ParameterOverrides.TryApply` now validates
  the overridden graph and refuses it as "<tool>: with parameters w = 0 the graph has 1 fault: - node 'm' parameter
  'width': ..." (`2e502508`). Test: `A_parameter_value_a_mesh_node_refuses_is_answered_naming_the_node`, seen failing
  with the SDK's generic error before the fix. No other evaluation-time throw was found.
- **The nested graph (Major).** `ToolSupport.MeshNote` recurses over `GraphValidator.Uses`, naming nested nodes as
  expanded (`h/g/edges`); `save` carries the note too (`2e502508`). Test: `A_graph_nesting_a_mesh_graph_says_so_in_every_answer`
  (get_graph, save, open, export, check, two levels down), seen failing (Assert.Contains on get_graph's note) in a
  scratch copy with the recursion disabled. *What `check` says now* (`1f4ebd5c`): for an output whose reach includes a
  mesh node, `one-value` stays in the problem list and `ok` is unchanged, but its message is the measured part followed
  by `CheckTool.NeutralFix`: "Fix: none here: it reads a mesh input, which in patina is a neutral stand-in, so it is flat
  for that reason; carve gives the real maps when a material names the graph, and its check there judges them." The text
  is chosen in `CheckTool.Run`/`Report` (patina's tool, which knows the mesh), from a set of outputs found by the new
  read-only `GraphEvaluator.TypesReaching(output)`; `OneValueRule` and `CheckRules` are untouched, so carve's check is
  unchanged. Tests: `MeshGraphTruthTests.Check_gives_one_value_the_neutral_reason_only_for_an_output_that_reads_a_mesh_node`
  (a mesh output and a flat fbm/levels output in one graph get the true and the old text), the nested test (the one
  problem and `NeutralFix`), and `MeshNodeToolTests.Check_tells_the_empty_mask_it_is_flat_for_the_neutral_inputs` (both
  weathered samples); all three seen failing in a scratch copy with the substitution disabled. T-162's
  `Check_reports_the_one_empty_mask_and_nothing_else` holds unchanged. check's description gained "one reading a mesh
  is told why" (an earlier wording failed `Every_description_is_under_the_limit` at 3,748 of 3,686 characters; the
  committed one passes `patina-unit`).
- **The scenario's break.** `MeshNodesStage.CheckTitleNote` is now the one title-note check (the sheet's title runs at
  least 80 px past the same graph's with a gradient in place of the mesh node), and the break runs that same check on
  the plain sheet's title against itself (`1f4ebd5c`). In the verified run (J-20261126-001448-df13e3, `08a82f54`) it
  failed as predicted: "the sheet's title runs to x 258, only 0 px past the title with no note (258)", and the stage's
  `checks.Check(probe.Failures.Count > 0, ...)` passed. What it does not prove: the check could still pass with the note
  missing if the mesh graph's title grew by 80 px for another reason, or if the plain title were measured short (glyphs
  too dim for `SwatchStage.Bright`, or a blank capture), since it compares extents, not the text. No new capture.
- **The worked examples.** `NodeToolTests.Every_types_worked_examples_run_as_written` now exports every evaluating
  example (size 32), not only validates it;
  `MeshGraphTruthTests.The_mesh_curvature_example_is_the_base_colour_on_the_stand_in_and_wears_in_runs_along_a_real_edge`
  holds the caption's two claims, on the neutral stand-in and on a test `IMeshMaps` band (`2e502508`). Not seen failing:
  they guard claims that were true and unguarded.
- **swatch's note.** `Every_swatch_view_says_the_mesh_inputs_are_neutral` holds the flat, tiled, sphere, cube and sheet
  captions and the sheet's title label (`2e502508`), through `PatinaHarness(blankEngine: true)`, which answers each
  `SwatchRequest` with blank frames. Its `shortcut:` stays: the harness reaches Carve.Hosting's internal
  `EngineStatus.SetReady` by reflection, since Carve.Hosting has no public ready state and no InternalsVisibleTo and the
  test project has no other seam; a rename fails every blankEngine test with a NullReferenceException, and it is to be
  replaced when `EngineStatus` gains a public ready state. Not seen failing (new coverage of a true note).
- **CheckSamplesTests (Nit).** It and its stand-in both key on "a node type `NodeRegistry.BuiltIn` does not know", not
  on file names; `MeshNodeToolTests.MeshSamples` is MemberData, held by `The_mesh_samples_are_the_two_weathered_graphs`
  (`2e502508`). Not seen failing.
- **T-162's As built.** Its publish line now says `Carve.Core.dll` is new to patina's output, with RV-82's measurement
  (`1f4ebd5c`); a record, no test.
- **README.md:** patina's weathered paragraph names `save` and the nested case, and says check's fix names the neutral
  inputs for any mesh-reading output, nested or not.
- **Frames:** scenario-patina's PNGs compared by SHA-256 between the verified run at `08a82f54`
  (J-20261126-001448-df13e3) and a run queued at `origin/milestone/M-29` `ed94eadf` (J-20261126-001804-380271), both on
  Mantorok: 76 PNGs each (35 captures, 41 exported maps), none differ. No frame was opened.
- **Not checked:** the worked-example, swatch-note and CheckSamplesTests tests were not run against a disabled fix; no
  frame was looked at (none changed); carve's side of a nested mesh graph was not exercised here.
