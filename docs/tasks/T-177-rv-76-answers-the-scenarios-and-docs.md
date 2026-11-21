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

### As built

Run on Opus 5.5 (claude-opus-5-5), in one agent.

- **The metallic break:** fixed. It judges the frames swapped, `!MetallicReads(lit[1], lit[0])`.
- **The painted-metal threshold:** no change needed. The person accepted 0.62 after T-142.
- **The unjudged frames:** fixed.
  - The tiling-3 sheet's sphere cell is judged against the sphere at tiling 3, with the break `sheet-tiling-ignored`.
  - The nested stage's old-wall and grime sheets are judged cell by cell against their exports (`LibraryStage.CheckGraphSheet`).
  - Their sphere cells are judged against two new frames, `nested-old-wall-sphere` and `nested-grime-sphere`, whose silhouettes are judged too.
  - The tiling-3 frame's change since T-140's job J-20261120-085227-39ade3 is T-157's ", tiling 3" label (9b207c2).
    The 1,600 px that differ lie at x 536 to 615, y 27 to 46, all inside the sphere cell's label band.
- **CLAUDE.md's Patina scenario row and the `src/Patina.Server` row:** fixed.
- **CLAUDE.md's Patina.SampleGraphs row:** fixed.
- **CLAUDE.md's edit_graph clause:** fixed.
- **T-155's and T-172's lines in the changelog, the milestone and their descriptions:** fixed, to say what was built.
- **T-172's Scope on `swatch.png`:** fixed.
- **Shown failing:** one `base` run with three planted breaks failed on each of them:
  - the two metallic spheres defined the other way round failed the rule and the swapped break;
  - the sheet drawn at tiling 1 failed its sphere cell on 78,131 px;
  - old-wall's sheet judged against the planks' texels failed every flat and tiled cell.
- **Frames:** compared by SHA-256 against J-20261121-173922-6c7470, the last passing `scenario-patina` job on M-28.
  - All 68 shared files are equal, except `swatch-tiling-checks.tsv`, which gains the new break's lines.
  - New in the run: `frames/nested-old-wall-sphere.png`, `frames/nested-grime-sphere.png` and `export-grime/`.
  - Looked at: the two new spheres, scaled down. They show bricks with grime, and grey streaks.
- **RV-76's outcomes** are `fixed` for seven findings, as `cairn lint` takes them, and `no change needed` for the threshold.
- **Not checked:**
  - The library suites' frames were not compared by hash. No milestone job holding them is fetchable, as T-172 found.
    This member changes no capture in the library stage; it only adds `CheckGraphSheet` beside it.
  - The person has not reviewed the two new sphere frames.
