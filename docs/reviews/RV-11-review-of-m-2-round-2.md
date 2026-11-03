---
description: >-
  Review of M-2, round 2
---
# RV-11 — Review of M-2, round 2

**Status:** Open

**Reviews:** M-2

**Round:** 2

**Previous:** RV-10

**Diff:** 10e622032da3731098f8da4895ac118041dd4114 against origin/main

**Reviewer:** claude-opus-5-5 (one reviewer, checking round 1's fixes)

## Findings: round-1 fixes, 63c744b..9ef45ea (claude-opus-5-5)

Evidence: `core-unit` 126/126 and `server-unit` 95/95. `server-regression` had 150 passed and 0 failed when the review was written, covering EditToolTests, LoaderTests, DebugExtensionTests, StdioTests, DefineMeshTests and the render caption, bad-camera and B-1 tests; a few slow RenderTests had not finished. The scenarios were not run and no frame was opened. One throwaway probe ran against `Carve.Core.dll`.

All 23 round-1 outcomes hold, one only in part (the wall-error wording, below). Points checked in particular:

- **Unknown arguments:** every legal argument, optional ones included, is still accepted, and a call with no arguments object reads as no keys.
- **Registration:** `render`'s registration through `RenderTool.Create` loses no other tool, and the extension and loader tests pass.
- **`MeshEdits`:** the exhaustive-error change never accepts a mesh or pushes an undo step when any error exists.
- **The pump's target re-check:** it runs only on the frame thread, and it cannot fail a valid render.
- **The person's three decisions** are applied as decided.

### The extrude wall error can say "too small" when the distance is too large for a short edge

**Severity:** Nit

**What:** `MeshEdits.ToSourceFace` (`src/Carve.Core/MeshEdits.cs:135-147`) decides "small" or "large" by comparing `|distance|` with the longest side of the face's bounding box. That is the wrong measure. A wall is a strip, one face edge long (L) by the distance (d) wide. `HasArea` (`PolygonMesh.cs:265`) refuses it when L·d ≤ 1e-9·(L² + d²), which means d/L is beyond about 1e±9. So what matters is how d compares with that wall's own edge, not with the face's size.

**Failing input** (probe run on this build):

- Take the pentagon `(0,0,0) (1,0,0) (1,1,0) (1e-10,1,0) (0,1,0)`, which `Create` accepts. It has one edge of length 1e-10.
- `Extrude(mesh, [0], 0.5f)` returns `faces[0]: the distance 0.5 is too small for this face: its new wall faces[4], over the edge from corner 3 to corner 4, is refused: face has zero area`.
- Distances 0.001 and 0.05 succeed, so a larger distance makes it worse. The text tells the agent to do the opposite of what helps.
- With the edge at 1e-11, distance 0.05 gets the same wrong "too small".

**Fix:** for a wall error, compare `|value|` with the length of that wall's own edge, from corner k to corner k+1, rather than the face's bounding box. Keep "large" for a ring-vertex error, since that is a non-finite coordinate. Add a row with a short-edge face like the one above.

**Outcome:** —

## Summary

A re-review round of one Opus reviewer, at least as strong as both fixers (Opus and Sonnet). It checked every round-1 outcome against the fix commits and looked for regressions. It raised 1 Nit.
