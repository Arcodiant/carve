---
description: >-
  Review of M-24
---
# RV-74 — Review of M-24

**Status:** Open

**Reviews:** M-24

**Round:** 1

**Previous:** —

**Diff:** 5b90bd3710be394d797837f5ab95eebff3ffa11f against 7d7752d

**Reviewer:** claude-opus-5-5, three reviewers: the Core baking kernel, the server and rendering, the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the Core baking kernel (claude-opus-5-5)

### NearEdges calls a convex edge concave when the other face is non-convex, so curvature reads it dark instead of light
- **Where:** src/Carve.Core/Baking/BakeEdges.cs:126-130 (rubric 1, 2)
- **What is wrong:** Convex or concave is decided by which side of this face's plane the *mean of the other face's outer loop* lies on. That holds only when the other face lies wholly on one side of the shared edge's line, which is true for a convex polygon. For a non-convex face, such as the L-shaped cap of any extruded L, U or T profile, the loop mean can sit beyond the edge line, and the answer flips. A throwaway probe test (written, run and deleted) built an L-block with one long arm (profile (0,0) (3,0) (3,1) (1,1) (1,2) (0,2), extruded 1). It is celled like `BakeLayoutTests.Celled` and laid out at 128. On the inner wall x = 1, both of its edges with the caps, (3,4) and (9,10), came back `Concave`. Both are 90° convex box edges. `Bake.Curvature` (width 0.2) read **0** at that wall's texel nearest the top edge, where it should read 1. `TestMeshes.LBlock` has equal arms, so the cap's loop mean falls exactly on the reflex corner (side = 0, which falls through to `Convex`). That is why `On_an_l_block_the_inner_edge_is_concave` and the T-126 L-block stage both pass by coincidence.
- **Consequence:** The curvature map (and any M-29 node built on `NearEdge.Shape`) inverts wear on convex edges next to non-convex faces. Such faces are common in Carve after extrusion. The wrench escapes only because every face next to its hard edges happens to put its mean on the right side. Fix: classify from the edge direction in this face's winding and the two normals (for example the sign of `dot(cross(n1, n2), edgeDir)`), and add an asymmetric L to the test.
- **Severity:** Major

**Outcome:** —

### A texel centre on an island's bottom or right boundary is recorded as Clamp, not Centre
- **Where:** src/Carve.Core/Baking/BakeRaster.cs:41,66-70 (rubric 1)
- **What is wrong:** Pass 1 applies the top-left rule to every edge, including an island's outer boundary. A centre lying exactly on a bottom or right boundary edge is held by no triangle, so pass 2 takes it with d² = 0 and flags it `Clamp`. T-120's rule is that the clamp is for a centre "outside every triangle", and that the result "records which texels were covered by their centres and which by the clamp". The probe (deleted) laid a quad whose right edge is u = 20.5/64. `CoverageAt(20, 15)` was `Clamp`. The weights and position are right; only the label and `ClampedCount` are wrong.
- **Consequence:** `ClampedCount` and `BakeCoverage` overstate the clamp on layouts with edges on centre lines. Nothing baked changes today, but M-29 or a stat that reads coverage would see it.
- **Severity:** Nit

**Outcome:** —

### The clamp's "nearer face wins, tie to the lower face" rule has no test
- **Where:** tests/Carve.Core.Tests/BakeLayoutTests.cs:73-117 (rubric 2)
- **What is wrong:** T-120's coverage bullet says a texel within reach of two faces takes the nearer, and a tie goes to the lower face id. The clamp tests (`An_inset_quad_...`, `A_sliver_face_...`) each have one face in reach. Nothing fails if pass 2 kept the first or the last triangle it met, or compared `<=` instead of `<` (BakeRaster.cs:72). A probe with faces 5 and 1 equidistant from column 10 got face 1, so the code is right; only the test is missing.
- **Consequence:** A Scope rule that T-120's Notes name as a stop is unguarded against regression.
- **Severity:** Minor

