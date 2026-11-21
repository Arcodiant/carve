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

### Hand-off

First agent, Claude Opus 5.5 (`claude-opus-5-5`), handing off at its call checkpoint (about 60 calls). Commit
`08d9d95`.

- **Done:** all four findings fixed, with tests, and their `**Outcome:**` lines filled in RV-76.
  - Save: `SaveTool` checks `graph with { Folder = <target's folder> }` and refuses with "the graph saved in <folder>,
    where its graph nodes' paths resolve, has N faults". It does not rewrite paths, because the existing rule (a saved
    graph evaluates) settles it. pop_snapshot's state is now `store.Graph` (placed in the store's folder), not the
    snapshot as pushed. The save and pop_snapshot descriptions say so.
  - Atlas: drawn in pages of 32 rows (`LabelAtlas.Pages`). Page 0 is drawn in the sphere's frame exactly as before.
    Each further page gets an `OffscreenFrame.ReadAsync` of its own, and the pages are stacked into one buffer, so
    `Compose`'s indexing is unchanged. A new scenario stage, `WideSheetStage.cs` (18 columns, labels on page 2 looked
    for as white text), with one line in `PatinaStages.cs`. It captures no frame.
  - Export: metallic is refused only when roughness is not named; beside roughness it is dropped from the list.
  - Overrides: `Colon` puts ": " only before a leading " is ".
  - CLAUDE.md's patina Layout line names the atlas pages.
- **Checked:** `cairn task T-176 verify` passed on `08d9d95` (patina-unit job J-20261121-183309-4a4287,
  scenario-patina J-20261121-183313-83f3e6, so WideSheetStage passed on the engine).
- **Left:**
  1. Compare by SHA-256 the frames of `cairn test artifacts J-20261121-183313-83f3e6` with the milestone's last good
     scenario-patina job: find its id with `cairn test status`, or the newest job on `origin/milestone/M-28`'s tip.
     Every frame should be equal, since no frame has more than 15 outputs. Open none unless one differs.
  2. Write `### As built`.
  3. `git rebase origin/milestone/M-28` if siblings have landed. T-175 touches path resolution and T-173 touches
     ExportTool.cs; keep both sides.
  4. verify, commit, `cairn lint`, finish.
- **Surprising:** nothing yet. The description lengths were not measured separately, but `NodeToolTests` (in
  patina-unit) holds every description at 3,686 or less, and it passed.

