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

### As built

Ran on Claude Opus 5.5 (`claude-opus-5-5`).

- **check's one-value text** (finding "check's new one-value reason says carve's check judges the real maps ..."):
  `CheckTool.NeutralFix` rewritten (commit 079079d9); where it is chosen (`TypesReaching` overlapping the mesh node
  types, `CheckTool.cs:123`) and the verdict (`one-value`, `ok` false) are unchanged, and an output that reads no mesh
  node keeps the rule's own fix. The text `check` now gives such an output, after the rule's measurement, whole:
  `Fix: it reads a mesh input, which in patina is a neutral stand-in, so it may be flat only for that; to see the real
  map, save the graph and draw it in carve: render mode 'uvs' with image {graph: the .texgraph's path, output: this
  output's name, size}; if it is flat there too, widen its levels.` Checked against carve: `RenderTool.ImageArgumentText`
  and `ImageLookText` take `image` as `{graph, output, size}` in mode `uvs` only, `graph` a `.texgraph` path (hence
  "save the graph"), the output "evaluated on this mesh, with its real curvature, occlusion, masks and position". The
  text names no carve `check`. New test `MeshGraphTruthTests.A_mesh_output_flat_by_its_own_levels_is_told_how_to_find_out_and_what_to_do`
  is the reviewer's probe (`mesh_curvature` -> `levels` 0/0 -> `wear`): shown failing in a scratch copy under `%TEMP%`
  with `CheckTool.cs` at the commit before (`Assert.Contains() Failure: Sub-string not found`). T-219's three tests follow
  the new text: the two in `MeshGraphTruthTests` assert `EndsWith(NeutralFix)` and pass unchanged; `MeshNodeToolTests.
  Check_tells_the_empty_mask_it_is_flat_for_the_neutral_inputs` asserted "widen" absent and now asserts the carve
  instruction comes before "if it is flat there too, widen its levels" and no "none here" (verify of 079079d9 failed it
  on both samples, which is how it was found). `check`'s description is not changed (its "one reading a mesh is told
  why" still holds), so its length is as it was and `Every_description_is_under_the_limit` passes.
- **The title break** (finding "The reworked title-note break still cannot fail on its own"): the stage captures a
  second plain sheet, `mesh-nodes-break-plain-sheet` (the same graph with `curv` set to a `gradient`, drawn by `swatch`
  through the tools), measures its title on its own, and the break runs `CheckTitleNote(probe, second, plain)`, which
  must fail (commits 079079d9, cc6708ee). The two captures compared are `mesh-nodes-break-plain-sheet` (presented as the
  mesh graph's) and `mesh-nodes-plain-sheet` (the plain title subtracted); they are two `swatch` calls, two frames, two
  `TitleExtent` measurements, so the outcome rests on what the second capture holds: a check that ignored its first
  argument (say `NoteWidth <= plain`) would pass on it and the break would report "the title without its note was
  accepted". That is no longer arithmetic: the first run (J-20261126-040901-4df0ed, the capture taken after save and
  open) measured the second title at x 338 against 258, because a saved graph's title is longer, and the check accepted
  it, so the break failed the run. The capture moved to beside the first plain one, before save; the verified run
  (J-20261126-041144-c21df6) logs `x 258 ... only 0 px past the title with no note (258)` in `mesh-nodes-breaks.txt`.
  The break lives in the stage, no product change.
- **Frames:** base J-20261126-002358-b8707d (`600a5a1d`, carried as `origin/milestone/M-29` 8d4b6be5's run), 35 frames;
  the verified run J-20261126-041144-c21df6 (on Mantorok, the base on Xellotath), 36 frames: 35 identical by SHA-256,
  none differ, one new, `mesh-nodes-break-plain-sheet`, byte-identical to `mesh-nodes-plain-sheet` (a plain sheet, as
  the stage's control is). The T-162 frames keep their hashes.
- **README.md:** patina's sentence on `check`'s fix for the empty masks now says they may be flat only for the neutral
  inputs, to draw them in carve with `render` mode `uvs` and the graph as `image`, and to widen the levels if flat there too.
- **Not checked:** the instruction was not run end to end in carve (a saved mesh graph drawn with `render` `uvs`); the
  break was not run against a deliberately broken `CheckTitleNote` (the run where the second title differed stands in
  for it); the two plain captures are byte-identical, so the break shows independence of measurement, not of renderer.
