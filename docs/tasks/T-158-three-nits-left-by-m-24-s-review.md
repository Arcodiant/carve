---
description: >-
  One block rule for the uvs image checks, one path form in bake's partial-write error, and a test summary back on its method, as M-24's review left them
---
# T-158 — Three Nits left by M-24's review

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit, scenario-materials, scenario-bakes

## Goal

The three Nits RV-75 found in M-24 and the person chose to file rather than fix before the merge (2026-11-20) are
closed, with no frame, bake, threshold or check changing.

## Scope

- **One block rule.** T-121's block rule for an image under the `uvs` look is coded twice: in the Materials frame
  (`scenarios/Carve.Scenario.Materials/UvImageFrame.cs`) and in the Bakes checks (`BakesChecks.CheckUvImage`). The
  copies have drifted: since RV-74 the Bakes copy's `blend` reads each channel, the Materials copy does not. Make it one
  rule both scenarios link, keeping each frame's check as it is (the Materials frame strict, the curvature frames
  `blend: 2`, the occlusion and normal frames `blend: 1`). Every frame stays byte-identical, and each check still fails
  on its break.
- **One path form in `bake`'s partial-write error.** It names the failed file by its absolute path with the inner
  `bake:` prefix from `ToolPaths.Write`, and lists the written and unwritten files relative to the working folder
  (`src/Carve.Server/BakeTool.cs`, the write loop and the error's text). Name all of them one way, relative as the
  result's `path` is, and update `BakeToolTests`' partial-write test.
- **A summary back on its method.** In `tests/Carve.Core.Tests/BakeLayoutTests.cs`, commit 1de2a41 put the clamp tie
  test between the padding test's `<summary>` and its method. Move the summary back and give the tie test its own.

## Notes

- Filed from RV-75 (round 2 of M-24's review). The first Nit also came up in RV-74, where only the stage helpers were
  fixed, so it had survived two rounds; the person chose to file all three and close the milestone.
- **Stop and ask** before changing any check's tolerance or which frames it applies to.
