---
description: >-
  Third review of M-1
---
# RV-3 — Third review of M-1

**Status:** Open

**Reviews:** M-1

**Round:** 3

**Previous:** RV-2

**Diff:** c953a53eb22313b5fb73eaa655eb9fc19beab6b4 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-2 fixes (claude-opus-5-5)

### The framing check passes images that are cropped at the frame edge or squashed

`src/Carve.Server/ViewFraming.cs:125-160`, `src/Carve.Server/RenderSystem.cs:129-134`. Correctness (false success). `Fits` predicts the GPU's rounding on the CPU, and past about 1e6 the prediction is wrong: over 97 unit-cube offsets it accepts, on the real `carve`, the predicted box was off the real pixels by a median of 4 px and at worst 75 px. At (2.5e6,2.5e6,2.5e6) `back`, real x 0..383 runs off the left edge. At (-3598575.2,-3598575.2,3598575.2) `front`, real x 256..511. At (0,-4e6,0) `front`, predicted y 85..406, real y 84..481. At (0,5e6,5e6) `back`, 61,496 pixels against 108,567. All return an image and the normal caption.

**Outcome:** —

### T-4's As built misdescribes what frames

`docs/tasks/T-4-shaded-render-tool.md:73,75`. Tracker hygiene. "Within a pixel or two", "up to 40 pixels off centre" and "3e6 frames in front only, 4e6 and up are the error" hold only on the +o,+o,+o diagonal: the worst gap is 75 px, the worst offset 102 px at (0,3e6,0) `front`, and (0,6e6,6e6) `front` returns an image. Refusal starts well below 3e6: in 120,000 box and view cases, 1% at offset/side 3e5, 25% at 1e6, 55% at 2e6. The true shape is a noisy band from about 1.5e5 to about 7e6.

**Outcome:** —

### An outline with a bridged hole, accepted before the fixes, is now refused as folded

`src/Carve.Core/Triangulator.cs:144-161`, `tests/Carve.Core.Tests/KernelReviewTests.cs` `NonAdjacentCollinearEdgesThatOverlapAreAFold`. Correctness (false refusal, regression). A wall with a window as one face, the usual keyhole bridge with duplicate vertices at its two ends: `{"vertices":[[0,0,0],[10,0,0],[10,10,0],[0,10,0],[0,0,0],[3,3,0],[3,7,0],[7,7,0],[7,3,0],[3,3,0]],"faces":[[0,1,2,3,4,5,6,7,8,9]]}`. At c069870: 8 triangles, 10 of 10 vertices. At HEAD: `face folds back on itself: the edge from faces[0][4] overlaps the edge from faces[0][9]`. The tool has no other way to express a hole.

**Outcome:** —

### The ordinary mis-ordered quad still does not get the named crossing

`src/Carve.Core/PolygonMesh.cs:105-112`. Correctness. The zero-area check runs first, so a unit square listed out of order, `[[0,0,0],[1,0,0],[1,1,0],[0,1,0]]` with `[[0,2,1,3]]`, gives `face has zero area`, though its corners span area 1. This is the commonest self-crossing an agent writes.

**Outcome:** —

### A new test fails on a clean checkout, depending on test order

`tests/Carve.Server.Tests/RenderTests.cs:299`, directory created only at `:51`. Tests. From a `git archive` copy of HEAD, `An_extreme_mesh_is_framed_or_refused_and_the_next_render_works` threw `DirectoryNotFoundException ... artifacts\render-tests\samples.txt`; a rerun passed.

**Outcome:** —

## Summary

Round 3 of M-1, one reviewer on claude-opus-5-5, checking RV-2's answers and the fixes (`git diff c069870 HEAD`). The non-framing answers held. The framing finding failed for the third round, so it went to the person, who chose to normalise the view: the bridge recentres and scales a copy for drawing, which replaces the check and its error. The stored mesh is unchanged.
