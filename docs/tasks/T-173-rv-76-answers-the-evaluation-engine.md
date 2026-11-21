---
description: >-
  Every finding of RV-76's `## Findings: the evaluation engine` is answered, fixed where it is a defect, with outputs byte-identical
---
# T-173 — RV-76 answers: the evaluation engine

**Status:** In Progress

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
