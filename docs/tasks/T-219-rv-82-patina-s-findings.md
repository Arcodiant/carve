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
