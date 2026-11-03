---
description: >-
  A render target typed as the mesh bound (1.02) is refused as outside the bounds because the stored float max is 1.0199999 and RenderTool compares the target as a double
---
# B-1 — render refuses a target on the mesh's own float bound

**Status:** Fixed

**Severity:** Major

**Milestone:** M-2

**Verify:** server-regression, scenario-crate, scenario-cube

**Perf:** skip: Carve declares no heavy suites

## Symptom

Found by T-12. After the crate is built, its bounds are reported as `(-0.02, 0, 0)` to `(1.02, 0.8, 1)`. Even so, `render(direction: [2, 1, 1], zoom: 3, target: [1.02, 0.4, 0.5])`, the target the person agreed for the zoom view, is refused as a tool error: the target is outside the mesh's bounds. The crate scenario uses `1.0199` as a stand-in for now, which the person approved in T-12's review.

## Cause

The stored mesh is `float`. The crate's max X is the float nearest 1.02, which is 1.0199999. `RenderTool.cs` compares the target, read from JSON as a `double`, against that bound in double precision, so 1.02 > 1.0199999 and the target is refused. The bounds check is inclusive by spec (T-9), so a target typed as the mesh's own reported bound must be accepted.

## Fix

Compare the target in the mesh's own precision: convert each target component to `float` before the inclusive bounds test, the way the mesh's own coordinates were stored. **Add no tolerance or epsilon.** A tolerance is an open rule for the person, and if a float comparison is not enough, stop and ask. Then:

- A test through the client: on the crate (or any mesh whose bound is not exact in float), a target equal to the reported bound is accepted, and one clearly beyond it is still refused.
- Show the test failing on the old comparison.
- Put the crate scenario's zoom target back to `[1.02, 0.4, 0.5]` and remove the stand-in comment. The zoom frame's bytes must be compared with the 1.0199 run, and any difference reported. A difference in that frame is expected to be tiny or none, but it is not to be hidden.
- The cube scenario's frames stay byte-identical.

**Open rules: stop and ask.** Any tolerance. Any change to which targets count as inside other than this precision fix. Any change to another frame's bytes.

### As done

Done by Claude Sonnet 5.5 (`claude-sonnet-5-5`). `RenderTool` now converts each target component to `float` before the inclusive bounds test; no tolerance or epsilon. New client test `A_target_on_the_reported_bound_of_a_mesh_inexact_in_float_is_accepted_and_one_beyond_is_refused` (mesh with x = 1.02; target 1.02 accepted, 1.021 refused), shown failing on the old double comparison (`Assert.False` failure) and passing after. The crate scenario's zoom target is back to `[1.02, 0.4, 0.5]`, stand-in comment removed.

## Verified by

Run on Claude Sonnet 5.5 (`claude-sonnet-5-5`).

- `cairn bug B-1 verify`: server-unit, server-regression, scenario-cube, scenario-crate pass.
- Crate zoom frame (`crate-dir-2-1-1-zoom3`) differs by hash from the 1.0199 run's frame (the copy in `artifacts/review/T-12/round-1/`, T-12's artifacts folder being gone): 430 of 262144 pixels differ, max channel difference 62. The person approved the new frame via a Waystone review (old and new in `artifacts/review/B-1/round-1/`). The other eight crate frames are identical by hash.
- Cube: the eight frames of the earlier baseline (`main/artifacts/scenarios/cube/1`) are identical by hash. The four T-9 frames added since have no saved baseline, so they were not compared by hash.
