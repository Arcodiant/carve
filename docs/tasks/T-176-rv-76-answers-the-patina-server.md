---
description: >-
  The patina server's RV-76 findings on save, the swatch sheet, export and override wording are answered, fixed where they are defects
---
# T-176 — RV-76 answers: the patina server

**Status:** In Progress

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

- **Decided (2026-11-21, by the person):** the label atlas grows to fit the outputs, rather than the sheet refusing more than 15; every sheet of 15 outputs or fewer stays byte-identical.
- The save fix covers `pop_snapshot`'s move too, with a test that saves into a different folder.
- Owns `FileTools.cs`, `GraphStore.cs`, `SwatchImages.cs`, `SwatchRendering.cs`, `ParameterOverrides.cs`, and export's metallic refusal in `ExportTool.cs` (T-173 adds a GC there; keep both sides). Every description stays at 3,686 characters or under.
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### As built

Two agents, both Claude Opus 5.5 (`claude-opus-5-5`): the first made the fixes and tests (`08d9d95`) and handed off
at its call checkpoint; the second compared the frames, wrote this section and finished.

- **Save to another folder (Major):** fixed. `SaveTool` judges `graph with { Folder = <target's folder> }` and refuses
  with "the graph saved in <folder>, where its graph nodes' paths resolve, has N faults"; nothing is written and the
  open graph is unchanged. pop_snapshot's state is `store.Graph` (placed in the store's folder), not the snapshot as
  pushed. The save and pop_snapshot descriptions say so. Tests in `NestedGraphToolTests` save into a different folder,
  for save and for pop_snapshot after it.
- **Judgement call:** a save whose `graph` nodes would not resolve from the new folder is refused rather than
  rewritten. Save's existing rule, that a saved graph evaluates, settles it applied where the file goes; rewriting
  the paths would change what the graph names, beyond the finding.
- **Sheet of more than 15 outputs:** fixed, as the person ruled (2026-11-21): the label atlas grows in pages of 32 rows
  (`LabelAtlas.Pages`). Page 0 is drawn in the sphere's frame exactly as before; each further page gets an
  `OffscreenFrame.ReadAsync` of its own, and the pages are stacked into one buffer, so `Compose`'s indexing is
  unchanged. New scenario stage `WideSheetStage.cs` (18 columns, labels on page 2 looked for as white text), one line
  in `PatinaStages.cs`; it captures no frame. Unit test
  `SwatchTests.A_sheet_of_more_than_15_outputs_reads_its_labels_from_a_second_atlas_page`. CLAUDE.md's patina Layout line names the atlas pages.
- **export refusing metallic beside roughness:** fixed. metallic is refused only when roughness is not named; beside
  roughness it is dropped from the list and written in roughness.png (`MetalPlateTests`).
- **Override wording:** fixed. `Colon` puts ": " only before a leading " is "
  (`ExposedParameterToolTests.An_override_value_patina_cannot_read_is_worded_with_one_colon`).
- Each finding's `**Outcome:**` line in RV-76 is filled; the three check-tool findings there are left to T-174.
- **Descriptions:** every one at 3,686 characters or under, held by `NodeToolTests` in patina-unit.
- **Frames, by SHA-256:** this member's scenario-patina job `J-20261121-183313-83f3e6` (`08d9d95`) against:
  - T-177's job `J-20261121-183529-2c4ced` (`2984e75`, the milestone's tip plus T-177's scenario checks, the newest
    scenario-patina job the queue holds on M-28's code): all 29 frames equal. T-177's job has two more frames of its
    own (`nested-*-sphere.png`), and its `swatch-tiling-checks.tsv` adds T-177's new check's lines.
  - T-140's `J-20261120-085227-39ade3`, the last good job recorded on the milestone: 23 of the 24 frames it has equal;
    `swatch-sheet-grid-tiling-3.png` differs, as T-172 and RV-76 recorded for work landed after that job, not this
    member. No M-28 tip job of the milestone's own verify could be found in the queue's history.
  - No frame image was opened.
