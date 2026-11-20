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

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 3bd5c2f; `BakeLayoutTests.On_a_long_armed_l_block_only_the_inner_edge_is_concave` (the profile (0,0) (3,0) (3,1) (1,1) (1,2) (0,2): only (3, 9) is concave and the wall x = 1's cap edges are convex) fails without it. The side test now takes the direction into the other face across the edge (its normal crossed with the edge as its own winding runs it), not the other face's corner mean, so it holds for a non-convex face and does not rest on the two faces being wound alike. Every bake and frame `scenario-bakes` writes was hashed before and after (runs 1 and 2 under `artifacts/scenarios/bakes`): all 76 PNGs and every `.tsv` but the run-path and `ms` columns are byte-identical, the L-block stage and the person's approved wrench frames included, so no ruling was needed.

### A texel centre on an island's bottom or right boundary is recorded as Clamp, not Centre
- **Where:** src/Carve.Core/Baking/BakeRaster.cs:41,66-70 (rubric 1)
- **What is wrong:** Pass 1 applies the top-left rule to every edge, including an island's outer boundary. A centre lying exactly on a bottom or right boundary edge is held by no triangle, so pass 2 takes it with d² = 0 and flags it `Clamp`. T-120's rule is that the clamp is for a centre "outside every triangle", and that the result "records which texels were covered by their centres and which by the clamp". The probe (deleted) laid a quad whose right edge is u = 20.5/64. `CoverageAt(20, 15)` was `Clamp`. The weights and position are right; only the label and `ClampedCount` are wrong.
- **Consequence:** `ClampedCount` and `BakeCoverage` overstate the clamp on layouts with edges on centre lines. Nothing baked changes today, but M-29 or a stat that reads coverage would see it.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 1de2a41; `BakeLayoutTests.A_centre_on_an_islands_right_boundary_counts_as_a_centre` fails without it. A clamped texel at distance 0 (its centre on a triangle's edge that the top-left rule gives to no one) is labelled `Centre`; its weights are the clamp's as before. No baked value reads the label, and the scenario's bakes and frames are byte-identical.

### The clamp's "nearer face wins, tie to the lower face" rule has no test
- **Where:** tests/Carve.Core.Tests/BakeLayoutTests.cs:73-117 (rubric 2)
- **What is wrong:** T-120's coverage bullet says a texel within reach of two faces takes the nearer, and a tie goes to the lower face id. The clamp tests (`An_inset_quad_...`, `A_sliver_face_...`) each have one face in reach. Nothing fails if pass 2 kept the first or the last triangle it met, or compared `<=` instead of `<` (BakeRaster.cs:72). A probe with faces 5 and 1 equidistant from column 10 got face 1, so the code is right; only the test is missing.
- **Consequence:** A Scope rule that T-120's Notes name as a stop is unguarded against regression.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 1de2a41; `BakeLayoutTests.A_clamped_texel_as_near_two_faces_takes_the_lower_face` fails with `<=` in pass 2's comparison (tried). Faces 5 and 1 a quarter texel either side of column 10's centres; every texel of column 10 is `Clamp` on face 1.

### Pad's cost is size² × padding, so padding near size/2 at 4096 runs for about a minute per map
- **Where:** src/Carve.Core/Baking/BakePadding.cs:56-77 (rubric 1)
- **What is wrong:** The row pass scans dx from 0 up to `texels` for every uncovered texel. It stops early only once dx² passes the best distance found, so texels far from any island scan the whole range. Measured with a throwaway probe (deleted), on 16 cores in Debug, with one small island: 1024 at padding 512 took 1.0 s, and 2048 at padding 1024 took 6.9 s. That is roughly cubic, so 4096 at 2048 comes to about 50 s for each padded map. `bake` accepts padding up to size/2 and pads every map and every mask.
- **Consequence:** A legal `bake` call at 4096 with large padding (not verified, per the retrospective) runs for minutes, far past the tool's answer window. That crosses into the server reviewer's part. Either a two-pass exact distance transform (linear in size² per pass), or a lower cap on padding, would remove it.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) a7f65de; `BakePadReferenceTests.Pad_matches_the_row_scan_it_replaced_bit_for_bit` (the speed itself has no test). The row pass is now the lower envelope of one parabola per column (Felzenszwalb and Huttenlocher), with the nearest-texel and lowest-index tie rule folded into exact integer switch points; the column pass is unchanged. With one small island, padding size/2 took 27 ms at 1024, 92 ms at 2048 and 287 ms at 4096 (Debug, a throwaway timing test, deleted). Byte identity: `BakePadReferenceTests` keeps T-120's row scan verbatim and holds the new `Pad` to it bit for bit on 12 layouts (celled cube, L-block, a one-face and a two-face subset, and eight seeded layouts of quads on whole and half texels, so many texels are as near two islands) at paddings 1, 2, 3, 5, 8, 13, size/4, size/2 - 1 and size/2; turning the tie round fails it and the existing padding test. The scenario's padded maps are byte-identical.

### WrenchBevelSeamTests is tagged unit but takes about 4 s
- **Where:** tests/Carve.Core.Tests/WrenchBevelSeamTests.cs:14-17 (rubric 6)
- **What is wrong:** The class carries `Tier=unit`, but it bakes the wrench's full bevel map at 1024 twice (the good width and the thin one), which measured 4 s. CLAUDE.md says `unit` means about 1 s or less per test. The person's ruling placed the seam check as a Core test. It did not set the tier or the size.
- **Consequence:** `core-unit` is slower than its tier promises. It is the largest single cost among the 51 bake tests (7 s in total).
- **Severity:** Nit

**Outcome:** skipped: (claude-opus-5-5) making it fast means baking less than the ruled map (a smaller size or width, or a face subset, which can change which texels the clamp gives each face and so the ruled 9° readings), and moving it means a new Core `regression` suite that the tier rule does not fit (it starts no `carve` and needs no device). A Nit; left for the person to place if it matters.

## Findings: the server and rendering (claude-opus-5-5)

### A bad folder is not reported with the call's other problems, and when it is reported it comes once per file, in another tool's words
- **Where:** src/Carve.Server/BakeTool.cs:197 and :229-237 (rubric 1)
- **What is wrong:** `bake` returns at line 197 with the argument errors before it resolves any output path. So the folder's path problems (lines 231-232) and the case clash (235-236) are only found on a call with nothing else wrong. When they are found, `ToolPaths.Resolve` runs once per job, so one bad folder gives one error per file. Each error is worded as a path the agent never gave, with a `bake:` prefix inside the list, not as `folder: ...`. Shown with a throwaway probe test (deleted). On the strip with group `g`, `{"size":64,"folder":"a*b","surface":{"g":{"roughness":2}}}` gives `bake failed with 1 problem(s)`, the roughness alone. `{"size":64,"folder":"a*b"}` then gives 2: `bake: path "a*b/strip.mask.g.png" holds the character '*'...` and the same for `strip.base_colour.png`.
- **Consequence:** this breaks T-122's "Errors, all at once" and the description's "Errors, all listed at once". An agent fixes one problem, calls again and meets the next. On the wrench's default maps (six masks and base colour), one bad character becomes seven near-identical lines about file paths, where the problem is the `folder` argument.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 85eb530; `BakeToolTests.A_bad_folder_is_one_folder_problem_listed_with_the_others` and `BakeToolTests.Two_masks_that_differ_only_by_case_are_refused_with_the_other_problems` fail without it. The folder is judged once (`ToolPaths.FolderProblem`) with the argument errors, as `folder: "a*b" holds the character '*', which a folder path may not`, in `ToolPaths`' words for a file; the masks' case clash is worked from the group and material names, before the layout, so it is listed with the rest too. No test or digest pinned the old words (`bake` is outside the tools/list digest).

### A surface entry naming a model material the mesh does not use is accepted silently
- **Where:** src/Carve.Server/BakeTool.cs:183-185 (rubric 1)
- **What is wrong:** `known` adds every material of the model (`model.Materials`), not only those the mesh's faces use. An entry naming a defined but unapplied material passes validation, covers no face, and gets no warning. The description (line 62) lists as an error "a surface entry naming a group or material the mesh does not have". Probe (deleted): `define_material {name:"unused"}` then `bake {maps:["base_colour"], surface:{"unused":{base_colour:"#000000"}}}` succeeds with `warnings: []`, and the map is all white.
- **Consequence:** an agent that defines a material and bakes before `set_material` gets a white base colour and no hint why. The tool's description says this case is refused.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 82127f6; `BakeToolTests.A_surface_entry_naming_a_material_no_face_uses_is_refused_with_the_meshs_names` fails without it. The surface's names are checked against the mesh's groups and the materials its faces use; the error (same words) lists those names.

### A failed write part way through leaves earlier files written, and the error does not say so
- **Where:** src/Carve.Server/BakeTool.cs:241-247 (rubric 1)
- **What is wrong:** files are written one at a time. If `ToolPaths.Write` fails on file k (a PNG held open by a viewer on Windows, a full disk), the tool returns `Error(writeError)` and drops the results for files 0..k-1. Those files are already written, or already replaced over the previous bake. The description says each error "writes nothing". This one is not in its list, but nothing tells the agent that some maps changed. Found by reading the code; no probe.
- **Consequence:** after a failed `bake` the folder holds a mix of new and old maps (say a new base colour with last call's ORM), and the agent cannot tell which.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 85eb530; `BakeToolTests.A_write_that_fails_part_way_names_the_files_written_before_it_and_those_not_written` fails without it. The error now ends `The bake stopped there; written before it, replacing any file of that name: ...; not written: ...`, and the description says a failed write stops the bake and names both. Staging every file and moving them all at the end was not done: each file already goes through its own temp file and move, and a run of moves can fail part way just the same.

### The description says `path` is absolute when the folder is, but an absolute folder comes back relative
- **Where:** src/Carve.Server/BakeTool.cs:59 and :512-516 (rubric 1)
- **What is wrong:** `ReportedPath` returns `Path.GetRelativePath(working, full)` unless the result is rooted, and on the same drive it never is. Probe (deleted): folder `<working>/abs` (absolute) came back as `abs/strip.base_colour.png`. An absolute folder outside the working directory came back as `../carve-probe-out-d3a74d/strip.base_colour.png`. The description says "(absolute when folder is)".
- **Consequence:** `define_material` still resolves the returned path to the same file, so nothing breaks. But the agent is told one form and gets another, including `..` paths it did not write.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 85eb530; a description fix, no test. The code is right (`define_material` resolves the path either way), so the text now says what it does: path is relative to the working folder, an absolute folder too, with `..` for one outside it, and absolute only on another drive. `bake` is one of the tools `PartToolsListTests` leaves out of its digest, so no digest moved.

### `image` with a non-`uvs` mode returns alone, before the call's other problems
- **Where:** src/Carve.Server/RenderTool.cs:317-320 (rubric 1)
- **What is wrong:** with a mode other than `uvs`, `image` returns an immediate `Error` before the shaded/checker path checks its other arguments. So `{"image":"a.png","view":"nope"}` reports only the image. In `uvs` mode the image problem is added to `problems` and listed with the rest (`UvImageArgumentTests.The_image_problem_is_reported_with_the_calls_other_problems`), but not here.
- **Consequence:** a second round trip for a mistyped call. This is minor, since the image refusal is the likely root mistake.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 8c1a528; `UvImageArgumentTests.Image_with_another_mode_is_reported_with_the_calls_other_problems` (view, projection, level) fails without it. With a mode other than `uvs`, the image's refusal, in its own words, goes ahead of whichever argument problem follows it (`render failed with 2 problem(s)`), and still comes back alone, as before, when it is the only one.

## Findings: the whole diff (claude-opus-5-5)

### Suite scenario-bakes does not list ColourRule.cs among its inputs, though the scenario links it
- **Where:** tests.json:645-669 (inputs of `scenario-bakes`); scenarios/Carve.Scenario.Bakes/Carve.Scenario.Bakes.csproj:21; CLAUDE.md:91 (the Bakes row's list of linked files) (rubric 7, 4)
- **What is wrong:** T-125 (commit 9873ab4) added `<Compile Include="../Carve.Scenario.Crate/ColourRule.cs" Link="ColourRule.cs" />` to the Bakes project, and `BevelStage.CheckStrip` uses `ColourRule.Lit`/`Encode` to predict the strip's shade (BevelStage.cs:263-264). The suite's inputs list `UvsScene.cs`, `CrateBuild.cs`, `CameraPixels.cs`, `Png.cs` and `ComparisonPng.cs` but not `scenarios/Carve.Scenario.Crate/ColourRule.cs`. A `dotnet-closure` of the Bakes project takes the project and its references, not linked source, which is why every other linked file is listed by hand (ten other suites list `ColourRule.cs`). T-123's Notes require each later member to "add any new inputs to the suite", and T-123's As built says the suite has "every linked file". The CLAUDE.md row's list of linked files ("with the Uvs scenario's `UvsScene.cs`, the Crate's `CrateBuild.cs` and `CameraPixels.cs`, `Png.cs` and Orcward's `ComparisonPng.cs` linked") and the csproj comment above the item group leave it out as well.
- **Consequence:** a change to `ColourRule.cs`, which about ten suites share, does not change scenario-bakes' fingerprint. `verify` then carries an old pass of the bevel strip check, whose whole prediction rests on that file. This is the gap the brief names, which M-5, M-19 and M-20 each found.
- **Severity:** Major

**Outcome:** fixed

**Fix:** (claude-opus-5-5) a1aee7d; a tracker fix, no test. Added to `tests.json`'s inputs, the CLAUDE.md Bakes row and the csproj comment. Every other linked file of the project (`UvsScene.cs`, `CrateBuild.cs`, `CameraPixels.cs`, `Png.cs`, `ComparisonPng.cs`, `wrench.carve`) was already an input.

### The occlusion and normal-map `uvs` frames have no block-rule check, though the retrospective says every frame but curvature keeps it
- **Where:** scenarios/Carve.Scenario.Bakes/OcclusionStage.cs:85; scenarios/Carve.Scenario.Bakes/BevelStage.cs:81; docs/milestones/M-24-baked-maps.md:247-248 (rubric 7, 2)
- **What is wrong:** SurfaceStage and CurvatureStage hold each `uvs` frame they capture to T-121's rule through `BakesChecks.CheckUvImage`. OcclusionStage captures `wrench-orm-uvs-occlusion` and BevelStage captures `wrench-uvs-normal`, and nothing checks either one. The T-126 ruling in the retrospective reads "`blend: 2` on the curvature `uvs` frames only, every other frame keeping T-121's strict block rule". For these two frames that is not true: they have no rule at all. The ten `wrench-orm-*` render frames and the seven `wrench-bevel-*` side frames are also only captured. Apart from the two `render_glb` pairs, they carry no pixel check.
- **Consequence:** the occlusion map changes value from texel to texel, which is the case that exposed `render_glb`'s missing mips. If the look sampled or dimmed it wrongly, or drew a normal map's RGB wrongly, no check would catch it. One stage keeps a rule that two other stages drop, and the tracker says the opposite.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 7aac4e6, with the retrospective's line in the commit that answers this review; a scenario break each, no unit test. `CARVE_BAKES_BREAK=dark` reads the occlusion inverted (a reach cannot do it: with reach 1 m the grip's middle still reads 255, nothing standing over it): 2 failures, the grip's middle reading 0 against 250 and the crevice margin. `bevel-wide` bakes the normal map 20 times as wide: 380 of the 448 flat texels fail, and 2 strip checks. Both breaks are in the scenario only. The retrospective now says those two were shown failing only here.

**Open:** (claude-opus-5-5) both frames fail T-121's strict rule, so a check on either needs a tolerance, which is the person's. Probed on 7aac4e6's code, with 22558 inside and 28344 outside pixels placed in each frame: `wrench-orm-uvs-occlusion` breaks it at 96 inside pixels (every one `inside-png`, up to 8 levels off, e.g. the frame 216 where the texel is 208, on the gradients of faces 64 and 65; none outside), and passes with `blend: 1` (and 2). `wrench-uvs-normal` breaks it at 3 inside pixels (faces 79, 12, 27: one channel 3 levels off N, e.g. (131, 130, 255) for (128, 128, 255)), and `blend` 1 or 2 does not clear them, since `Blended` reads grey. Options: (a) occlusion with `blend: 1` and the normal map with a per-channel slack of 3 (or a blend that reads each channel); (b) occlusion with `blend: 1` and the normal frame left unchecked; (c) both left as captures only, the retrospective's ruling line saying so. No check was added; the frames are as they were.

### Two scenario checks were never shown failing, against "every new check was shown failing on a break"
- **Where:** scenarios/Carve.Scenario.Bakes/OcclusionStage.cs:168 (`OpenLeast`); scenarios/Carve.Scenario.Bakes/BevelStage.cs:118-144 (`CheckFlat`); docs/milestones/M-24-baked-maps.md:175; docs/tasks/T-124-occlusion-and-the-orm-bake.md:57-60; docs/tasks/T-125-bevel-normals.md:50-53 (rubric 2, 7)
- **What is wrong:** T-124's and T-125's Scope say "checks, each with a break". No break reaches the open-texel bound (≥ 250). `reach` only makes texels more open, and `metal` touches G and B. The bevel flat-texels-hold-N check reads the PNG as baked, while `bevel-g` inverts only the copy put on the material (BevelStage.cs:59, 62). Neither As built names a break for these two checks. The retrospective still says every new check was shown failing.
- **Consequence:** nobody has shown that either check can catch the fault it names. The tracker claims coverage it does not have.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 7aac4e6; a scenario check, no unit test. BevelStage keeps its own rows in `bevel.tsv` and adds the control from `RenderExportAsync`'s frames, as `BakesChecks.CompareExportAsync` does: the three-quarter `render_glb` against the front `render`, noted if it passes. On the plain run it differs in 88205 of 1048576 pixels (patch 86985) and fails, as it must; the two good pairs still pass (0 and 1 pixels).

### The bevel stage's render_glb comparison drops the control the other two stages keep
- **Where:** scenarios/Carve.Scenario.Bakes/BevelStage.cs:287-296 against BakesChecks.cs:321-333 (rubric 2)
- **What is wrong:** `BakesChecks.CompareExportAsync`, used by SurfaceStage and OcclusionStage, adds a control (three-quarter `render_glb` against the front `render`) that must fail D-6's rule, and notes it if it passes. BevelStage keeps its own `CompareExportAsync`, which calls only `RenderExportAsync` and has no control. The refactor pass (0a14afa) merged the other two but left this one with only the rendering half.
- **Consequence:** if the comparison stops telling frames apart (for example, a `render_glb` that ignores the map's file, or a measure that always passes), the bevel stage's "render_glb agrees with render with the map" passes with nothing to show it could fail. The rule is kept by one stage and broken by another.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 7aac4e6; a scenario change, no unit test. The hard edges come from `set_shading`'s answer: the wrench is set to the smooth shading it already has (the mesh is unchanged), and the answer's `hard_edges` are the server's. `wrench.carve` is no longer read for them. The checks see the same edges as before: 448 flat texels and the same strip rows in `bevel.tsv`, and every bake and frame was byte-identical.

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

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 28ae5ba; a doc fix, no test. T-121's `**Verify:**` now names `scenario-crate` and `scenario-uvs`.

### Two As built sections still carry an evidence path broken by a lost backslash, and the retrospective names only T-126's
- **Where:** docs/tasks/T-123-the-bakes-scenario.md:112; docs/tasks/T-124-occlusion-and-the-orm-bake.md:152-153; docs/milestones/M-24-baked-maps.md:231 (rubric 7)
- **What is wrong:** T-124's line ends `...\M-24\T-124` and the next line starts `ound-1\`): **approved**`, where `\r` was lost as a line break. T-123 has a stray duplicate line `ound-1\`. The person **approved**, no notes. Agent 2 looked at the frames` after the repaired one. The retrospective says only T-126's path was garbled and repaired.
- **Consequence:** the evidence folder for T-124's approval is not a usable path, and the retrospective understates what still needs repair.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 28ae5ba; a doc fix, no test. T-124's path rejoined with its `\round-1`, T-123's stray line removed, and the retrospective's line on T-126 says T-123's and T-124's had the same fault and were repaired here.

### The break table is documented incompletely in two places
- **Where:** CLAUDE.md:91 (`CARVE_BAKES_BREAK` (`swap`, `mirror`, `curvature`, `reach`, `metal`, `bevel-g`)); scenarios/Carve.Scenario.Bakes/BakesChecks.cs:37-41 (rubric 7)
- **What is wrong:** the CLAUDE.md row leaves out `faint` (SurfaceStage.cs:67-68, T-123's break of 53 failures). `BakesChecks.Break`'s summary, the one place the switch is defined, lists only `swap`, `faint` and `mirror`, not the four later stages' breaks. The same row also says the L-block "reads under 128 on its inner edge", where the ruled check is at most 64 (CurvatureStage.cs:28, 123).
- **Consequence:** anyone running the breaks from the docs misses one, or reads a weaker concave bound than the code holds.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 28ae5ba; a doc fix, no test. The CLAUDE.md row lists `faint` and says the L-block reads 64 or less; `BakesChecks.Break`'s summary lists every stage's break.

### T-121's block rule is written twice, and stage helpers the refactor left are duplicated
- **Where:** scenarios/Carve.Scenario.Materials/UvImageFrame.cs:13-19, 98-162 against scenarios/Carve.Scenario.Bakes/BakesChecks.cs:66-164; BevelStage.cs:42 (`Steel = "#80858A"`, "from SurfaceStage's table"); the `groups` parse at SurfaceStage.cs:47, CurvatureStage.cs:37, OcclusionStage.cs:57, BevelStage.cs:50 (rubric 3)
- **What is wrong:** the window of 8/7, the `(c + 1) / 2` padding, the quarter-to-three-quarter texel placement, the 4 px edge clearance and the crossing test are coded separately in the Materials frame and in the Bakes checks, and the two are not linked. Within Bakes, the steel colour is copied from SurfaceStage's table into BevelStage, and the same groups lookup is written four times. UvImageFrame's summary also says the barrel's UVs fill the unit square "so it is inside the islands", while the code needs 10 or more padding samples to pass (line 70).
- **Consequence:** a change to the look's rule, or to the steel colour, has to be made twice or the checks drift apart without anyone noticing.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** (claude-opus-5-5) 28ae5ba; a refactor, no new test. In part: the four `groups` lookups are one `BakesChecks.GroupsOf`, `BevelStage` takes `SurfaceStage.SteelColour`, and `UvImageFrame`'s summary says what the check does (samples inside the islands and in the `(c + 1) / 2` padding, ten or more of each). The block rule itself is still coded once in the Materials frame and once in the Bakes checks: sharing it means linking one scenario's checks into the other's suite, left for a later pass. Every Bakes frame and file was byte-identical after the change.

## Summary

Three reviewers on Opus 5.5 read the diff in parallel, sized to its spread (about 6800 lines over 52 files in the Core kernel, the server and rendering, a new scenario and the tracker): one on `src/Carve.Core` and its tests, one on `src/Carve.Server`, `src/Carve.Hosting` and their tests, and one on the whole diff for the scenario, `tests.json`, `CLAUDE.md`, the tracker and what falls between the parts. Each wrote its own `## Findings` section.

**Summary (core):** I read every file in `src/Carve.Core/Baking/` (layout, raster and top-left rule, padding, frame, edges and coplanarity, masks, ray caster, occlusion and ORM, bevel, curvature), the `FaceStyle.ToBytes` change, and the six bake test classes. I read them against T-120's Scope, Notes and As built, the Scope of T-124 to T-126, and the milestone's rulings. I wrote and ran throwaway probe tests (asymmetric L-block, centre on a boundary, clamp tie, Pad timing) and then deleted them; the worktree is as found. The 51 bake unit tests pass. I found no determinism fault: every parallel loop writes only its own row or column, and the counters are integer sums. Tangent-frame sign, mirrored islands, BVH traversal and the padding tie rule all check out. I did not run anything at 4096, read the unit tests of the tangent frame and masks in detail, or review `BakeTool`'s use of the kernel.

**Summary (server):** Read `BakeTool.cs` whole. Read the diffs of `RenderTool.cs`, `RenderSystem.cs` (the `uvs` image draw, its release in `Make`/`Install`, the `GenerateMipmaps` load and its engine need 8 comment), `UvLayout.cs`, `LookHolder.cs`, `MaterialTools.cs`, `RenderGlbTool.cs` and `RenderQueue.cs`, plus `ToolPaths`. Read T-121's and T-122's Scope and the milestone with its rulings, and the bake and `uvs`-image tests (the regression tests read, not run). Ran the 59 unit tests of `BakeToolTests`, `BakeNormalToolTests`, `BakeOcclusionToolTests` and `UvImageArgumentTests` (all pass), and one throwaway probe class for findings 1, 2 and 4 (deleted; no repo file changed by me). The texture lifetime (rebuilt on a different image by `MaterialTexture`'s byte equality, released on `Install` or on the next `Make`), the sRGB round trip and the mipmap change agree with the code and the retrospective. Not read: T-124/T-125/T-126 in full, the Core kernel, `BevelRenderTests` and `RenderGlbSamplingTests` beyond their names. An untracked `tests/Carve.Core.Tests/ZzReviewProbeTests.cs` in the worktree is not mine; it is presumably another reviewer's probe.

**Summary (whole):** I read the whole Bakes scenario (every stage, `BakesChecks.cs`, the csproj and the break switch), `UvImageFrame.cs` and its call, the `tests.json` entry against every linked file, `Carve.slnx`, the CLAUDE.md, D-7 and changelog diffs, T-123 to T-125's Scope and As built, T-154, the milestone retrospective's rulings (every threshold in code matches them) and the two refactor-pass commits. I also checked the product diff for break switches, environment reads and InternalsVisibleTo (none) and the bakes for nondeterminism (none found). `cairn lint` is clean, and the Bakes project builds with 0 warnings. I read T-120, T-122 and T-126's tracker text only for their Verify lines, did not run the unit tests or any scenario, opened no images, and could not check `samples/wrench.carve` against fps-arms (private repo; git holds it as LF text).

## Hand-off

The answer (claude-opus-5-5, two agents) has 18 of 19 findings answered and **Status** left Open: the block rule on the
occlusion and normal-map `uvs` frames waits on the person (its **Open:** line has the numbers and the options). Tip after
this commit.

- **Answered by the second agent:** the bad `folder` and the case clash, listed once with the other problems; the failed
  write naming the files; `path`'s description; render's `image` refusal listed with the other problems; a break each for
  the two unbroken checks (`dark`, `bevel-wide`); BevelStage's `render_glb` control; hard edges through `set_shading`.
- **Left:** the block-rule finding. Once ruled: add the `CheckUvImage` calls (their places in OcclusionStage and BevelStage
  carry an RV-74 comment), run `scenario-bakes`, fill the outcome, remove this section and set **Status:** Answered.
- **Checked:** `scenario-bakes` plain on the final code (run 12): passed, all 66 PNGs byte-identical with run 3, the
  first agent's last; the text files differ only by the run folder in paths, the `ms` timings and `bevel.tsv`'s new
  control row. 994 server unit tests pass.
- **Surprising:** a reach does not darken the grip's middle at all (nothing stands over it), so its break reads the map
  inverted, as `curvature`'s does.
