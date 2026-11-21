---
description: >-
  Every finding of RV-76's `## Findings: the scenarios and test configuration` and `## Findings: the whole diff` is answered
---
# T-177 — RV-76 answers: the scenarios and docs

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina, scenario-patina-library-1, scenario-patina-library-2

## Goal

Every finding of RV-76's `## Findings: the scenarios and test configuration` and `## Findings: the whole diff` is answered, so that M-28's review round can pass.

## Scope

- All eight findings under `## Findings: the scenarios and test configuration (claude-opus-5-5)` and `## Findings: the whole diff (claude-opus-5-5)` in RV-76.
- Each fix with a test that fails without it, where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-76, in this task's worktree.

## Notes

- The painted-metal sphere's `MetalMin` 0.62 was accepted by the person after T-142 (recorded in T-142's Notes); answer that finding with that, unless a sharper measure is found that keeps every approved frame.
- The tracker docs it corrects (T-155's and T-172's lines in the milestone file and the changelog, their descriptions, CLAUDE.md's rows) say what was built, not the goal.
- Owns `scenarios/Carve.Scenario.Patina/`, `tests.json`, CLAUDE.md's rows, the milestone file's member lines, and the changelog. T-173 also edits one CLAUDE.md sentence; keep both sides.
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
