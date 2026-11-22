---
description: >-
  Every finding of RV-77's `## Findings: round 2, the engine, nodes and checks` is answered
---
# T-178 — RV-77 answers: the engine, nodes and checks

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, patina-regression

## Goal

Every finding of RV-77's `## Findings: round 2, the engine, nodes and checks` is answered, so that M-28's review can close with a light confirming round.

## Scope

- All four findings under `## Findings: round 2, the engine, nodes and checks (claude-opus-5-5)` in RV-77.
- Each fix with a test that fails without it, shown failing where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-77, in this task's worktree.

## Notes

- **Decided (2026-11-21, by the person):** the tile cache's cap stays tiles-only. Arrays waiting for reuse and nodes' scratch are counted in `LastHeldBytes` and documented, and the 2 GB regression tests hold the whole process. Record this ruling in T-173's Notes and in the milestone file's decisions; that answers the finding that it is recorded nowhere.
- The blur peak finding (1,592 MB against 1,226 MB at 8192, Debug): find the cause by measurement. Fix it if it is a defect; otherwise record the cause and the margin.
- Owns `src/Patina.Graphs/Evaluation/`, the nodes, `Checks/`, `CheckTool.cs`, the output-name validation for `metallic_roughness`, and their tests.
- **Answering RV-77:** for each finding, fix it or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-77-review-of-m-28-round-2.md` in this task's worktree. The line is exactly `**Outcome:** fixed` (or the other two forms); put any detail on a following `**Fix:**` line, since `cairn lint` accepts nothing after `fixed`. Run `cairn lint` and read its output before `finish`.
- A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