**Outcome:** —

### Pad's cost is size² × padding, so padding near size/2 at 4096 runs for about a minute per map
- **Where:** src/Carve.Core/Baking/BakePadding.cs:56-77 (rubric 1)
- **What is wrong:** The row pass scans dx from 0 up to `texels` for every uncovered texel. It stops early only once dx² passes the best distance found, so texels far from any island scan the whole range. Measured with a throwaway probe (deleted), on 16 cores in Debug, with one small island: 1024 at padding 512 took 1.0 s, and 2048 at padding 1024 took 6.9 s. That is roughly cubic, so 4096 at 2048 comes to about 50 s for each padded map. `bake` accepts padding up to size/2 and pads every map and every mask.
- **Consequence:** A legal `bake` call at 4096 with large padding (not verified, per the retrospective) runs for minutes, far past the tool's answer window. That crosses into the server reviewer's part. Either a two-pass exact distance transform (linear in size² per pass), or a lower cap on padding, would remove it.
- **Severity:** Minor

**Outcome:** —

### WrenchBevelSeamTests is tagged unit but takes about 4 s
- **Where:** tests/Carve.Core.Tests/WrenchBevelSeamTests.cs:14-17 (rubric 6)
- **What is wrong:** The class carries `Tier=unit`, but it bakes the wrench's full bevel map at 1024 twice (the good width and the thin one), which measured 4 s. CLAUDE.md says `unit` means about 1 s or less per test. The person's ruling placed the seam check as a Core test. It did not set the tier or the size.
- **Consequence:** `core-unit` is slower than its tier promises. It is the largest single cost among the 51 bake tests (7 s in total).
- **Severity:** Nit

**Outcome:** —

## Findings: the server and rendering (claude-opus-5-5)

### A bad folder is not reported with the call's other problems, and when it is reported it comes once per file, in another tool's words
- **Where:** src/Carve.Server/BakeTool.cs:197 and :229-237 (rubric 1)
- **What is wrong:** `bake` returns at line 197 with the argument errors before it resolves any output path. So the folder's path problems (lines 231-232) and the case clash (235-236) are only found on a call with nothing else wrong. When they are found, `ToolPaths.Resolve` runs once per job, so one bad folder gives one error per file. Each error is worded as a path the agent never gave, with a `bake:` prefix inside the list, not as `folder: ...`. Shown with a throwaway probe test (deleted). On the strip with group `g`, `{"size":64,"folder":"a*b","surface":{"g":{"roughness":2}}}` gives `bake failed with 1 problem(s)`, the roughness alone. `{"size":64,"folder":"a*b"}` then gives 2: `bake: path "a*b/strip.mask.g.png" holds the character '*'...` and the same for `strip.base_colour.png`.
- **Consequence:** this breaks T-122's "Errors, all at once" and the description's "Errors, all listed at once". An agent fixes one problem, calls again and meets the next. On the wrench's default maps (six masks and base colour), one bad character becomes seven near-identical lines about file paths, where the problem is the `folder` argument.
- **Severity:** Minor

**Outcome:** —

### A surface entry naming a model material the mesh does not use is accepted silently
- **Where:** src/Carve.Server/BakeTool.cs:183-185 (rubric 1)
- **What is wrong:** `known` adds every material of the model (`model.Materials`), not only those the mesh's faces use. An entry naming a defined but unapplied material passes validation, covers no face, and gets no warning. The description (line 62) lists as an error "a surface entry naming a group or material the mesh does not have". Probe (deleted): `define_material {name:"unused"}` then `bake {maps:["base_colour"], surface:{"unused":{base_colour:"#000000"}}}` succeeds with `warnings: []`, and the map is all white.
- **Consequence:** an agent that defines a material and bakes before `set_material` gets a white base colour and no hint why. The tool's description says this case is refused.
- **Severity:** Minor

