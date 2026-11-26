---
description: >-
  Review of M-29, checking RV-82's answers
---
# RV-83 — Review of M-29, checking RV-82's answers

**Status:** Open

**Reviews:** M-29

**Round:** 2

**Previous:** RV-82

**Diff:** 30e203107052d5f216a4cb057509fc52dd7defb8 against origin/main

**Reviewer:** two reviewers, each claude-opus-5-5 (Claude Opus 5.5), checking RV-82's 36 answers against the code and reviewing the five fix members' commits: one on the Core, Carve.Graphs and the server (T-217, T-218), one on the descriptions, patina and the acceptance (T-219, T-220, T-221)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-82's answers on the Core, Carve.Graphs and the server, and T-217's and T-218's commits (claude-opus-5-5)

| RV-82 finding | Member | Answer |
|---|---|---|
| The cache key leaves out the slot (Core) | T-217 | holds |
| `graphs-unit` reads the planks sample | T-217 | holds |
| `Validate` passes mesh-node parameters | T-217 | holds |
| Seven `unit` tests run well over the tier's 1 s | T-217 | holds |
| `Read` hashes the bytes once and `Rgba` reads the files again | T-217 | holds |
| `Times_each_map_on_the_wrench` asserts nothing | T-217 | holds |
| The files' hash takes each file's absolute path | T-217 | holds |
| The cache key has no slot (server) | T-217 | holds |
| `DrawnOf` says no render draws empty graph slots | T-218 | does not hold |
| A per-mesh copy's name can be a material the model already has | T-218 | does not hold |
| Bake memory is held outside the 512 MB cap (skipped, T-222) | T-222 | holds: T-222 states the problem truly |
| Export cannot be cancelled | T-218 | holds |
| A bake that fails on the mesh tells the agent to redefine the material | T-218 | holds |
| `PieceExport.Select` now carries the model's materials, with no test | T-218 | holds |
| The server suites read sample files their inputs leave out | T-217 | holds |

### A model the pump shows while a render's `Resolve` of it lands is drawn with empty graph slots, marked baked, and stays so
- **Where:** `src/Carve.Server/RenderPump.cs:200-203` (`ShowMesh`: `DrawnOf` at 200, then a second `Peek` at 202, then `_unbaked` from that second `Peek`); `src/Carve.Server/GraphBakes.cs:55-58` (`DrawnOf` makes its own `Peek`) and `:170-176` (a `Resolve` with the same keys keeps the same `Model` reference); `RenderPump.cs:174` (shows the mesh again only when `Peek` changes)
- **Severity:** Major
- **Rubric:** RV-82 answer does not hold: "`DrawnOf` says no render draws empty graph slots"; 1 (threading)
- **What is wrong:** `ShowMesh` reads the resolution twice. `DrawnOf(M2)` can find no `Peek` and build M2 with every graph slot empty. If a render's `Resolve(M2)` then reaches `_drawn.AddOrUpdate` before line 202, `_shownBakes` is the new resolution and `_unbaked` is false. Nothing then marks the frame or shows the mesh again: line 174 compares `Peek(M2)` with `_shownBakes`, and they are equal. The render posts and gets a frame with empty slots and `Unbaked == false`, so `SheetVersions.Baked` returns it. Every later render of M2 does the same. Its `Resolve` gives the same keys, so `Resolve` keeps `last.Model` (line 174 of GraphBakes.cs), `Peek` returns the same reference, and the pump never shows the mesh again. That lasts until the next edit. The order needs no second client: an edit returns, the pump shows the new version on its next idle step (`RenderPump.cs:73-76`), and the agent's next `render` resolves the same model at the same moment. A cache hit makes that resolve take only milliseconds. The window is only the `WithMaterial` loop in `DrawnOf`, so it is rare, but the result is wrong and persistent. So the comment at `GraphBakes.cs:51-53` ("No render returns that") is still not true, and neither is T-218's Goal ("never hands back a frame drawn with empty graph slots"). Found by reading; no hook in `GraphBakes` lets a test force the interleaving, so I did not run it. Taking `Peek` once, and passing it to `DrawnOf` or setting `_unbaked` from whether `DrawnOf` used it, would close it.

