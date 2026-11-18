---
description: >-
  The arm exported and loaded through the engine, compared with Carve's preview at three weights, and driven live with a weight slider in the model viewer
---
# T-106 — The arm in a game

**Status:** In Progress

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-105, T-104, T-108

**Verify:** scenario-morph

## Goal

The arm reaches a game: it is exported, read by the engine, drawn by the engine at its weights to match Carve's
preview, and driven live by a person in the viewer's window.

## Scope

- **Export:** `scenario-morph` exports the arm, skinned with `Curl`, into a content folder in the run folder.
  Load checks in a new `tests/Carve.MorphLoad.Tests` (reading `CARVE_MORPH_RUN`, skipped when unset, linking
  `RunContent.cs`) run as the suite's last step, as `run-character-scenario.ps1` does:
  - `TargetNames` is `["Bulk", "Lean"]`;
  - `DefaultWeights` are 0;
  - every position and normal delta equals Carve's;
  - no warnings;
  - `samples/arm.carve` is compared by content.
- **Frames 8–10:** `render_glb` of the export at Bulk 0, 0.5 and 1, `+z`. Each is compared with `render`'s
  frame at the same weights under D-6's frame rule. Frame 8 has no `render` twin among T-105's frames, so the
  scenario takes one.
- **The person's real-time pass, before frames 8–10 are approved:** the viewer in window mode on the exported
  arm, with the slider. Ask the person through `mcp__waystone__review` to drag Bulk and Lean and play `Curl`,
  with a short note of what to judge: smooth growth, no crack at the elbow, the dial following the key. Send the
  launch command in the review's context. The frames go to review only after the person answers. "Commented" is
  not approval.
- Update CLAUDE.md's layout table (`Carve.MorphLoad.Tests`, the script if one is added).

## Notes

- `scenario-morph` does not exist until T-103 adds it to `tests.json`. Add it to this item's `**Verify:**` line once it is there (`cairn lint` refuses an unknown suite before then).
- Evidence: `~\artifacts\carve\M-21\T-106\`.
- **Goes back to the person, stop and ask:** any loaded-versus-preview difference beyond the frame rule; anything
  the person's pass finds.
- Sonnet. The review agent is separate.

### As built

Built by Claude Sonnet 5.5 (the implementer); the real-time pass and frame review by a separate Claude Sonnet 5.5 agent, who finished it.

- **Export.** `ArmStages.ExportAndCompareAsync` (`scenarios/Carve.Scenario.Morph`), run at the end of the arm's stages (after `Bad` is deleted, before `Curl`
  is deleted for the cylinder): `export` of the arm into `game/Content/models/arm.glb` of the run folder, skinned, 3 bones, clip `Curl`, 672 triangles.
  Suite `scenario-morph` now runs `scripts/run-morph-scenario.ps1` (scenario, then the checks) and is on this item's `**Verify:**` line.
- **Frames 8 to 10** (`arm-8-glb-bulk-0-z`, `arm-9-glb-bulk-half-z`, `arm-10-glb-bulk-1-z`): `render_glb` of the export, `+z`, orthographic, with
  `targets` Bulk 0, 0.5 and 1, each against `render` at the same weights under D-6's frame rule (Orcward's `Comparison`, linked as `ComparisonPng.cs`).
  Twins: frame 1 for Bulk 0 and frame 2 for Bulk 1 (T-105's frames); Bulk 0.5 alone has none, so the scenario takes one (`arm-9-twin-bulk-half-z`).
  **Result: 0 of 262144 pixels differ at all three weights.** Frames 8 and 10 are byte-identical (sha256) to T-105's approved frames 1 and 2; frame 9 differs
  from its twin in some bytes but in no pixel past the rule's luma threshold. A control (the file at Bulk 1 against the model at rest) fails the rule
  (11318 pixels, patch 4058). `differences.tsv` and `compare/*.png` (render | render_glb | difference) are in the run folder.
- **Load checks** (`tests/Carve.MorphLoad.Tests`, regression, 6 tests, read `CARVE_MORPH_RUN`, link `RunContent.cs`): `Load` gives `TargetNames` `[Bulk, Lean]`,
  `DefaultWeights` 0, no `Warnings`, three bones and `Curl`; every position and normal delta of every loaded vertex equals Carve's exactly (the T-104 test's
  method, on the scenario's saved `work/arm.carve`); `samples/arm.carve` and `arm-lean.carve` equal the saved files by content; `differences.tsv` holds.
- **Not-planar warnings (the person's ruling, 2026-11-18).** The export reports not-planar faces (the subdivided, smoothed quads), folded by T-72: 10 named, the rest one line. The scenario
  and the load check accept only not-planar warnings and assert the folded face count equals what `select faces where non_planar` names, as `scenario-soft` does (`NotPlanarFold` linked; the
  scenario writes `work/select-non-planar-mesh.txt`). Any other warning fails. Shown failing: with the select file one face short, `The_export_is_clean` fails; a test also fails the check
  with a count one over, one under and a foreign warning added. The loader's own `Warnings` is still empty. The rule itself is T-127, outside M-21.
- **Broken product shown.** `CARVE_MORPH_BREAK=glbweights` (`render_glb` sent no `targets`) fails the scenario: `arm-9-glb-bulk-half-z draws the same as arm-8-glb-bulk-0-z, pixel for pixel`.
- CLAUDE.md's layout table has `Carve.MorphLoad.Tests`, `run-morph-scenario.ps1` and the extended Morph row.
- Engine needs: none. Files outside the worktree: the content folder copied to `G:\My Drive\artifacts\carve\M-21\T-106\round-1\game\` for the person's viewer run.
- Not checked: the viewer window (no input is scripted); only a headless viewer run on the exported arm with `--targets Bulk=0.5,Lean=0.25` (it loads, 672 triangles). No frame image was opened by me; the three comparisons are pixel counts.

- **The person's verdicts.** Real-time pass in the viewer on `arm.glb` (Bulk, Lean, Curl, instructions in `round-1/viewer-pass.md`): approved. Frames 8 to 10 and their triptychs (`round-2`): approved. Frames 8 and 10 are byte-identical to T-105's approved frames 1 and 2, frame 9 has 0 differing pixels against its twin.
- **Model.** Sonnet 5.5 throughout; the review and finish were a separate Sonnet 5.5 agent.
- **Not checked.** Nothing scripts the viewer window; the person's pass is the check.