**Outcome:** —

### A failed write part way through leaves earlier files written, and the error does not say so
- **Where:** src/Carve.Server/BakeTool.cs:241-247 (rubric 1)
- **What is wrong:** files are written one at a time. If `ToolPaths.Write` fails on file k (a PNG held open by a viewer on Windows, a full disk), the tool returns `Error(writeError)` and drops the results for files 0..k-1. Those files are already written, or already replaced over the previous bake. The description says each error "writes nothing". This one is not in its list, but nothing tells the agent that some maps changed. Found by reading the code; no probe.
- **Consequence:** after a failed `bake` the folder holds a mix of new and old maps (say a new base colour with last call's ORM), and the agent cannot tell which.
- **Severity:** Minor

**Outcome:** —

### The description says `path` is absolute when the folder is, but an absolute folder comes back relative
- **Where:** src/Carve.Server/BakeTool.cs:59 and :512-516 (rubric 1)
- **What is wrong:** `ReportedPath` returns `Path.GetRelativePath(working, full)` unless the result is rooted, and on the same drive it never is. Probe (deleted): folder `<working>/abs` (absolute) came back as `abs/strip.base_colour.png`. An absolute folder outside the working directory came back as `../carve-probe-out-d3a74d/strip.base_colour.png`. The description says "(absolute when folder is)".
- **Consequence:** `define_material` still resolves the returned path to the same file, so nothing breaks. But the agent is told one form and gets another, including `..` paths it did not write.
- **Severity:** Nit

**Outcome:** —

### `image` with a non-`uvs` mode returns alone, before the call's other problems
- **Where:** src/Carve.Server/RenderTool.cs:317-320 (rubric 1)
- **What is wrong:** with a mode other than `uvs`, `image` returns an immediate `Error` before the shaded/checker path checks its other arguments. So `{"image":"a.png","view":"nope"}` reports only the image. In `uvs` mode the image problem is added to `problems` and listed with the rest (`UvImageArgumentTests.The_image_problem_is_reported_with_the_calls_other_problems`), but not here.
- **Consequence:** a second round trip for a mistyped call. This is minor, since the image refusal is the likely root mistake.
- **Severity:** Nit

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### Suite scenario-bakes does not list ColourRule.cs among its inputs, though the scenario links it
- **Where:** tests.json:645-669 (inputs of `scenario-bakes`); scenarios/Carve.Scenario.Bakes/Carve.Scenario.Bakes.csproj:21; CLAUDE.md:91 (the Bakes row's list of linked files) (rubric 7, 4)
- **What is wrong:** T-125 (commit 9873ab4) added `<Compile Include="../Carve.Scenario.Crate/ColourRule.cs" Link="ColourRule.cs" />` to the Bakes project, and `BevelStage.CheckStrip` uses `ColourRule.Lit`/`Encode` to predict the strip's shade (BevelStage.cs:263-264). The suite's inputs list `UvsScene.cs`, `CrateBuild.cs`, `CameraPixels.cs`, `Png.cs` and `ComparisonPng.cs` but not `scenarios/Carve.Scenario.Crate/ColourRule.cs`. A `dotnet-closure` of the Bakes project takes the project and its references, not linked source, which is why every other linked file is listed by hand (ten other suites list `ColourRule.cs`). T-123's Notes require each later member to "add any new inputs to the suite", and T-123's As built says the suite has "every linked file". The CLAUDE.md row's list of linked files ("with the Uvs scenario's `UvsScene.cs`, the Crate's `CrateBuild.cs` and `CameraPixels.cs`, `Png.cs` and Orcward's `ComparisonPng.cs` linked") and the csproj comment above the item group leave it out as well.
- **Consequence:** a change to `ColourRule.cs`, which about ten suites share, does not change scenario-bakes' fingerprint. `verify` then carries an old pass of the bevel strip check, whose whole prediction rests on that file. This is the gap the brief names, which M-5, M-19 and M-20 each found.
- **Severity:** Major

**Outcome:** —

### The occlusion and normal-map `uvs` frames have no block-rule check, though the retrospective says every frame but curvature keeps it
- **Where:** scenarios/Carve.Scenario.Bakes/OcclusionStage.cs:85; scenarios/Carve.Scenario.Bakes/BevelStage.cs:81; docs/milestones/M-24-baked-maps.md:247-248 (rubric 7, 2)
- **What is wrong:** SurfaceStage and CurvatureStage hold each `uvs` frame they capture to T-121's rule through `BakesChecks.CheckUvImage`. OcclusionStage captures `wrench-orm-uvs-occlusion` and BevelStage captures `wrench-uvs-normal`, and nothing checks either one. The T-126 ruling in the retrospective reads "`blend: 2` on the curvature `uvs` frames only, every other frame keeping T-121's strict block rule". For these two frames that is not true: they have no rule at all. The ten `wrench-orm-*` render frames and the seven `wrench-bevel-*` side frames are also only captured. Apart from the two `render_glb` pairs, they carry no pixel check.
- **Consequence:** the occlusion map changes value from texel to texel, which is the case that exposed `render_glb`'s missing mips. If the look sampled or dimmed it wrongly, or drew a normal map's RGB wrongly, no check would catch it. One stage keeps a rule that two other stages drop, and the tracker says the opposite.
- **Severity:** Minor

**Outcome:** —

### Two scenario checks were never shown failing, against "every new check was shown failing on a break"
- **Where:** scenarios/Carve.Scenario.Bakes/OcclusionStage.cs:168 (`OpenLeast`); scenarios/Carve.Scenario.Bakes/BevelStage.cs:118-144 (`CheckFlat`); docs/milestones/M-24-baked-maps.md:175; docs/tasks/T-124-occlusion-and-the-orm-bake.md:57-60; docs/tasks/T-125-bevel-normals.md:50-53 (rubric 2, 7)
- **What is wrong:** T-124's and T-125's Scope say "checks, each with a break". No break reaches the open-texel bound (≥ 250). `reach` only makes texels more open, and `metal` touches G and B. The bevel flat-texels-hold-N check reads the PNG as baked, while `bevel-g` inverts only the copy put on the material (BevelStage.cs:59, 62). Neither As built names a break for these two checks. The retrospective still says every new check was shown failing.
- **Consequence:** nobody has shown that either check can catch the fault it names. The tracker claims coverage it does not have.
- **Severity:** Minor

**Outcome:** —

### The bevel stage's render_glb comparison drops the control the other two stages keep
- **Where:** scenarios/Carve.Scenario.Bakes/BevelStage.cs:287-296 against BakesChecks.cs:321-333 (rubric 2)
- **What is wrong:** `BakesChecks.CompareExportAsync`, used by SurfaceStage and OcclusionStage, adds a control (three-quarter `render_glb` against the front `render`) that must fail D-6's rule, and notes it if it passes. BevelStage keeps its own `CompareExportAsync`, which calls only `RenderExportAsync` and has no control. The refactor pass (0a14afa) merged the other two but left this one with only the rendering half.
- **Consequence:** if the comparison stops telling frames apart (for example, a `render_glb` that ignores the map's file, or a measure that always passes), the bevel stage's "render_glb agrees with render with the map" passes with nothing to show it could fail. The rule is kept by one stage and broken by another.
- **Severity:** Minor

**Outcome:** —

### The bevel stage reads hard edges by parsing the sample file on disk, not from the model through the tools
- **Where:** scenarios/Carve.Scenario.Bakes/BevelStage.cs:96-102 (rubric 5, CLAUDE.md "never special-case the product or reach into its internals")
- **What is wrong:** `HardEdges` opens the `wrench.carve` copied beside the scenario as JSON and reads `meshes[0].hard_edges`, justified as "which get_mesh does not report". The model the server holds is never asked. A tool already returns hard edges: `set_shading` answers `{mesh, shading, hard_edges}` (ShadingTools.cs:19, 49). Otherwise the rule is to widen the API. This ties the scenario to the file format's field names and to mesh index 0, and the CheckFlat and CloseEdge checks run on the input file rather than on the mesh that was baked.
- **Consequence:** if a tool call between `open` and the bake changed hard edges (or T-71's move of hard edges onto attribute layers changed the file's shape), the checks would test the wrong edges with no error. It also sets the precedent of a scenario reading product storage directly.
- **Severity:** Minor

**Outcome:** —

### T-121's Verify line leaves out the two suites whose `uvs` look frames it changed
- **Where:** docs/tasks/T-121-an-image-under-the-uv-layout-look.md:17 (rubric 4)
- **What is wrong:** T-121 changed the `uvs` look's drawing (`UvLayout.cs`, `RenderSystem.cs`, `RenderTool.cs`, `LookHolder.cs`). Its As built (line 84) compares scenario-crate's and scenario-uvs' frames byte for byte to show nothing moved, yet its `**Verify:**` names only `server-unit, server-regression, scenario-materials`.
- **Consequence:** the member's own `verify` does not re-run the two suites that capture the changed look. Only the milestone-wide run covered them.
- **Severity:** Minor

**Outcome:** —

### Two As built sections still carry an evidence path broken by a lost backslash, and the retrospective names only T-126's
- **Where:** docs/tasks/T-123-the-bakes-scenario.md:112; docs/tasks/T-124-occlusion-and-the-orm-bake.md:152-153; docs/milestones/M-24-baked-maps.md:231 (rubric 7)
- **What is wrong:** T-124's line ends `...\M-24\T-124` and the next line starts `ound-1\`): **approved**`, where `\r` was lost as a line break. T-123 has a stray duplicate line `ound-1\`. The person **approved**, no notes. Agent 2 looked at the frames` after the repaired one. The retrospective says only T-126's path was garbled and repaired.
- **Consequence:** the evidence folder for T-124's approval is not a usable path, and the retrospective understates what still needs repair.
- **Severity:** Nit

**Outcome:** —

### The break table is documented incompletely in two places
- **Where:** CLAUDE.md:91 (`CARVE_BAKES_BREAK` (`swap`, `mirror`, `curvature`, `reach`, `metal`, `bevel-g`)); scenarios/Carve.Scenario.Bakes/BakesChecks.cs:37-41 (rubric 7)
- **What is wrong:** the CLAUDE.md row leaves out `faint` (SurfaceStage.cs:67-68, T-123's break of 53 failures). `BakesChecks.Break`'s summary, the one place the switch is defined, lists only `swap`, `faint` and `mirror`, not the four later stages' breaks. The same row also says the L-block "reads under 128 on its inner edge", where the ruled check is at most 64 (CurvatureStage.cs:28, 123).
- **Consequence:** anyone running the breaks from the docs misses one, or reads a weaker concave bound than the code holds.
- **Severity:** Nit

**Outcome:** —

### T-121's block rule is written twice, and stage helpers the refactor left are duplicated
- **Where:** scenarios/Carve.Scenario.Materials/UvImageFrame.cs:13-19, 98-162 against scenarios/Carve.Scenario.Bakes/BakesChecks.cs:66-164; BevelStage.cs:42 (`Steel = "#80858A"`, "from SurfaceStage's table"); the `groups` parse at SurfaceStage.cs:47, CurvatureStage.cs:37, OcclusionStage.cs:57, BevelStage.cs:50 (rubric 3)
- **What is wrong:** the window of 8/7, the `(c + 1) / 2` padding, the quarter-to-three-quarter texel placement, the 4 px edge clearance and the crossing test are coded separately in the Materials frame and in the Bakes checks, and the two are not linked. Within Bakes, the steel colour is copied from SurfaceStage's table into BevelStage, and the same groups lookup is written four times. UvImageFrame's summary also says the barrel's UVs fill the unit square "so it is inside the islands", while the code needs 10 or more padding samples to pass (line 70).
- **Consequence:** a change to the look's rule, or to the steel colour, has to be made twice or the checks drift apart without anyone noticing.
- **Severity:** Nit

**Outcome:** —

## Summary

Three reviewers on Opus 5.5 read the diff in parallel, sized to its spread (about 6800 lines over 52 files in the Core kernel, the server and rendering, a new scenario and the tracker): one on `src/Carve.Core` and its tests, one on `src/Carve.Server`, `src/Carve.Hosting` and their tests, and one on the whole diff for the scenario, `tests.json`, `CLAUDE.md`, the tracker and what falls between the parts. Each wrote its own `## Findings` section.

**Summary (core):** I read every file in `src/Carve.Core/Baking/` (layout, raster and top-left rule, padding, frame, edges and coplanarity, masks, ray caster, occlusion and ORM, bevel, curvature), the `FaceStyle.ToBytes` change, and the six bake test classes. I read them against T-120's Scope, Notes and As built, the Scope of T-124 to T-126, and the milestone's rulings. I wrote and ran throwaway probe tests (asymmetric L-block, centre on a boundary, clamp tie, Pad timing) and then deleted them; the worktree is as found. The 51 bake unit tests pass. I found no determinism fault: every parallel loop writes only its own row or column, and the counters are integer sums. Tangent-frame sign, mirrored islands, BVH traversal and the padding tie rule all check out. I did not run anything at 4096, read the unit tests of the tangent frame and masks in detail, or review `BakeTool`'s use of the kernel.

**Summary (server):** Read `BakeTool.cs` whole. Read the diffs of `RenderTool.cs`, `RenderSystem.cs` (the `uvs` image draw, its release in `Make`/`Install`, the `GenerateMipmaps` load and its engine need 8 comment), `UvLayout.cs`, `LookHolder.cs`, `MaterialTools.cs`, `RenderGlbTool.cs` and `RenderQueue.cs`, plus `ToolPaths`. Read T-121's and T-122's Scope and the milestone with its rulings, and the bake and `uvs`-image tests (the regression tests read, not run). Ran the 59 unit tests of `BakeToolTests`, `BakeNormalToolTests`, `BakeOcclusionToolTests` and `UvImageArgumentTests` (all pass), and one throwaway probe class for findings 1, 2 and 4 (deleted; no repo file changed by me). The texture lifetime (rebuilt on a different image by `MaterialTexture`'s byte equality, released on `Install` or on the next `Make`), the sRGB round trip and the mipmap change agree with the code and the retrospective. Not read: T-124/T-125/T-126 in full, the Core kernel, `BevelRenderTests` and `RenderGlbSamplingTests` beyond their names. An untracked `tests/Carve.Core.Tests/ZzReviewProbeTests.cs` in the worktree is not mine; it is presumably another reviewer's probe.

**Summary (whole):** I read the whole Bakes scenario (every stage, `BakesChecks.cs`, the csproj and the break switch), `UvImageFrame.cs` and its call, the `tests.json` entry against every linked file, `Carve.slnx`, the CLAUDE.md, D-7 and changelog diffs, T-123 to T-125's Scope and As built, T-154, the milestone retrospective's rulings (every threshold in code matches them) and the two refactor-pass commits. I also checked the product diff for break switches, environment reads and InternalsVisibleTo (none) and the bakes for nondeterminism (none found). `cairn lint` is clean, and the Bakes project builds with 0 warnings. I read T-120, T-122 and T-126's tracker text only for their Verify lines, did not run the unit tests or any scenario, opened no images, and could not check `samples/wrench.carve` against fps-arms (private repo; git holds it as LF text).