**Outcome:** —

### A slot that would refuse its output is served the base-colour bake from the cache instead of refusing
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:104` (every slot but metallic-roughness is keyed as `base_colour_texture`), against `:171` and `:175` (`normal_texture` refuses a grey output, `occlusion_texture` a colour one); `src/Carve.Server/GraphBakes.cs:107` (a hit skips `Png`, and with it `PackingOf`)
- **Severity:** Minor
- **Rubric:** 1. Correctness (the cache key: "packs an output the same way or refuses it")
- **What is wrong:** The key's comment holds for the bytes: where both slots take an output they pack it alike. But a refusing slot now shares a key with base colour, so once base colour's bake of that output is cached, the refusing slot gets a hit and never reaches `PackingOf`. render and export call no `Validate` before `Resolve` (`grep` over `src/Carve.Server`: `Validate` is only in `MaterialTools.cs:253` and `GraphSlotProblem.cs:120,145`). So a graph edited after `define_material` (patina's ordinary loop) can turn an output from grey to colour. render and export then draw the colour image as occlusion rather than refusing as T-166 says they must. Probe in a scratch copy (`ProbeRv83Tests.Probe_a_refusing_slot_takes_a_cached_base_colour_bake`, `ToolHarness`). Material `m` has `base_colour_texture` and `occlusion_texture` both `{g.texgraph, x, 64}`, with `x` grey (`mesh_curvature`), and is exported. Then `g.texgraph` is rewritten so that `x` is a `colour` node. The second export succeeded and wrote `b.glb`, its `baked:` naming only `base_colour_texture`. A material `solo` with only `occlusion_texture: {g.texgraph, x}` was then exported too, with no `baked:` line: the occlusion slot took base colour's cached PNG. Without that cache entry the same slot throws "'x' is a colour output; occlusion_texture takes a grey one". The same holds for a `normal_texture` on an output that became grey. T-217's slot-table test (`A_slot_keyed_as_base_colour_packs_every_output_as_base_colour_does_or_refuses_it`) checks `Png` directly, never through the cache, so it cannot see this. Keying a slot that refuses the output's kind apart (or running `PackingOf` before `Get`) would fix it.

**Outcome:** —

### `Copies` still renames a user's `o@mesh` material when the mesh-reading graph material `o` is worn by no mesh
- **Where:** `src/Carve.Server/ExportTool.cs:312-315` (a copy is any `o@<mesh>` on that mesh with `o.HasGraph`); `src/Carve.Server/GraphSlotProblem.cs:89-100` (`Clashes` only looks at meshes that wear `o`); `GraphBakes.cs:148` (`Resolve` makes copies only for those)
- **Severity:** Minor
- **Rubric:** RV-82 answer does not hold: "A per-mesh copy's name can be a material the model already has"
- **What is wrong:** The new rule takes "o a graph material still unbaked" to mean "a copy was made". But a mesh-reading `o` that no mesh wears is never resolved either, so it keeps `HasGraph`, and nothing clashes because no copy is made. `Copies` then takes the user's `o@strip` for a copy, renames it `o` and so replaces the graph material `o` in the export. That is the rename the person ruled out ("nothing is replaced"), with no refusal and no warning. Probe in a scratch copy (`ProbeRv83Tests.Probe_an_unused_mesh_reading_material_renames_a_users_at_material`): the strip has `worn` (curvature occlusion, worn by no face) and a red material renamed `worn@strip` in the saved file and opened again, on faces 0-3. The export answered "1 material 'worn'", and the .glb's one material is named `worn`, not `worn@strip`. Requiring that `Resolve` actually made the copy (that mesh `n` wears `o`) would leave such a name alone. The four new `GraphExportTests` all have `o` worn on the mesh.

**Outcome:** —

### The render retry is tested on a copy of `RenderTiles`, not on render, so reverting its one wiring line passes every suite
- **Where:** `tests/Carve.Server.Tests/RenderBakedFrameTests.cs:46-52` (`BakeAndRender`, "What render's `RenderTiles` does"); the code it stands for, `src/Carve.Server/RenderTool.cs:580` and `:706`
- **Severity:** Minor
- **Rubric:** 2. A test for each thing fixed; `### As built` true of the code
- **What is wrong:** The three retry tests drive `SheetVersions.Baked` with a restatement of `RenderTiles` written in the test, on the linked pump. Putting line 580 back to `SheetVersions.OnOneMesh(RenderTiles, f => f.Version, SheetAttempts)`, the code before T-218, takes the retry out of render and leaves every one of these tests passing. `RenderGraphRefusalTests`, the one real render, returns the clash and no-UV errors, and those come back the same with or without the retry. T-218's As built says the tests were "seen failing with the retry disabled" and lists only the `uvs` look under "Not checked" as exercised through the helper alone. The same is true of the shaded path. By reading; I did not revert the line in a scratch copy.

