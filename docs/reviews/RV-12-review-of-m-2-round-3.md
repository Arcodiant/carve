---
description: >-
  Review of M-2, round 3
---
# RV-12 — Review of M-2, round 3

**Status:** Answered

**Reviews:** M-2

**Round:** 3

**Previous:** RV-11

**Diff:** a04ef9eabae9fad58d2292b48f7290edace26950 against origin/main

**Reviewer:** claude-opus-5-5 (one reviewer, checking the RV-11 fix)

## Findings: the RV-11 fix, 95e8fb2 (claude-opus-5-5)

The fix holds for real-scale meshes, checked with a throwaway probe:

- A short edge with a big distance says "large", including when the short edge is the last one, from corner 4 back to 0.
- A long edge with a tiny distance (1e-30) says "small".
- The inset case (distance 0, inset 1e-30) says "small".
- A ring-vertex error (non-finite at z=3e38) says "large".
- The 1e-30 and 3e38 rows are unchanged.
- No other text, tolerance or rule changed.
- The new test fails on the old code, and the last edge maps right.

`core-unit`: PASS, 127/127.

### The edge length is computed in float, so it underflows or overflows at extreme scales and the word flips

**Severity:** Nit

`ToSourceFace` (`src/Carve.Core/MeshEdits.cs`, the new `edge.Length()` line) uses `Vector3.Length()`, which squares the edge in float. The zero-area rule itself (`PolygonMesh.FaceSize`/`HasArea`) works in double and accepts both meshes below:

- **Underflow:** a unit square scaled to 1e-25, extruded with distance 1e-35. The squared length, 1e-50, underflows to 0, so the message says "too large". The distance is 1e-10 of the edge, so it is too small. The old bounding-box code said "small" here, so this is a regression.
- **Overflow:** a square scaled to 2e19, extruded with distance 1e30. The squared length, 4e38, overflows to +Inf, so the message says "too small". It should say "large", and the old code did.

Fix: measure the edge in double, as `PolygonMesh` does, and add the 1e-25/1e-35 square as a test row.

**Outcome:** fixed

The wall's edge length is computed in double from the float corners and compared with `Math.Abs((double)value)`. The theory `The_edge_is_measured_in_double_so_extreme_scales_keep_the_word` (1e-25 square by 1e-35 says small; 2e19 square by 1e30 says large) was shown failing on the old code (c637d21, claude-sonnet-5-5).

## Summary

A re-review round of one Opus reviewer, checking only 95e8fb2. It raised 1 Nit. The person chose to fix it and run one more round.
