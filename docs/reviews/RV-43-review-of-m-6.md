---
description: >-
  Review of M-6
---
# RV-43 — Review of M-6

**Status:** Open

**Reviews:** M-6

**Round:** 1

**Previous:** —

**Diff:** 21975a247d53c865a9edb1fc7682049fc9e74594 against origin/main

**Reviewer:** four reviewers, each claude-opus-5-5: the kernel and file format; projections and unwrap; the looks and export; the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the kernel and the file format (claude-opus-5-5)

Read against T-53's Plan, Rulings and As built, T-60's Plan and M-6's Decided at planning. The UV and file unit tests ran: 183 passed. Public-API probes ran from a scratch console project, now deleted. The probes found no code defects:

- **Seams through cuts:** a loop_cut across a seam leaves the halves as seams, and so does a plane_cut.
- **Bit-exact round trip:** -0, a subnormal, float max and 0.1/0.2 all come back bit-exact.
- **Read errors:** each is named, with no exception.
- **Lowest version:** an empty `uvs` list writes the lower version again.
- **Overlap:** a 37-triangle fan, also rotated and offset to about 1000, gives no false overlap on shared edges.

Version gating, the v2–v4 rewrites, holed-face corner order, subdivide's interpolation (double, rounded once), stencil sums and `CarriedUvs` all match the rulings. Both findings are test gaps.

### Splitting a seam in plane_cut, loop_cut or knife is untested
**Where:** `src/Carve.Core/FaceSplit.cs:588-590` (CarryUvs seam halves); `tests/Carve.Core.Tests/UvCarryTests.cs:120-165,204`
**Rubric:** Tests: a check that would still pass with the feature broken
Ruling 4 says a seam split by a cut keeps the mark on every piece. No test asserts it:
- The cut tests (`Plane_cut_both_…`, `Loop_cut_at_a_quarter…`, `Knife_…`) never look at `Seams`.
- The theory `Every_edit_leaves_uvs_that_fit_the_mesh` only runs `MeshUvs.Check`, which passes when the halves are missing.

For example, with `.WithSeams(halves, true)` deleted, the loop_cut case still passes: it cuts on (0,4), which splits the seam 2-6, and the seam simply vanishes. A grep of `Seams`/`IsSeam` across both test projects finds no seam assertion for any FaceSplit edit; the only seam-carry assertions are for subdivide, delete and extrude.

The code itself is correct (probed). The break could not be planted in a read-only review, so this was worked out from the test bodies.
**Outcome:** —

### The shared-edge overlap test never reaches the clipping code
**Where:** `tests/Carve.Core.Tests/MeshUvHealthTests.cs:41-47`; `src/Carve.Core/MeshUvHealth.cs:54`
**Rubric:** Tests: a check that would still pass with the feature broken
T-60's Plan asks for "a shared edge not counted as overlap". The test uses two axis-aligned rectangles that meet at u = 0.5, and the bounds filter `a.MaxU <= b.MinU` (0.5 <= 0.5) skips that pair before `Intersection` runs. A clip that counted a shared edge, or the points touching it, as area would still pass.

The other overlap tests (just over and just under 1e-6, the hole) do reach the clip, but none has a shared edge. The code is right today (the fan probe). The gap is that this case is not pinned; a shared diagonal edge, whose bounds overlap, would test it.
**Outcome:** —

## Findings: projections and unwrap (claude-opus-5-5)

Checked against Carve.Core from a scratch console app, now deleted. These were found sound:
- **Determinism:** union roots are order-independent, and dictionaries are used only for lookups or with sorted keys.
- **Pin ties:** they follow the rule.
- **LSCM:** sign and orientation are right (`v = -y` gives a negative shoelace), and the rotations have determinant 1.
- **Unwrap layout:** the real-area rescale, the margins and the fit are correct.
- **Holed faces:** the hole-loop allowance matches ruling 1.
- **Collinear vertices:** a pentagon with one unwraps correctly in all five rotations.
- **Undo:** each call is one undo step, and a refusal pushes nothing.
- **Worked examples:** they check out.