**Outcome:** —

### The export-cancellation test bakes curvature at 2048 twice and takes 6 s in a `unit` class
- **Where:** `tests/Carve.Server.Tests/GraphExportTests.cs:176-194` (`An_export_named_in_notifications_cancelled_writes_no_glb`, size 2048 at `:180`); the class's `[Trait("Tier", "unit")]` at `:10`; the rule at `CLAUDE.md` ("`unit` means about 1 s or less per test")
- **Severity:** Minor
- **Rubric:** 6. Tests are placed (the `Tier` trait)
- **What is wrong:** In a scratch copy, Debug, run alone with the probes: `Passed ... An_export_named_in_notifications_cancelled_writes_no_glb [6 s]`. The second export bakes the whole 2048 curvature map, and the test also waits 1 s in two delays. RV-82 had this same fault in `graphs-unit` (tests moved to `graphs-regression` by T-217). The milestone file records no exception for it. The test can still fail without the fix with a smaller second bake, or it can move to a `regression` class.

**Outcome:** —

### `Unchanged` compares the files at two instants, not with the bytes the bake read, so an A, B, A rewrite caches B's image under A's key
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:78-88`; used at `src/Carve.Server/GraphBakes.cs:122` and `:198`
- **Severity:** Nit
- **Rubric:** 1. Correctness (the cache key)
- **What is wrong:** `Rgba` loads the graph and its nested files again after `Read` hashed them. `Unchanged` hashes them a third time after the bake. Suppose a file goes from A to B between `Read` and `Rgba`'s `Load` and back to A before `Unchanged`, as a `save`, `pop_snapshot` and `save` cycle in patina would do it. B's image is then cached under A's key, which is RV-82's fault in a narrower window. A file rewritten repeatedly with changing bytes is otherwise only never cached, which is safe. Hashing the bytes the evaluator itself loads would close it. By reading.

**Outcome:** —

**Covered:** I read whole RV-82's first two sections, T-217's and T-218's files, T-222, `GraphSlotBake.cs`, and `GraphBakes.cs` lines 40-219. I read the fix diff of `Carve.Graphs` (`MeshNodes`, `MeshMaskNode`, `MeshCurvatureNode`), `Patina.Graphs` (`GraphValidator`, `NodeType`, `GraphEvaluator`), `ParameterOverrides`, `ToolSupport`, `SheetVersions`, `RenderPump`, `RenderQueue`, `RenderSystem`, `RenderTool` (both retry paths), `ExportTool` (with `Run` and `Copies` whole), `GraphSlotProblem`, `CheckTool` and `MaterialTools`. Of the tests I read the new `GraphExportTests`, `GraphMaterialErrorsTests` and `PieceExportToolTests` tests, the head of `RenderBakedFrameTests`, the `BrokenCopiesTests` split and the new parameters test. Every line removed from `MeshNodesTests` and `BrokenCopiesTests` was compared with the regression classes: nothing was dropped but the deleted timing test and the replaced parameters test. I also checked `tests.json`'s `graphs-unit` and `graphs-regression` against every sample path and linked item in `tests/Carve.Graphs.Tests`, and found nothing missing. In a scratch copy (`%TEMP%\rv83-opus1`) I built and ran, unit tier only, my two probes, the cancellation test and the four `RenderBakedFrameTests`; all 7 passed, with the times and outputs quoted above. Checked and sound: the key for every slot and output kind, colour and grey, and the `uvs` image's key (base colour's packing, so its old suffix is not needed); the relative-path hash (other folders' nested files differ in their bytes, so their keys differ); `CheckParameters` on driven and nested values, named as expanded, and asked only when no range fault was found; the clash refused in `Resolve` before any bake, on the piece, kept-level and render paths (clips mode and the `uvs` look make no copies); no `.glb`, partial file or cache entry after a cancel; the frame mark passed from the frame thread through the request's completion; and the retry bounded at 3 inside `OnOneMesh`'s 3. I did not get to: running `graphs-regression`, or timing the whole of `server-unit`; reverting each fix hunk in a scratch copy (the "would fail without" claims were judged by reading, apart from the probes); check's warning for a kept level's clash on a part whose live mesh does not wear the material; and the `server-regression` and `tests.json` inputs beyond the graphs suites.

## Findings: RV-82's answers on the descriptions, patina and the acceptance, and T-219's, T-220's and T-221's commits (claude-opus-5-5)

| RV-82 finding | Member | Answer |
|---|---|---|
| `reads_mesh` ... is explained nowhere | T-220 | holds |
| render's description no longer holds T-165's image-graph sentences | T-220 | holds |
| check's and select's descriptions say there is no delete | T-220 | holds |
| export's description lost a space | T-220 | holds |
| The count of `help` entries and T-207's figures | T-220 | holds |
| Nothing holds the open and define_material entries | T-220 | holds |
| B-10's default-answer digests do not reach a holed face | T-220 | holds |
| A mesh node with a bad parameter is reported as evaluating | T-217, T-219 | holds |
| A graph that nests a mesh-reading graph gets no note | T-219 | does not hold (check's text; the note holds) |
| The scenario's break cannot fail on its own | T-219 | does not hold |
| The worked-example test only validates the examples | T-219 | holds |
| swatch's mesh note is in no unit test | T-219 | holds |
| `CheckSamplesTests` skips by file name | T-219 | holds |
| As built leaves open what patina's output gained | T-219 | holds |
| The acceptance crevice set counts face 99 | T-221 | holds |
| Suite inputs miss sample files | T-217 | holds |
| Two graphs-unit tests take 21 s and 12.7 s | T-217 | holds |
| CLAUDE.md says the acceptance grip is checked "against the clean stand-in" | T-220 | does not hold |
| README's list of unit suites leaves out the new graphs-unit | T-220 | holds |
| Stage 3's restore is said to check for a cache hit | T-221 | holds |
| T-168's round 2 bake times mark one 512 bake as 1024 | T-221 | holds |

### The reworked title-note break still cannot fail on its own: it subtracts the plain title's extent from itself
- **Where:** `scenarios/Carve.Scenario.Patina/MeshNodesStage.cs:75-81` (the break), `:83-85` (`CheckTitleNote`); `docs/tasks/T-219-rv-82-patina-s-findings.md:97-103`
- **Severity:** Major
- **Rubric:** RV-82 answer does not hold: The scenario's break cannot fail on its own
- **What is wrong:** The break is now `CheckTitleNote(probe, plain, plain)`. `CheckTitleNote` tests `extent - plain >= NoteWidth`, so the break tests `plain - plain >= 80`, which is `0 >= 80`. That is false for every capture, including a blank one, a title cut short, or a mis-measured title. The probe therefore fails whatever the pixels hold, and `checks.Check(probe.Failures.Count > 0, ...)` passes by arithmetic. The verified run's "only 0 px past the title with no note (258)" (T-219 As built) shows only that x minus x is 0. The finding pointed at this exact flaw: the old break negated a looser form of the check "on the same two numbers". It asked for the real rule to be run on two different titles that have no note, for example `plain` against a second capture of the plain sheet, so that the outcome rests on what was captured. The fix uses one number twice, which is weaker than before. The As built's "What it does not prove" lists other limits but not this one.

**Outcome:** fixed

### check's new one-value reason says carve's check judges the real maps, but carve's check evaluates no graph, and the reason is also given to outputs that are flat for a real reason
- **Where:** `src/Patina.Server/CheckTool.cs:133-135` (`NeutralFix`), `:123` (which outputs get it); `src/Patina.Graphs/Evaluation/GraphEvaluator.cs:76-89` (`TypesReaching`); against `src/Carve.Server/CheckTool.cs:22,57`
- **Severity:** Major
- **Rubric:** RV-82 answer does not hold: A graph that nests a mesh-reading graph ... and check calls its mask broken (the person's ruling asked for "the true reason in its text"); correctness of the fix
- **What is wrong:** `NeutralFix` ends "carve gives the real maps when a material names the graph, and its check there judges them." carve's `check` description says "check casts no rays and evaluates no graph" (description and entry). No source under `src/Carve.Server`, `src/Carve.Graphs` or `src/Carve.Core` references `CheckRules`, `OneValueRule` or `one-value` (grep; only built `Patina.Graphs.dll` copies match). So the agent is told to rely on a later check that does not exist. Second, `TypesReaching` marks an output as soon as any mesh node lies upstream, whether or not that node can make the output vary. Probe on `PatinaHarness` in a scratch copy: `mesh_curvature` → `levels` with `in_low 0, in_high 0` → output `wear`. In carve that is 1 on every texel too, whatever the curvature. `check` answers `one-value` with "every texel is 1.000 (255) ... Fix: none here: it reads a mesh input, which in patina is a neutral stand-in, so it is flat for that reason; ...". So a mask that is broken for a real reason is told it needs no fix, and no carve tool will say otherwise. A `blend` with factor 0 over a mesh node is the same case. The person was told that such an output keeps `one-value`, which it does, but the text is then the misleading one. `Check_gives_one_value_the_neutral_reason_only_for_an_output_that_reads_a_mesh_node` covers only a flat output with no mesh node upstream. T-219's As built line "`OneValueRule` and `CheckRules` are untouched, so carve's check is unchanged" implies carve's check uses them, and it does not.

**Outcome:** fixed

### The test that the crevice set holds only drive side faces runs in no suite, and CLAUDE.md says the project's tests skip without a round
- **Where:** `tests/Carve.WeatheringAcceptance.Tests/WeatheringAcceptanceTests.cs:19` (`Tier` `regression` on the class), `:316-326` (`The_crevice_set_holds_no_texel_of_a_face_that_is_not_a_drive_side`); `tests.json:108-126`; `CLAUDE.md:123`
- **Severity:** Minor
- **Rubric:** tests are placed; honest `**Verify:**` lines; CLAUDE.md kept current
- **What is wrong:** The new test is a plain `[Fact]` on `samples/wrench.carve` and a blank bake, quick and with no device. Its class is `Tier` `regression`, and no suite's `run` names `Carve.WeatheringAcceptance.Tests`: the project is only a `dotnet-closure` input of `server-unit`, whose command runs `tests/Carve.Server.Tests`. Only `run-acceptance-check.ps1 -Weathering` and `run-weathering-negatives.ps1` run it, by hand, against a round folder. So `verify` never runs it. An edit to `wrench.carve`'s drive, to `DriveFaces` or to `DriveSides` would pass every suite and change which texels the crevice check measures. T-221's `**Verify:**` (server-unit, scenario-weathered) runs neither the test nor the crevice code. The As built says so ("no unit suite runs"). The CLAUDE.md row still says the project "Reads `CARVE_WEATHERING_RUN`, skipped when it is unset". That is now untrue of this test, which needs no round and is not skipped.

**Outcome:** fixed

### The milestone file's rulings leave out the three rulings of the review round, and its crevice ruling states the set that was replaced
- **Where:** `docs/milestones/M-29-graphs-on-a-mesh.md:205-233` ("Decided while it ran"), `:227-229`
- **Severity:** Minor
- **Rubric:** tracker hygiene (the milestone's file holds the person's rulings)
- **What is wrong:** Three rulings of 2026-11-25 are recorded only in member files, and none of them is in "Decided while it ran":
  - the crevice set is the drive's four visible side faces (T-221:32-40, T-168's new paragraph), declined: the first set with face 99 removed;
  - `check` keeps `one-value` for a mesh-reading output with the true reason, declined: withholding it (T-219:61-63);
  - the `<material>@<mesh>` clash is refused (T-218:39).
  
  The milestone's crevice bullet (`:227`) still reads "the drive's texels within 0.004 m of the edges of its two ends", the set the person replaced. `grep 2026-11-25` over the milestone file finds only that bullet, the verify line and the test report.

**Outcome:** —

### Retrospective lines the fixes have made untrue
- **Where:** `docs/milestones/M-29-graphs-on-a-mesh.md:253-255`, `:295-298`, `:326-328`, `:346-347`, `:352-353`
- **Severity:** Minor
- **Rubric:** tracker hygiene (the retrospective true of the code)
- **What is wrong:** Each of these was written before the review and is now false:
  - "The planning calls that held: ... the cache's key" (`:254`). The key left out the slot, RV-82's double-found Major, and T-217 changed it.
  - "The acceptance checks were ruled three times ...; and the crevice set narrowed to the drive's ends" (`:295-298`). It was ruled a fourth time, to the drive's four side faces, which are not its ends.
  - Never verified: "cancelling mid-bake (export bakes with no cancellation token)" (`:326-327`). `ExportTool.Run` now takes a `CancellationToken`, passes it to `bakes.Resolve` and checks it before writing (`src/Carve.Server/ExportTool.cs:187`, T-218).
  - Never verified: "A mesh node inside a nested graph file: patina's stand-in note does not look into one" (`:328`). It does now (`ToolSupport.MeshNodeNames`, T-219), and T-219:44-45 says the orchestrator would correct this line.
  - Known and left: "Six descriptions sit within 20 characters of the 3,686 budget" (`:346`). The served text at HEAD has five (define_material and get_mesh 3,678, project_uvs 3,682, define_mesh 3,684, unwrap 3,685). It had seven at `4138e51`.
  - Known and left: "A shortcut in T-163: an edit between render's bake and its frame makes the frame thread draw that model with empty graph slots" (`:352-353`). T-218 now bakes and draws again, up to 3 tries (`SheetVersions.Baked`).

**Outcome:** —

### T-168's table and comparison still call the old figures "the ruled set", and the new paragraph misnames them and says the table is below
- **Where:** `docs/tasks/T-168-graphs-on-a-mesh-acceptance-run.md:113` (the "Crevice (margin 10, the ruled set)" row), `:143` (the new paragraph's last sentence), `:145` ("ruled set: 49.143, 24.862")
- **Severity:** Nit
- **Rubric:** an honest record
- **What is wrong:** The per-round table still labels 49.143 and 24.862 (10,357 and 2,604 texels) "the ruled set", and the Comparison line repeats "ruled set: 49.143, 24.862". The set ruled now gives 40.297 and 15.206: both rounds' `acceptance/numbers.tsv` on the shared drive agree, at 3,939 and 980 texels. The new paragraph says "The figures in the table below and in the Negatives table are those of the set first ruled". The per-round table is above it, not below. And those figures are of the set as coded, with face 99. The set first ruled gives 48.1 and 24.6, as the same paragraph says two sentences earlier. The negatives' new figures match `%TEMP%\carve-t168-negatives\*\acceptance\numbers.tsv` (reference 67.530, no-wear 0.077, no-dirt −43.499).

**Outcome:** fixed

### CLAUDE.md still lists the grip check as part of the steel's base colour bake
- **Where:** `CLAUDE.md:123`
- **Severity:** Nit
- **Rubric:** RV-82 answer does not hold: CLAUDE.md says the acceptance grip is checked "against the clean stand-in"
- **What is wrong:** The finding said ruling 4 holds the grip "to what its faces wear in the saved file ..., and that is not the steel slot's bake". The row now reads "the steel's base colour bake (hard-edge wear, drive crevice (...), grip held to what its faces wear in the saved file, against the starter file's grip ...)". The wording of the clause is fixed, but the grip is still listed inside the steel bake's parenthesis, as one of that bake's checks.

**Outcome:** fixed

### README's list of regression suites leaves out the new graphs-regression
- **Where:** `README.md:53`
- **Severity:** Nit
- **Rubric:** the README kept current
- **What is wrong:** T-217 added the suite `graphs-regression` (`tests.json:52`, no GPU) and named it in CLAUDE.md. README's line reads "regression, no GPU: `patina-regression`." T-220 corrected the unit line two lines above, but the two members ran side by side, and neither added the new suite to this line.

**Outcome:** fixed

### T-220's As built names a commit that is not on the branch
- **Where:** `docs/tasks/T-220-rv-82-the-descriptions-and-docs-findings.md:64`
- **Severity:** Nit
- **Rubric:** tracker hygiene (`### As built` true of the code)
- **What is wrong:** It says "All in 4a69443e", and commit `8a873264` is titled "As built names the rebased commit". But `git merge-base --is-ancestor 4a69443e HEAD` fails: the change on `milestone/M-29` is `45f7b023`.

