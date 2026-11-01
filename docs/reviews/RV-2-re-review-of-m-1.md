---
description: >-
  Re-review of M-1
---
# RV-2 — Re-review of M-1

**Status:** Answered

**Reviews:** M-1

**Round:** 2

**Previous:** RV-1

**Diff:** 246360b897c78a6cfe46adceff20462f42655b31 against origin/main

**Reviewer:** claude-opus-5-5 (two reviewers)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: kernel and define_mesh fixes (claude-opus-5-5)

### Meshes the kernel now accepts at extreme scale render blank as a success, or fail on a raw NaN

`src/Carve.Core/PolygonMesh.cs:139-156,179-184` against `src/Carve.Server/ViewFraming.cs:43-52,72-77`, `src/Carve.Server/RenderTool.cs:21`. Correctness: a regression from the scale fix, reported by both re-reviewers. The kernel moved to double, but framing is single precision. The worked cube scaled by s, then `render`, on the real server: s=1 to 1e18 and 1e-10, 1e-14 draw the same image; s=1e19, 1e-15, 1e-20, 1e-30 succeed with the normal caption and 0 foreground pixels; s=1e20, 1e30 give `The render failed: The front view could not be set up: nearPlaneDistance ('NaN') must be greater than '0'`. At 8cbd23f such faces were refused, so an error became a false success.

**Outcome:** fixed

### A folded outline is still accepted, and a real vertex is still dropped from the triangles

`src/Carve.Core/Triangulator.cs:88-97,125`. Correctness; RV-1's self-intersection and omitted-vertex findings answered only in part. `FindCrossing` skips adjacent edges and ignores overlaps, and the no-ear fallback removes a flat vertex even where the outline turns back 180 degrees. `{"vertices":[[2,4,0],[1,4,0],[5,4,0],[1,0,0],[0,4,0]],"faces":[[0,1,2,3,4]]}`: edges 0-1 and 1-2 fold back along y=4; it is accepted, the triangles are `(1,2,3),(1,3,4)`, and vertex 0 is in none. `MeshReport` still counts it as used.

**Outcome:** fixed

### The clipping error runs first, so a bowtie never gets the named crossing

`src/Carve.Core/PolygonMesh.cs:113-120`, `tests/Carve.Core.Tests/MeshValidationTests.cs:63-70`. Correctness. `FindCrossing` runs only after clipping succeeds, so `(0,0),(2,2),(2,0),(0,1)`, whose edges 0 and 2 cross, gets `face cannot be triangulated (self-intersecting or degenerate outline)`, while the double-wound hexagon gets the message naming both edges. The test pins the vaguer text.

**Outcome:** fixed

### The touching-outline test never reaches FindCrossing

`tests/Carve.Core.Tests/KernelReviewTests.cs:59-66`. Tests cover the Scope. That outline fails clipping, so "crosses" can never appear. Changing both `< 0` tests at `Triangulator.cs:132` to `<= 0` leaves all 51 Core tests green. A keyhole outline reaches the check, is accepted today (8 triangles, 10 of 10 vertices) and has no test.

**Outcome:** fixed

### The error count still reports one missing argument as several problems

`src/Carve.Server/DefineMeshTool.cs:93-114`. Correctness. `ArrayOrError` does not add to `shapeBad`, and `PolygonMesh.Create` runs on the empty lists. `{"vertices":[[0,0,0],[1,0,0],[0,1,0]]}` gives 2 problems, `{}` gives 3, `{"faces":[[0,1,2]]}` gives 4 including false out-of-range lines, `"faces":{}` gives 2. The `BadArguments` rows do not check the count.

**Outcome:** fixed

### A non-integer index that rounds to a whole double is accepted

`src/Carve.Server/DefineMeshTool.cs:217`. Correctness. `TryGetDouble` rounds before the `d != Math.Floor(d)` check. With 3 vertices, `[[0,1.00000000000000001,2]]` is accepted as index 1, `[[1e-400,1,2]]` as index 0, and `[[0,1.5e-400,2]]` gives `index 0 repeats faces[0][0]`.