### A cube with one seam edge is refused as "no extent", which misleads the agent
**Where:** `src/Carve.Core/Unwrap.cs:152-155` (message at `:217-218`)
**Rubric:** errors and descriptions are content (they teach something wrong); a singular island
`Unwrap.Run` was run on `define_mesh`'s unit cube with one seam, `(0,1)`. The slit cube passes the disc test, since loops = 1 and χ = 1, and that is correct. But its only boundary vertices are the two pins, so the LSCM solution collapses (UV area 0).

The call is then refused with `faces[0]: the island of 6 faces has no extent: its corners all lie on one point or one line, so it cannot be flattened`. That is false for a 1 m cube; the real cause is too few seams, so an agent reading this is pointed the wrong way. With 3 seams (a path) the cube unwraps with stretch 7.8, and with 7 seams it unwraps cleanly.

The message, or the check, should tell an island with real 3D extent that collapsed ("mark more seams") apart from a truly degenerate one.
**Outcome:** —

### Unwrap's refusals write numbers in the machine's culture
**Where:** `src/Carve.Core/Unwrap.cs:49`, `:53`, `:68`; `src/Carve.Core/UnwrapLscm.cs:155`
**Rubric:** consistency with project rules (RV-24: refusals write numbers the same on every machine; see `RefusalCultureTests`)
These are plain `$"{x:R}"` interpolations with no `MeshEdits.Invariant`. Under de-DE:
- margin −0.5 gives `margin: -0,5 is not a margin into can take; ... (0,5)`;
- an `into` of `(0, 0, 0.5, 0)` gives `into: [0, 0, 0,5, 0] has no area`.

Separately, `UnwrapTool` widens `into` from float, so an `into` value of 0.1 is echoed as `0.10000000149011612`, where `project_uvs` echoes `0.1`.
**Outcome:** —

### project_uvs accepts a seam that Core then throws on
**Where:** `src/Carve.Server/ProjectUvsTool.cs:136-140` vs `src/Carve.Core/UvProjection.cs:96`
**Rubric:** a refused call is a named refusal; the tool and Core disagree
The two sides test "seam along the axis" differently:
- **The tool** tests in float: `|cross(n(axis), n(seam))| < 1e-6f`.
- **Core** tests in double: `|s - a(s·a)| < 1e-6`, and throws `ArgumentException`.

With `axis` [0,1,0] and `seam` [1e-6,1,0], the float cross is 1E-06, which is not below 1e-6f, so the tool accepts. Core then throws "seam lies along the axis" inside the `Rework` lambda; both steps were confirmed in the scratch app.

The model's state stays safe. What the agent sees was not confirmed end to end; it would be the MCP SDK's generic tool error instead of the named `seam:` refusal.
**Outcome:** —

### Ruled island cuts and two refusals have no test
**Where:** `src/Carve.Core/UnwrapIslands.cs:52`; `src/Carve.Core/UnwrapLscm.cs:152-156`; `tests/Carve.Core.Tests/UnwrapTests.cs`
**Rubric:** tests that would still pass with the feature broken
A search of the Unwrap tests for converge, IsManifold, mis-wound, "three or more" and "no extent" finds nothing, so all of these would still pass:
- dropping `!edge.IsManifold` (ruling 2: cut a mis-wound edge);
- dropping `edge.Faces.Count != 2` (cut a 3+-face edge);
- breaking the "no extent" refusal;
- breaking the "did not converge" refusal.

T-56's broken-product table covers none of them. The mis-wound behaviour itself was checked in the scratch app: two quads sharing edge 1-2 in the same direction come out as two islands, as ruled.
**Outcome:** —

