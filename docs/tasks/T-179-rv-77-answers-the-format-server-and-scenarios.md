---
description: >-
  Every finding of RV-77's `## Findings: round 2, the format, server and scenarios` is answered
---
# T-179 — RV-77 answers: the format, server and scenarios

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina, scenario-patina-library-1, scenario-patina-library-2

## Goal

Every finding of RV-77's `## Findings: round 2, the format, server and scenarios` is answered, so that M-28's review can close with a light confirming round.

## Scope

- All ten findings under `## Findings: round 2, the format, server and scenarios (claude-opus-5-5)` in RV-77.
- Each fix with a test that fails without it, shown failing where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-77, in this task's worktree.

## Notes

- **Decided (2026-11-21, by the person):** three of these follow RV-76 findings whose fixes did not hold, and the person chose to fix them now, with a light round 3 to confirm: fan-out through parameter drives (validation must be bounded, as the 4096-node cap bounds expansion); an outer `graph`-type node colliding with an expanded id (a fault, never a throw); and the swapped metallic break (a break must be able to fail on its own, not only with the check it breaks).
- Owns `GraphValidator.cs`, `GraphParameters.cs`, `FileTools.cs`, `GraphStore.cs`, scenario-patina's stages, `CLAUDE.md`'s rows, the changelog and the tracker lines. T-178 edits the milestone file's decisions; keep both sides on a rebase.
- **Answering RV-77:** for each finding, fix it or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-77-review-of-m-28-round-2.md` in this task's worktree. The line is exactly `**Outcome:** fixed` (or the other two forms); put any detail on a following `**Fix:**` line, since `cairn lint` accepts nothing after `fixed`. Run `cairn lint` and read its output before `finish`.
- A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
