---
description: >-
  scenario-patina's library stage also captures each sample on swatch's cube at tiling 3, judged by T-157's cube check
---
# T-169 — Cube captures in the library stage

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** scenario-patina

## Goal

Every sample material in M-28's library is seen on the cube as well as the sheet and the sphere, so a regression in how
a sample reads on flat faces and edges is caught.

## Scope

- In `scenarios/Carve.Scenario.Patina/LibraryStage.cs` (T-142), one `swatch` `view: cube` capture per sample at
  tiling 3, judged face by face by T-157's `SwatchCubeCheck` from the exported texels.
- A break on the scenario side (a face mirrored) caught on at least one sample.
- The new frames shown to the person before landing.

## Notes

- **Filed (2026-11-20, by the person's choice):** T-142 proposed cube captures in its review and the person approved
  the stage without ruling on them. Asked, the person filed them off-milestone rather than adding them to M-28.
- Sonnet: concrete.