### Cylindrical's shared arc/height scale is untested
**Where:** `tests/Carve.Core.Tests/UvProjectionTests.cs:101-140`
**Rubric:** tests that would still pass with the feature broken
T-55 rules that arc length and height share one scale, keeping aspect. The cylindrical tests check only:
- the seam column;
- u rising along the ring and v rising down the rings (ordering only);
- x reaching `into`'s left and right edges;
- y lying inside `into`.

If v were fitted to `into`'s height on its own, every assertion would still hold, including the given-`into` test, which would then fill [1, 3] exactly. Nothing compares the v span with the u span, for example (height / (2π·mean r)) × u span. This comes from reading the tests; the break was not planted.
**Outcome:** —

## Findings: the looks and export (claude-opus-5-5)

GPU resource lifetime is sound: the checker texture and the checker, layout and line materials are each made once, lazily, on the frame thread, and `LookHolder` destroys old meshes. No v-flip anywhere. Corner order matches across render (A, C, B), export (A, B, C from `Triangulator`) and the file. The no-UV export path keeps its bytes, pinned by hash. The join with T-51 is sound: `GameLimits` has no vertex limit for the UV vertex split to throw off. Nothing new writes to stdout.

### The checker description's worked example is a transposed (mirrored) mapping, and its "mirrored" case puts white at the bottom-left
**Where:** `src/Carve.Server/RenderTool.cs:228`
**Rubric:** 1 (Correctness: descriptions are content, T-57 Scope)
The text says: "a cube face given [[0,0],[1,0],[1,1],[0,1]] from its top-left corner round shows the 64 cells with white at its top-left; given [[1,0],[0,0],[0,1],[1,1]] it shows violet there and white at the top-right: mirrored."

Carve's faces wind counter-clockwise seen from outside (define_mesh's text), so "from its top-left corner round" means top-left, bottom-left, bottom-right, top-right. The first list therefore puts u = 1 at the bottom-left: u runs down the face and the hues run top to bottom. That is a transposed island, which is itself mirrored, yet the text presents it as correct. The second list puts (0,0) at the bottom-left, not the top-right.

The product's own probe agrees. `CheckerLookTests.FrontUvs` gives the +Z face [4,5,6,7] (bottom-left, bottom-right, top-right, top-left seen from +Z) the UVs [[0,1],[1,1],[1,0],[0,0]]; read from the top-left, that is [[0,0],[0,1],[1,1],[1,0]]. An agent copying the example gets mirrored UVs and is told they are right.

Checked by working the corner order by hand against the face loop and the probe's constants; not rendered.
**Outcome:** —

### Nothing checks that the uvs look's labels are drawn; the labels frame's sweep skips exactly where they are
**Where:** `scenarios/Carve.Scenario.Crate/CrateUvLayout.cs:47` (and `:134-135`); `src/Carve.Server/RenderSystem.cs:322-324`
**Rubric:** 2 (Tests cover the Scope: T-58 `labels: faces`, corner labels)
`crate-uvs-labels` runs only `Sweep`, which skips a 44 px box round each square corner and a ±22 x ±12 box round each face's label point, so it checks everything except the labels. The other tests don't reach the drawing either:
- `UvLayoutTests` tests `LabelLayout.UvLabels` and `UvLayout.FaceLabels` as pure functions with a fake measure.
- `UvLayoutLookTests:183` asserts only the caption and orange pixels away from the id.

Breaking the wiring in `DrawOverlay` would pass every check. Two examples: dropping the `spec.Mode != Look.Uvs` early-out condition (no corner labels on an unlabelled layout), or routing uvs through `LabelLayout.Plan`.

Found by grepping tests and scenarios for any pixel or overlay assertion on uvs labels; there is none. The person's visual approval in round 2 is the only cover.
**Outcome:** —

### Corner-label fit at 256 is shown only with a 12 px fake font height (not confirmed)
**Where:** `tests/Carve.Server.Tests/UvLayoutTests.cs:205`; `src/Carve.Server/LabelLayout.cs:153-155`
**Rubric:** 2
At 256 the margin is 16 px. Under the test's measure the label is 12 + 2 x `Padding`(2) = 16 px tall, so the "row stays off the square" assertion passes with no room to spare.

