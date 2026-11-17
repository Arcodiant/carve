---
description: >-
  The arm exported and loaded through the engine, compared with Carve's preview at three weights, and driven live with a weight slider in the model viewer
---
# T-106 — The arm in a game

**Status:** Open

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-105, T-104, T-108

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
- Evidence: `C:\Users\andre\artifacts\carve\M-21\T-106\`.
- **Goes back to the person, stop and ask:** any loaded-versus-preview difference beyond the frame rule; anything
  the person's pass finds.
- Sonnet. The review agent is separate.
