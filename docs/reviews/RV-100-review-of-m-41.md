---
description: >-
  Review of M-41
---
# RV-100 — Review of M-41

**Status:** Answered

**Reviews:** M-41

**Round:** 1

**Previous:** —

**Diff:** c049d19c71ab01489a2480ccfabad0d3033318d2 against origin/main

**Reviewer:** claude-opus-5-5 (Core geometry), claude-opus-5-5 (the whole diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Core geometry (claude-opus-5-5)

### A very small `max_edge` never returns

`src/Carve.Core/Tessellation.cs:56` and `:152-153`. Rubric point 1 (edge cases).

The only bound on `max_edge` is that it must be above 0, and nothing stops the loops once the spacing is too small for a double to represent:

- The edge's piece count, `(int)Math.Ceiling(length / maxEdge - 1e-9)`, saturates at `int.MaxValue`, so the edge loop tries to add about 2^31 points.
- The lattice step `x += maxEdge` stops advancing once `maxEdge` is under half an ulp of `x`.

Failing case: a unit quad at (1000..1001, 1000..1001) with `Tessellate(mesh, 1e-14)` was still running after 15 s. "No cap on how fine" in the Scope does not ask for a call that never ends.

**Outcome:** fixed

 (the person's ruling of 2026-11-27, budget and faster filter): `Tessellation.Predicted` is worked before any cut and a call over 200,000 predicted new vertices is refused, so this case is refused at once; tested by `A_call_predicted_over_the_budget_is_refused_at_once`. In 2a5d164d.

### Run time grows as N^1.5 at fine `max_edge`, with no budget

`src/Carve.Core/Tessellation.cs:156`. Rubric point 1 (load).

Each lattice candidate runs `PlaneGeometry.Inside` over every loop vertex, projecting each one again, and then `SegmentDistance` against every loop segment. Measured on a 1 x 1 quad in Release:

| `max_edge` | Vertices | Time |
|---|---|---|
| 0.02 | 3.6k | 0.56 s |
| 0.01 | 13k | 3.3 s |
| 0.005 | 49k | 23 s |
| 0.0035 | 98k | 67 s |

The triangulation itself took about 1.5 s of the 23 s case; the rest is this filter. Extrapolated, 1 mm on a 1 m face runs for tens of minutes inside the store's lock.

**Outcome:** fixed

 (the person's ruling of 2026-11-27, budget and faster filter): `Tessellation.Lattice` works row crossings once and buckets segments in a grid; the unit quad at 0.0025 (191,085 vertices) went from 9.7 s to 3.1 s in Release, with the same points (an equivalence test against the old rule; existing tests unchanged). In 2a5d164d.

### Border cases with no test: an unselected face with a hole, and UVs across a seam

`src/Carve.Core/Tessellation.cs:75` and `:110-111`, `tests/Carve.Core.Tests/TessellateTests.cs`. Rubric point 2.

- **An unselected face's hole loop on the border.** No test has one. Line 75 is reached only through the selected faces' own holes, so dropping hole insertion for unselected faces would pass every test. A probe shows the code is right today.
- **UVs across a seam.** No test checks that an unselected neighbour across a UV seam takes its border-point UVs from its own corners.

**Outcome:** fixed

`An_unselected_faces_hole_on_the_border_takes_the_border_points` (the dais's bottom face round a peg: hole gains 8 points, outer loop unchanged, closed) and `Across_a_seam_the_unselected_neighbour_takes_its_border_uvs_from_its_own_corners`. In 2a5d164d.

### `pin_border`'s "nothing left off the border" refusal has no test, and the refusal tests do not name the error

`src/Carve.Core/Displace.cs:129`, `tests/Carve.Core.Tests/DisplaceTests.cs` (`Pin_border_with_vertices_or_no_selection_is_refused_and_changes_nothing`). Rubric point 2. A Nit.

- **Untested refusal.** A selection that is all border (for example one untessellated cube face) is refused, and no test covers it.
- **Weak asserts.** The three refusal asserts check only `Succeeded == false`. They would still pass if the call were refused for some other reason.

**Outcome:** fixed

`Pin_border_on_a_selection_that_is_all_border_is_refused`, and the three refusal asserts check an error on `pin_border` saying "has no border". In 2a5d164d.

## Findings: the whole diff (claude-opus-5-5)

### tessellate's `split` lists every piece id at summary, undoing the `created` blocks

`src/Carve.Server/TessellateTool.cs:77`, described at `:24` as `split:[{face, pieces}]`. Rubric point 1 (M-23's ruling 5, a budget of 2,000 characters at `summary`).

On the unit cube, `{"max_edge":0.05,"faces":[3]}` at `summary` answered 5,413 characters, 1,250 ids of them in `pieces`. `subdivide` answers `split:[]`. The Scope says "reported in `split`, as other edits do".

**Outcome:** fixed

 (the person's ruling of 2026-11-27, `split` is short at summary): each face's `pieces` is `{count, first}` at summary through `Moved`, whole at full; description and tests updated. In 2a5d164d.

### VerbosityToolTests: the "full has more warnings" assertion became an `if`

`tests/Carve.Server.Tests/VerbosityToolTests.cs:146-148`. Rubric point 2 (a test weakened).

Before, any row whose `full` text differed from its `summary` text had to show more warnings at `full`. Now that check runs only when `full` happens to have more. Failing case: `delete` dropping its warnings to `[]` at `full` would pass. The new `moved` check also compares only `count` with the full list's length, never `first` with the first 10 ids.

**Outcome:** fixed

The unconditional "full shows more warnings" check is back for any row that differs in anything but `moved` and `split`, and `first` is checked against the first 10 ids of the full list for `moved` and each `split` pieces (tessellate's row now cuts more than 10 pieces). In 2a5d164d.

### The `verbosity` schema text and README's verbosity list don't match the new `moved`

Rubric points 1 and 7. A Nit.

- `src/Carve.Server/MeshToolSupport.cs:164-167` (`VerbositySchemaText`) still says `full` is "the same, with every warning one per line".
- `README.md:130-136`: the list of tools that take `verbosity` leaves out `displace`, which the new `moved` line names. This predates M-41.

**Outcome:** fixed

`VerbositySchemaText` says long id lists are short at summary and whole at full; README lists `displace` among the tools that take `verbosity` and names `split` beside `moved`. In 2a5d164d.

### The tessellate description's error list misses two refusals

`src/Carve.Server/TessellateTool.cs:25`. Rubric point 1. A Nit.

The Core also refuses an empty `faces: []` (`Tessellation.cs:39`) and a face that "cannot be tessellated" (`Tessellation.cs:90`). Neither is in the description's list of errors, and neither is in `Bad_input_is_a_tool_error_that_changes_nothing`.

**Outcome:** fixed

The description lists the empty `faces []`, "cannot be tessellated" and the budget; the test has the empty list and the budget. "cannot be tessellated" is not tested: no face `PolygonMesh.Create` accepts was found that triggers it. In 2a5d164d.

### No upper bound on how fine `max_edge` can go (unsure; the Scope asked for this)

`src/Carve.Core/Tessellation.cs:34-62`, `TessellateTool.cs:16`. Rubric point 1.

At 0.05 one unit face becomes 1,250 triangles; at 1e-4 about 10^8, inside the store's lock. Raised for the person: a guard on the expected piece count would turn it into an error that changes nothing. This is the same problem as the Core reviewer's first two findings.

**Outcome:** fixed

 (the person's ruling of 2026-11-27, budget and faster filter): the guard asked for, as the first finding. In 2a5d164d.

## Summary

Two reviewers ran in parallel, both claude-opus-5-5:

- **Core geometry:** `ConstrainedDelaunay.cs`, `Tessellation.cs`, the `pin_border` change and their tests, read whole. The reviewer probed them from a scratch console app on the built `Carve.Core.dll`. These held: holes close to the outer loop and to each other, concave notches, collinear and co-circular loops, slivers, faces far from the origin, a cube tessellated twice staying closed, area conserved, determinism. It did not test skin-weight carry-over or run the suites.
- **The whole diff:** the server tools, `TessellateStage` and the Cuts and Shed edits, the server tests, README, CLAUDE.md, the As built sections, the retrospective and B-14. It ran 115 server tests and 22 Core tests, and measured the descriptions from `tools/list` (tessellate 2,512, displace 3,612, move 2,383 characters). The only loosened check is the person's 3/4-coverage ruling. It did not re-derive the triangulation or run the scenarios.