**Outcome:** fixed

### "Lists every problem" is still untrue for one face with two faults

`src/Carve.Core/PolygonMesh.cs:75-79,90`, promise at `src/Carve.Server/DefineMeshTool.cs:31-32`. Correctness. `"faces":[[0,9]]` reports only the length, `[[0,0,9]]` and `[[0,0,1.5]]` only `faces[0][2]`; the hidden problem appears on the next call. The description's list of bad input also omits self-intersection and non-finite coordinates.

**Outcome:** fixed

## Findings: render, harness and docs fixes (claude-opus-5-5)

### A mesh a few float steps wide, far out, is cropped or cut with no error, and just below that it is one pixel

`src/Carve.Server/ViewFraming.cs:50,72`, comment at `:47-48`, `src/Carve.Server/RenderTool.cs:18`, `docs/tasks/T-4-shaded-render-tool.md:74`. Correctness; tests cover the Scope. RV-1's framing finding, moved rather than closed. `eye` rounds to float, so the view direction stops matching the corners framing computed. Unit cube at (o,o,o) on the real `carve`: fine at 1e6; 3e6 back draws 24,778 and touches the edge; 4e6 cropped at two edges in both views; 7e6 back x 359..511; 8e6 is 2 pixels; 1e7 is 1 pixel. The same ratio occurs close in: a box of side 4e-5 at (100,100,100) reaches 1.07 in device units. Tests stop at 1e6.

**Outcome:** fixed

### The 1e-15 constant blanks a good mesh, and its stated reason is false

`src/Carve.Server/ViewFraming.cs:28-29,50`, `docs/tasks/T-4-shaded-render-tool.md:73`. Correctness; CLAUDE.md "Ask, don't land". A cube of side 1.2e-15 renders in full; sides 1.1e-15, 1e-15, 1e-16, 1e-20 are accepted and give 0 pixels with no error. The comment says look-at lengths underflow below it; with `SmallestRadius = 0` the matrices stay exact down to side 1e-22.

**Outcome:** fixed

### A steady stream of define_mesh keeps a queued render from ever being drawn

`src/Carve.Server/RenderPump.cs:41-44`, `src/Carve.Server/RenderTool.cs:54`. Correctness. Every version change ends the `Step` before any request is served. A `render`, then `define_mesh` calls in a loop (470 in 5 s against 20 frames a second): the render was not answered during the 5 s and returned 117 ms after the loop stopped. No bound on the wait, and no test.

**Outcome:** fixed

### Reordering after each hook throws away a value an extension set directly

`src/Carve.Server/Program.cs:57-60`, `src/Carve.Extensibility/ICarveExtension.cs:13-15`. Correctness. `Sources.Remove` rebuilds every provider, so `context.Configuration["K"] = "v"` is dropped when that hook returns: hook 1 read `set`, hook 2 read empty, while `AddInMemoryCollection` survived. Reproduced in a scratch copy of the sequence.

**Outcome:** fixed

### The CheckDrawn floor passes an L-block with all its side faces missing

`scenarios/Carve.Scenario.Cube/CubeScenario.cs:32`. Tests cover the Scope. One floor of 14,000 is set by the long box (28,563), while the L-block draws 88,829 and 94,688. With `MeshBridge`'s L-block cut to its two caps, frames dropped to 49,732 and 59,798 and the scenario passed. A per-frame floor is a tightening.

**Outcome:** fixed

## Summary

Round 2 of M-1, two reviewers on claude-opus-5-5, checking RV-1's answers and the fixes (`git diff 8cbd23f HEAD`), split into the kernel and `define_mesh`, and everything else. Every RV-1 answer held except the framing finding, which moved. The extreme-scale blank frame was reported by both reviewers and is kept once, so 13 reports make 12 findings.

The person decided the framing rule, since the finding survived two rounds and the spec set no range: `render` checks that the mesh can be framed and otherwise returns a tool error telling the agent to recentre or scale, with no coordinate range and no normalising. That decision answers the three framing findings together.
