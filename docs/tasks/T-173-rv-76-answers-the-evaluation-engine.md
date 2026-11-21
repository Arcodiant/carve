---
description: >-
  Every finding of RV-76's `## Findings: the evaluation engine` is answered, fixed where it is a defect, with outputs byte-identical
---
# T-173 — RV-76 answers: the evaluation engine

**Status:** Done

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, patina-regression

## Goal

Every finding of RV-76's `## Findings: the evaluation engine` is answered, fixed where it is a defect, with outputs byte-identical, so that M-28's review round can pass.

## Scope

- All nine findings under `## Findings: the evaluation engine (claude-opus-5-5)` in RV-76.
- Each fix with a test that fails without it, where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-76, in this task's worktree.

## Notes

- **Decided (2026-11-21, by the person):** the strong minify of a multi-port node (pinning every port, about 1.5 GiB for `tiles` at 8192) is fixed in M-28: the evaluator pins only the output ports a node reads, with a `regression` test holding a multi-port minify at 8192 under 2 GB.
- Owns `src/Patina.Graphs/Evaluation/`, `Imaging/`, `Nodes/NodeType.cs`, `BlurNode`'s and `DistanceNode`'s buffers, export's GC between evaluations, and CLAUDE.md's sentence on which nodes are separable. T-176 also edits `ExportTool.cs` (export's metallic refusal); keep both sides on a rebase.
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### As built

One agent, on Claude Opus 5.5 (`claude-opus-5-5`). Outputs byte-identical: every two-pass test against the one-pass node, every golden and the regression suite pass unchanged. Each finding's outcome, in RV-76's order:

- **Kept arrays outside the bound:** the contract is corrected, not the policy. `PeakBytes` stays the tiles; `GraphEvaluator.LastHeldBytes` adds the waiting arrays and nodes' scratch. Putting both under the bound was built and measured: it made a blur at 1024 in tiles of 64 under 4 MiB make 12,640 tiles against 1,840, so it is a question for the person, not landed.
- **Multi-port minify:** fixed as ruled. An entry keeps only the ports read, and `BandTiles` counts only those. Regression `minify-mask` at 8192 (Debug) peaked at 991 MB.
- **`BandTiles` cast:** fixed, clamped in `long`.
- **Blur and distance scratch:** fixed through `NodeEvaluation.Scratch`. The last tile's scratch is kept for the next, outside `FreeLimit`. Allocation for that blur fell from 397 MiB to 18 MiB.
- **No unit test for the warm-up, the cold list or the bands:** fixed with a tile-count test at 2.1 times the fewest. Measured 1,840; with each piece removed in turn: 2,716 (no warm-up), 48,620 (no cold list), 11,407 (no band narrowing).
- **CLAUDE.md says transform is separable:** fixed.
- **`WarmRows` comment:** fixed.
- **`TileRead.Row` with several rectangles:** fixed, each rectangle tried in turn.
- **Export's collection between roughness and metallic:** fixed. No test holds it.

Debug regression at 8192 (Xellotath): turn 781 MB, distance 793 MB, blur 1,592 MB in 563 s, minify 1,522 MB, minify-mask 991 MB. The bound is 2,048 MB. The blur's peak is higher than T-155's 1,226 MB. It is not looked into here; As built already says those peaks move with the collector's timing.
