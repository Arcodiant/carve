---
description: >-
  RV-83: patina's check text and the title break
---
# T-227 — RV-83: patina's check text and the title break

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina

## Goal

patina's `check` says only what is known about a flat output that reads a mesh node, and the scenario's break for
the title note fails on two titles the product really drew.

## Scope

Each finding of RV-83 named here, by its heading there:

- "The reworked title-note break still cannot fail on its own: it subtracts the plain title's extent from itself"
- "check's new one-value reason says carve's check judges the real maps, but carve's check evaluates no graph, and the reason is also given to outputs that are flat for a real reason"

- **`check`'s text, as the person ruled on the round 2 finding (2026-11-25):** for a `one-value` output that reads
  a mesh node, `check` says only what is known: it may be flat only because its mesh inputs are neutral stand-ins in
  patina; to see the real map, draw it in carve (`render` mode `uvs` with the graph as `image`); if it is flat there
  too, widen its levels. It no longer says that carve's `check` judges the real maps (carve's `check` evaluates no
  graph) and no longer says "Fix: none here". Declined: going back to the old text for every output. The verdict
  (`one-value`, `ok`) is unchanged, an output that reads no mesh node keeps the old text, and the README's sentence
  follows. Check each claim in the new text against carve's code before writing it (that `render` mode `uvs` takes
  `image` as `{graph, output, size}` and draws that output on the mesh). `check`'s description stays within the
  length test.
- **The break, as the person ruled (2026-11-25):** this finding has survived two rounds. The break captures a
  second sheet whose title really has no note, the same graph with its mesh node replaced by a plain one, drawn by
  the product, and presents that capture as the mesh graph's, so the title check fails on two independently measured
  titles. Declined: accepting the break as it is, and replacing the frame check with one on the answer's text. No
  product change; a break lives in the scenario. If the stage already captures such a plain sheet as its control, the
  break needs a capture that is not the very frame the check subtracts from (a second capture of it, or of a second
  plain graph): say which, and why the check could pass on it if the check were broken.
- Frames: the Patina scenario gains at most the break's capture; every existing frame keeps its hash against a run
  of `origin/milestone/M-29`.
- **Owns:** `src/Patina.Server/CheckTool.cs`, `tests/Patina.Server.Tests`, `scenarios/Carve.Scenario.Patina`, patina's
  section of `README.md`.

## Notes

- **A fix member of RV-83,** round 2 of the review of M-29 (`docs/reviews/RV-83-review-of-m-29-checking-rv-82-s-answers.md`). Each finding listed in the Scope is read in
  full there: where it is, what is wrong, and how the reviewer showed it. RV-82 is round 1; the member whose fix a
  finding is about says what it did in its `### As built`.
- Every finding gets a fix with a test that fails without it, shown failing (in a scratch copy with the fix's hunk
  reverted), or, where the finding is about a record, the record made true. A finding that turns out not to hold is
  answered `no change needed: <reason>` with the evidence.
- The member sets the `**Outcome:**` line of each of its findings in RV-83 to a bare `fixed` (`cairn lint` accepts
  nothing after it) or `no change needed: <reason>`, and touches no other finding's line. Commits and test names go
  in this member's `### As built`.
