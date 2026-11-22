---
description: >-
  Every finding of RV-77's `## Findings: round 2, the engine, nodes and checks` is answered
---
# T-178 — RV-77 answers: the engine, nodes and checks

**Status:** Done

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

### As built

One agent, on Claude Opus 5.5 (`claude-opus-5-5`). No committed output or frame changed. Each finding's outcome, in RV-77's order:

- **Blur's 8192 peak:** no change needed, by measurement. Three Debug exports of the blur graph at 8192, side by side, sampled every 250 ms (driver and samples in `artifacts/T-178/`): before T-173 1,154 MB, the current code 1,595 MB (reproduced, not noise), the current code under a 900 MB GC heap limit 1,112 MB in the same 753 s; all three PNGs are byte-identical. The rise is heap the collector leaves uncollected without pressure (most likely because the scratch fix spends the large-object budget that triggered full collections far more slowly), not memory the code holds. The margin under 2,048 MB is 453 MB; the regression test is unchanged.
- **The cap question left nowhere:** fixed by the person's ruling (the cap stays tiles-only), recorded in T-173's Notes and M-28's decisions.
- **`metallic_roughness` as an output name:** fixed; the validator reserves it, a fault on `name`, held in `RolesAndNamedOutputsTests`.
- **worley's ring bound at jitter 1 only:** fixed in the test; manhattan at 0.9 and euclidean at 0.95 added. A margin of `1 - jitter` fails the manhattan case at 0.9 (shown, then reverted). At 0.5 the 3 x 3 cells always hold the true second nearest, so no jitter that low can hold the bound.