`UvLabels` clamps onto the image first. With a server font any taller than 12 px, the (0,0) and (1,0) labels would sit over the square's top rows at 256. No test uses the real `MeasureText`, and the scenario sweep skips those corners. The real font was not measured, so this is unconfirmed.
**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

Read across the members' interfaces, with `carve` built at 62a8724 and called over stdio from a scratch script. These agree, so they are not findings:
- **Sign and corner order:** the unflipped sign (negative shoelace) is the same in `UvArea`, `unwrap`, `project_uvs`, `check` and the layout look. Corner order (outer loop, then holes) is the same in set_uvs, render, the layout look, export and `check`.
- **"Unmapped":** it means live faces with no UVs, and the count agrees in `set_uvs`, `check`, the caption and the export warning.
- **Combined and tombstones:** `Combined` offsets UVs and seams correctly, and tombstoned faces are unmapped.
- **The joins with main:** the B-2 and T-51 joins are clean.
- **Architecture rules:** they hold.
- **The barrel:** its seams and ids agree across the `unwrap` example, `BarrelSpec` and `TestMeshes`.

### render's checker example has the winding backwards
**Where:** `src/Carve.Server/RenderTool.cs:228`
**Rubric:** 1 Correctness (a description contradicting behaviour)
The example says a face given `[[0,0],[1,0],[1,1],[0,1]]` "from its top-left corner round" shows the checker unmirrored, and that `[[1,0],[0,0],[0,1],[1,1]]` is mirrored. Corners go in loop order, counterclockwise seen from outside, so this is the wrong way round:
- **By hand:** the first list's shoelace sum is +2 (flipped) and the second's is −2 (unflipped).
- **On the product:** a cube, then `set_uvs` on face 1 with the first list, then `check`, gives `faces[1]: UVs flipped, so the texture is mirrored on the face`.
- **Against the other tools:** `set_uvs`'s own example (`SetUvsTool.cs:22-23`) goes `[[0,0],[0,1],[1,1],[1,0]]`, the opposite direction, and so does `CheckerLookTests.FrontUvs`.

An agent copying render's "good" example gets a mirrored face that `check` flags. The text comes from T-57's planned wording (`docs/tasks/T-57-checker-look.md:89`).
**Outcome:** —

### set_uvs throws on a negative face id
**Where:** `src/Carve.Server/SetUvsTool.cs:95-99`, `src/Carve.Core/MeshUvs.cs:59`
**Rubric:** 1 Correctness (error path)
`TryIndex` accepts −1, and `set_uvs` never checks for it:
- `faces:[{"face":-1,...}]` reaches `MeshUvs.WithFaces`, which throws `ArgumentOutOfRangeException`.
- `clear:[-1]` indexes `m.Faces[-1]`.

On the product, both return only `An error occurred invoking 'set_uvs'.` with no element named, although the description promises a named error. `project_uvs` and `unwrap` check `f < 0` and answer `faces[-1]: face -1 is not a face of the mesh, which has 6`.
**Outcome:** —

### server suites miss a linked scenario file
**Where:** `tests.json:31-41` (server-unit), `tests.json:47-58` (server-regression); `tests/Carve.Server.Tests/Carve.Server.Tests.csproj:41`
**Rubric:** 6 Tests are placed (suite inputs)
`Carve.Server.Tests` now links `scenarios/Carve.Scenario.Crate/CheckerColours.cs`, which `CheckerTextureTests` (unit), `CheckerLookTests` and `CheckerArgumentTests` (regression) use. cairn's `dotnet-closure` follows only `ProjectReference`s, which is why `CameraPixels.cs` is listed by hand in both suites. `CheckerColours.cs` is listed in neither, so a change to it does not re-run either suite. The head commit 62a8724 changes exactly this file.
**Outcome:** —

