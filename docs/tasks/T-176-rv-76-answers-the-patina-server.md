---
description: >-
  The patina server's RV-76 findings on save, the swatch sheet, export and override wording are answered, fixed where they are defects
---
# T-176 — RV-76 answers: the patina server

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit, scenario-patina

## Goal

The patina server's RV-76 findings on save, the swatch sheet, export and override wording are answered, fixed where they are defects, so that M-28's review round can pass.

## Scope

- Four findings under `## Findings: the patina server (claude-opus-5-5)` in RV-76: a save to another folder breaking its graph nodes (Major); a sheet of more than 15 outputs throwing; export refusing `metallic` with `roughness` named; the garbled override wording. The three check-tool findings there are T-174's.
- Each fix with a test that fails without it, where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-76, in this task's worktree.

## Notes

- **Decided (2026-11-20, by the person):** the label atlas grows to fit the outputs, rather than the sheet refusing more than 15; every sheet of 15 outputs or fewer stays byte-identical.
- The save fix covers `pop_snapshot`'s move too, with a test that saves into a different folder.
- Owns `FileTools.cs`, `GraphStore.cs`, `SwatchImages.cs`, `SwatchRendering.cs`, `ParameterOverrides.cs`, and export's metallic refusal in `ExportTool.cs` (T-173 adds a GC there; keep both sides). Every description stays at 3,686 characters or under.
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.