**Outcome:** fixed

**Covered:** I read RV-82's three sections whole, T-219's, T-220's and T-221's files whole, T-217's As built, the milestone file's rulings and retrospective, the T-219 source and scenario diff, the T-220 commit, the T-221 test, scenario and record diffs, the new and changed patina tests (`MeshGraphTruthTests`, `MeshNodeToolTests`, `NodeToolTests`, `PatinaHarness`, `CheckSamplesTests`), and the tests.json diff. I ran these in scratch copies under `%TEMP%\m29-rv83-o2`:
- a `ToolHarness` dump of `tools/list` and every `help` entry at `4138e51` and at HEAD, and a sentence script over both. Nothing was lost except the intended rewrites; no schema changed; no description is over 3,686 (the longest is 3,685); exactly check, define_material, export, get_mesh, render and select changed, matching the pin's comment;
- HEAD's B-10 digest theory on a build of `origin/main` (b02b7b2): 8 of 9 pass, gatehouse and arm included, and barrel-weathered cannot open there. `c518cce` has no `include`. Gatehouse has 2 holed and 17 deleted faces, arm has a target and barrel-weathered has graph slots;
- a `PatinaHarness` probe of a missing, a broken, a two-deep and a subfolder-nested mesh graph (each is reported as a fault, or carries the note named `g/g/edges`), and of a mesh output clipped flat by `levels`.

I compared both rounds' `numbers.tsv` on `G:` and the negatives' folders with the records. I checked the T-218 sentences in render's and export's entries against `SheetVersions`, `GraphSlotProblem.Clashes` and `ExportTool.Run` by reading. I did not revert hunks to see the named tests fail; I reasoned it through instead. I did not run any scenario or regression test. I did not verify T-219's frame-hash comparison or T-221's 58-hash comparison, or open any image.

## Summary

Round 2 checked the answers of RV-82 against the code and reviewed the fix commits (54 files, about 1,500 added lines
since `4138e51`). Two reviewers, split as the fix members were. `origin/main` had not moved.

31 of the 36 answers hold. Five do not: a frame with empty graph slots can still be drawn; a user's `o@mesh` material
is still renamed on export in one case; the scenario's title-note break still cannot fail on its own; `check`'s new
text for a mesh-reading output is not true; and one clause of CLAUDE.md. 15 new findings: 3 Major, 7 Minor, 5 Nit.
What held: nothing was lost from the descriptions and every one is at 3,686 or less; the acceptance numbers match the
round folders; the cache key tells the two packings apart; export cancellation, the mesh-caused endings and the
validation of mesh parameters.