### CLAUDE.md's samples row leaves out barrel.carve
**Where:** `CLAUDE.md:88`
**Rubric:** 7 Tracker hygiene (CLAUDE.md rows match what was built)
The `samples/` row lists every sample with its file version and what keeps it equal by content, except `samples/barrel.carve`, the milestone's sample. That file:
- is version 5;
- is kept equal to `TestMeshes.SeamedBarrel` unwrapped by `UnwrapSampleTests`;
- is compared by scenario-uvs with the barrel it builds.
**Outcome:** —

### Face and `into` errors are worded differently in each UV tool
**Where:** `src/Carve.Server/SetUvsTool.cs:95-96`, `src/Carve.Server/ProjectUvsTool.cs:78,162`, `src/Carve.Server/UnwrapTool.cs:93`, `src/Carve.Core/Unwrap.cs:209`, `src/Carve.Core/MeshUvs.cs:102`
**Rubric:** 1 Correctness (one tool surface an agent learns from)
The same check is written once per tool, each with its own wording (all from the stdio probe):
- **A deleted face** gets four messages:
  - `clear[f]: face f is deleted` (set_uvs `clear`);
  - `faces[f]: face f is deleted, and a deleted face holds no UVs` (set_uvs `faces`);
  - `faces[f]: face f is deleted, so it cannot be mapped` (project_uvs);
  - `faces[f]: face f is deleted` (unwrap).
- **A three-number `into`:**
  - project_uvs says `into: expected 4 numbers [u0, v0, u1, v1], got 3 element(s)`;
  - unwrap says `into: expected [u0, v0, u1, v1], four numbers, got an array`, which doesn't say what is wrong.
- **An `into` with no area** is worded differently by the two tools.
- **Where validation lives:** face validation is in Core for `unwrap` but in Server for `set_uvs` and `project_uvs`.
**Outcome:** —

### mark_edges says marks are "drawn in export", now true only of hard edges
**Where:** `src/Carve.Server/ShadingTools.cs:59`
**Rubric:** 1 Correctness (description vs behaviour)
"Marks" in this sentence now covers seams, but a seam does nothing to export: export splits vertices only where UVs or normals differ, and M-6's rule is that a seam never changes geometry or shading. The same sentence says marks are "kept ... through move and extrude", although they are now also carried through subdivide and M-5's cuts.
**Outcome:** —

## Summary

Round 1 of the review of M-6 (UVs), at head `62a8724`, rebased onto `origin/main` `fb96f44` (B-2, T-51), after the refactor pass. The diff is 96 files and about 8.4k lines.

**The split.** The diff was split by area, because the members built separate pieces with a few shared interfaces. It went to four reviewers, all on claude-opus-5-5, as strong as the implementers of the riskiest parts:

- **The kernel and the file format** (T-53, T-60): `MeshUvs`, version 5, carrying UVs and seams through every edit, check's UV warnings.
- **Projections and unwrap** (T-54, T-55, T-56): `UvProjection`, LSCM, CG, the packer, and the UV tools.
- **The looks and export** (T-57, T-58, T-59, T-61): `checker`, `uvs`, `Model.Combined`, `TEXCOORD_0`, the scenario checks, and the join with T-51.
- **One pass over the whole diff**: interfaces between parts, the tool surface as one thing an agent learns from, `tests.json`, `CLAUDE.md`, the architecture rules, and the rebase joins.

Each reviewer probed with a scratch build outside the repo; none edited it.

**What the reviewers found:**

- **Sound:** the file format (bit-exact, every older version unchanged), the carrying rules, determinism, LSCM orientation, GPU resource lifetime, the v axis, corner order across file, render and export, and the joins with main.
- **The findings are concentrated in:**
  - tool descriptions that teach something wrong (the checker example, found by two reviewers independently; mark_edges);
  - error paths (set_uvs on a negative id; project_uvs's seam test; unwrap's culture and "no extent" wording);
  - one inconsistent error vocabulary across the UV tools;
  - tests that would pass with a ruled behaviour broken.
