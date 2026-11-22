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

### As built

Two agents, both Claude Opus 5.5 (`claude-opus-5-5`; Sonnet was under its weekly limit): the first wrote the code and handed off at its call checkpoint; the second verified, ran the shown-failing checks and the frame review, and answered RV-77.

RV-77's ten findings under `## Findings: round 2, the format, server and scenarios`, each `fixed`:
- Fan-out through drives: `GraphValidator.NodeBudget` caches each nested file's parameter read per validation and charges a miss to the 4096 cap (through `GraphParameters.Analyse` and `WithOverrides`), so past it a drive is a fault on its parameter. Without the fix, fan 6 had not finished after 20 s.
- Outer graph node with an expanded id: the use is left unexpanded and the outer node's id fault is the only one. The test failed without the fix.
- Metallic break: a darker dielectric (grey 100, metallic 0, frame `swatch-sphere-metallic-0-dark`, approved by the person) judged as the metal; only the rule's clipped half refuses it. With `MetallicReads` planted to its median half (not committed), the break failed and the metallic check passed.
- Save into a folder holding another file of the same name: refused, naming both files (`SaveTool.Changed`). The test failed with the check disabled.
- Changelog: T-172's old line removed.
- CLAUDE.md: a page for each 32 labels.
- CLAUDE.md: the Patina row names `WideSheetStage.cs`.
- Node cap summary: reworded as a cap on what nesting takes in; behaviour unchanged.
- WideSheetStage: names one letter longer per column; each label is judged against its backing, and the tiled label must begin with the flat one. `wide-sheet-labels.tsv` shows each label ending 4 px short of its backing (allowed: 3 to 6), and a label a letter off is 8 px out, so no threshold was changed.
- RV-76 How lines: `1f69cb2` to `a1ffbd9`, an ancestor of the milestone.

Checked: patina-unit in full (461 + 162 passed, 6 skipped). Against job `J-20261121-184545-5c2ba5`, 65 frames are identical by SHA-256 and the only new one is `swatch-sphere-metallic-0-dark`.

Surprising: backslash escapes in Python heredocs through the Bash tool became real newlines and tabs (fixed with `chr(92)`); a `cairn test artifacts` run from the container root wrote `test-artifacts/` there, which was moved to `%TEMP%`.
