---
description: >-
  Review of M-29
---
# RV-82 — Review of M-29

**Status:** Open

**Reviews:** M-29

**Round:** 1

**Previous:** —

**Diff:** fbfacb73f80f40b24230154345d7a4eaac2d14aa against origin/main

**Reviewer:** five reviewers, each claude-opus-5-5 (Claude Opus 5.5), split by area: the Core and Carve.Graphs; the server's bake cache, render, export and errors; the descriptions, help and get_mesh; patina, the sample graphs and the taught examples; the scenarios, the acceptance and the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the Core and Carve.Graphs (claude-opus-5-5)

### The cache key leaves out the slot, but the bake's bytes depend on it, so two slots of one graph output share an entry
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:77-96` (`Key`: files' hash, output, size, and the mesh only), against `:112-130` and `:146-151` (`Rgba` packs by `PackingOf(slot, ...)`); used as the whole key at `src/Carve.Server/GraphBakes.cs:104` with the bake at `:113`
- **Severity:** Major
- **Rubric:** 1. Correctness (the cache key)
- **What is wrong:** `PackingOf` packs one output differently by slot. For example, `roughness` in `emissive_texture` or `base_colour_texture` is grey RGB (`OutputRole.Occlusion`), and in `metallic_roughness_texture` it is G, with B set from `metallic`. `Key` takes no slot, and `GraphBakes.Resolve` caches each slot's PNG under `Key(f, g.Output, g.Size, ...)` alone. So a material whose emissive (or base colour) slot and metallic-roughness slot both name `{graph, roughness, size}` gets whichever bake ran first in both slots. I wrote a probe in a scratch copy (`Probe_key_has_no_slot_but_the_bake_depends_on_it`, on `samples/graphs/planks.texgraph`, `roughness`, 64). It gives equal keys and, for the two slots, 8,192 of 16,384 RGBA bytes different: emissive texel 0 is `152,152,152,255` and metallic-roughness is `255,152,0,255`. The server's own comment at `GraphBakes.cs:187` ("The key has no slot, and this look packs differently from a metallic-roughness slot") appends `:image` for the `uvs` look only, and leaves the render and export path open. T-163's Scope lists the key's contents without the slot, so this is a gap in the spec as well as in the code. The test `The_key_changes_for_each_edit_a_mesh_reading_slot_depends_on` never varies the slot. Across the line: the fix belongs in either `Key` or `GraphBakes`.

**Outcome:** fixed

### `graphs-unit` reads the planks sample and its PNGs, but neither is in the suite's inputs
- **Where:** `tests.json:31-49` (suite `graphs-unit`); the reads are at `tests/Carve.Graphs.Tests/GraphSlotBakeTests.cs:20, 44, 51, 141`
- **Severity:** Major
- **Rubric:** 4. Honest Verify scope
- **What is wrong:** `GraphSlotBakeTests` reads `samples/graphs/planks.texgraph` and compares against `samples/graphs/planks/<output>.png` byte for byte (`A_graph_with_no_mesh_node_gives_the_png_patina_exports`, and also the slot table and the key test). The suite's inputs list `samples/wrench.carve` and no `samples/graphs/**`. A change to the planks graph or its PNGs therefore carries a cached `graphs-unit` pass instead of re-running it. The test that most directly holds "a graph without a mesh node gives patina's export bytes" is the one that goes stale. The dependency came in with T-163 (`6b57f12`), which added the tests and did not add the inputs. (`patina-unit` has `samples/graphs/**`, but that does not make `graphs-unit` re-run.)

**Outcome:** fixed

### `Validate` passes mesh-node parameters that every bake then refuses, so `define_material` and `check` accept a graph that never renders
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:159-164` (`Validate`), against `src/Carve.Graphs/MeshNodes.cs:44-50` and `MeshMaskNode.cs:28-34` (refused only inside `Evaluate`)
- **Severity:** Minor
- **Rubric:** 1. Correctness (refusals); 7. Tracker hygiene
- **What is wrong:** `Validate` claims "every fault of the graph". However, `mesh_curvature` `width: 0`, `mesh_occlusion` `reach: 0`, and a `mesh_mask` naming both or neither of `group` and `material` are `MeshParameterException`s, thrown only during evaluation. I wrote a probe in a scratch copy (`Probe_validate_passes_parameters_that_fail_every_bake`): a graph with `mesh_curvature` `width: 0` and a bare `mesh_mask`. It gives `Validate: passed` and then `Bake: MeshParameterException: nodes['w'].params['width']: is 0, but must be above 0 metres`. Nothing in `src` calls `MeshMaskNode.Selection` or `Positive` outside evaluation, and `MaskNames` skips a mask with neither name. T-166's Scope uses exactly this case as its example of a graph fault that `define_material` must refuse (`nodes['wear'].params['width']: is 0, ...`). As built, the refusal comes later, at `render` or `export`, with the right words. T-166's As built says `Validate` covers "every fault" and does not mention these cases.

**Outcome:** fixed

### Seven `unit` tests run well over the tier's 1 s, the slowest at 22 s, and the suite exceeds its estimate
- **Where:** `tests/Carve.Graphs.Tests/MeshNodesTests.cs:89-115` (`Each_node_equals_its_bake_at_every_tile_size_and_a_zero_cap(1024)`), `:270-293`, `:199-208`, `:226-242`; `BrokenCopiesTests.cs:55`; `tests.json:48` (`est-seconds: 40`); the rule is at `CLAUDE.md:165-167`
- **Severity:** Minor
- **Rubric:** 6. Tests are placed
- **What is wrong:** I ran the whole suite once in a scratch copy, Debug, on this machine. Total 46 s against `est-seconds` 40. Per test: node-equals-bake at 1024 took 21.9 s; `Times_each_map_on_the_wrench` 14.2 s; the occlusion crevice test 3.7 s; broken copy "curvature read as occlusion" 2.4 s; raised hoops 2.1 s; node-equals-bake at 256 1.6 s; plain barrel occlusion 1.2 s. Every class carries `Tier` `unit`, and `unit` means about 1 s or less per test. T-161's Scope asked for 1024 in `graphs-unit`, but the milestone file records no exception to the tier rule. These are the CPU-heavy 1024 occlusion bakes that B-11 found slow under load in the server suite.

**Outcome:** fixed

### `Read` hashes the bytes once and `Rgba` reads the files again, so an edit during a bake is cached under the old content's key
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:48-71` (`Read` hashes) against `:116` (`Rgba` calls `Load(graph.Path, ...)` again, and the `graph` node reads nested files again); used at `src/Carve.Server/GraphBakes.cs:102-113`
- **Severity:** Minor
- **Rubric:** 1. Correctness (the cache key)
- **What is wrong:** The key is taken from the bytes `Read` saw, and the image from whatever the evaluator reads seconds later (a bake at 1024 takes 6 to 14 s). Suppose a graph file changes in that window, for example a `patina` `save`. The new content's image is then stored under the old content's key. While the file stays new, the next render hashes differently and rebakes. If the file goes back to the old bytes (a `pop_snapshot` and `save`, or a revert), the old key hits and serves the image of the edited graph until it is evicted. Found by reading, not run.

**Outcome:** fixed

### `Times_each_map_on_the_wrench` asserts nothing and writes into the shared temp folder
- **Where:** `tests/Carve.Graphs.Tests/MeshNodesTests.cs:269-293`
- **Severity:** Nit
- **Rubric:** 2. Tests cover the Scope (a test that cannot fail)
- **What is wrong:** Its summary says "Not a check". It produces As built's times and writes `%TEMP%/carve-graphs-times.txt` on every run, which costs 14 s of the unit suite (above). It cannot fail on anything the code decides. It would sit better as a script or a skipped test.

**Outcome:** fixed

### The files' hash takes each file's absolute path, so the same graph in a moved folder misses the cache
- **Where:** `src/Carve.Graphs/GraphSlotBake.cs:65`
- **Severity:** Nit
- **Rubric:** 1. Correctness (the key changes with "nothing else")
- **What is wrong:** `hash.AppendData(file + "\n" + bytes.Length + "\n")` puts the absolute path into the key. Moving a project folder, or opening the same model from a copy, changes every key with no change to the bake. This is only a needless rebake, never a wrong image. A path relative to the top graph's folder would keep the nesting structure without the location.

**Outcome:** fixed

**Covered:** I read whole: `GraphSlotBake.cs`, `MeshBakeSource.cs`, `IMeshMaps.cs`, `MeshNodes.cs`, the four node files, `MeshMapsExceptions.cs`, `Carve.Graphs.csproj`, and every test in `tests/Carve.Graphs.Tests`. I read the diff hunks, with their context, of `Material.cs`, `CarveFile.cs` (writer version choice, `HoldsParts`, `ReadMaterial` and `ReadGraphSlot`, the key lists), `BakePadding.cs` (`FillUncovered`, and `Pad`'s head), `BakeMasks.cs`, the `Patina.Graphs` text kind, and `GraphSlotFileTests.cs` with the other Core test diffs. I also read T-161's and B-11's files whole, the Scope parts of T-160, T-163 and T-166 that concern my part, the milestone's retrospective, and `tests.json`'s `graphs-unit`, `core-unit` and `patina-unit`. Run in a scratch copy (`%TEMP%\rv82-core`): `graphs-unit` whole, 40 of 40 passing, with per-test times; and two probe tests, the slot key and `Validate`. What I checked and found sound: `MeshBakeSource`'s locking (one lock over every map and layout, and `Mask`'s nested `TryAdd` runs under it on the same thread); the neutral fill before padding; padding at the 64 minimum (8 ≤ 32); the mesh-reading key covering vertices, UVs, seams, groups, materials, shading, hard edges and occluders; the v10 writer and reader, including another drive and an absolute path in the file; older versions written byte for byte (by reading the writer, and through the committed-sample test); an old reader refusing v10 on its version; the `@` rule (the faces' side is held by `GraphBakesTests`); and the engine-free guard. I did not get to: `CarveFile.cs` outside its hunks; the v9 SHA-256 baseline against `main`'s own build (not rebuilt); `Pad`'s envelope pass; `TextParamTests` beyond its asserts; a concurrent-use probe of `MeshBakeSource`.

## Findings: the server's bake cache, render, export and errors (claude-opus-5-5)

### The cache key has no slot, so two slots that name the same graph output share one bake packed for whichever slot baked first
- **Where:** `src/Carve.Server/GraphBakes.cs:104-123` (key and lookup), `src/Carve.Graphs/GraphSlotBake.cs:77-100` (the key) and `:146-156` (the packing depends on the slot); the comment at `GraphBakes.cs:187` shows the gap was known for the `uvs` image and closed only there
- **Severity:** Major
- **Rubric:** 1 (correctness: the cache)
- **What is wrong:** `GraphSlotBake.Key(files, output, size, model, mesh)` hashes the files, output, size and mesh, but not the slot. `PackingOf` packs `roughness` as `Roughness` (G, with `metallic` in B) for `metallic_roughness_texture`, but as grey RGB (`Occlusion`) for `base_colour_texture`, `emissive_texture` and `occlusion_texture`, all of which the slot table accepts. So a material, or two materials, with `{graph: g, output: "roughness"}` in `metallic_roughness_texture` and in any of those three slots at the same size get one cache entry. The slot baked second takes the first one's PNG, in render and in export alike, so D-6 does not catch it. I proved it in a scratch copy with a unit-tier probe on `ToolHarness`: a worley graph with `roughness` and `metallic` outputs, and material `m` with both `base_colour_texture` and `metallic_roughness_texture` set to `{graph:"rough.texgraph", output:"roughness", size:64}`. `Resolve` baked 1 slot, not 2. The drawn `metallic_roughness_texture` bytes equal the base colour's (`mr==base True`), while `GraphSlotBake.Png` for the two slots gives different bytes (`directMR==directBase False`). Which slot is wrong depends on order, so across materials a base colour can also come out as the packed G/B image. No test has two slots on one output. Adding the slot, or its packing role, to the key fixes it.

**Outcome:** fixed

### `DrawnOf` says no render draws empty graph slots, but a render whose frame is served on a newer model returns exactly that frame
- **Where:** `src/Carve.Server/GraphBakes.cs:48-53` (the claim "No render draws that: render resolves before it queues"), `src/Carve.Server/RenderTool.cs:552-566, 602-611`, `src/Carve.Server/RenderPump.cs:190-200`; the record in the milestone's "Known and left" ("A shortcut in T-163")
- **Severity:** Minor
- **Rubric:** 7 (tracker hygiene), 1 (threading)
- **What is wrong:** `render` resolves `store.Current.Model` (M1), then posts the frame. The pump serves "the mesh current at its post or a newer one". If a `move`, `define_material` or `undo` lands in between (M2), the pump calls `ShowMesh(store.Current)`. `DrawnOf(M2)` finds no resolution (`Peek(M2)` is null) and draws M2 with every graph slot empty. That frame is served and returned to the agent, and its caption carries M1's `baked:` list. A single frame gets no version check (`ShiftedIdsRefusal` only runs when ids are shifted), and `SheetVersions.OnOneMesh` only compares tiles with each other. So the code comment is false. The retrospective's wording ("the frame thread draw that model with empty graph slots until the next bake lands") leaves out that the agent's own render returns the blank-slotted frame, and that "the next bake" comes only with the next render or export. The code has no `shortcut:` comment for this limit. The one `shortcut:` in T-163's code (`GraphBakes.cs:278`, the costliest node named by type) states its limit correctly.

**Outcome:** fixed

### A per-mesh copy's name can be a material the model already has, and export then renames that material's faces away
- **Where:** `src/Carve.Server/GraphBakes.cs:143-156` (copies named `{m.Name}@{name}` and `{m.Name}@{part}@{level}` with no check against existing names), `src/Carve.Server/ExportTool.cs:306-325` (`Copies`: anything named `<graph material>@...` counts as a copy, and `Base` is cut at the first `@`)
- **Severity:** Minor
- **Rubric:** 1 (the copies' names)
- **What is wrong:** By the planning ruling, a file with an `@` name still opens and its faces can still be given it. A model with a plain material `iron@strip` (an older file) and a mesh-reading graph material `iron` on mesh `strip` therefore collides. `Resolve`'s `WithMaterial` replaces the user's `iron@strip` with iron's copy in the drawn model, and `Copies` treats the user's faces as copy users and renames them `iron`. Probe (scratch copy, unit tier): faces 0-2 `iron` (curvature occlusion graph), face 3 `iron@strip` (base colour `#FF0000`). In the drawn model, `iron@strip` had base colour `#FFFFFF` and a baked `MaterialTexture` occlusion. The exported .glb had one material, `iron`, and the red was gone with no warning. Mesh names may contain `@` (`Model.NameProblem` refuses only `:`), so a mesh `hull@1` and part `hull`'s kept level 1 both make `iron@hull@1`, and one bake replaces the other in render. A graph material whose own name has `@` (only possible from a hand-edited file) would be renamed back to the text before its first `@`. None of these cases is tested.

**Outcome:** fixed

### Bake memory is held outside the 512 MB cap: every resolved model in the undo history keeps its own copies of its bakes
- **Where:** `src/Carve.Server/GraphBakes.cs:123` (`new MaterialTexture(g.Path, [.. png], ...)`, a fresh copy per slot on every `Resolve`, hits included), `:27` and `:166-171` (the `ConditionalWeakTable<Model, Drawn>`), `:246` (the count); `ModelStore.UndoDepth` = 100
- **Severity:** Minor
- **Rubric:** 1 (the cache: growth and byte count)
- **What is wrong:** The cap covers only `_entries`. Each `Resolve` builds the drawn model with a new `ImmutableArray` copy of every slot's PNG, even on a cache hit, and keeps it in `_drawn` for as long as the key `Model` lives. `ModelStore` holds up to 100 earlier models for `undo`. A session that renders after each edit therefore holds up to 100 sets of the bakes, none of them counted and none evicted. By T-163's numbers that is about 6 slots per barrel model at 1024, so several MB per state, and 16 times that at 4096. The count also charges each entry `size*size*4` of "decoded" bytes the cache never holds (the decode is `PbrMaterials`'), so `Bytes` is neither the cache's real size nor the process's. Eviction cannot drop a bake in use, since every user holds a copy, and a single entry over the cap is kept, by design. No test evicts (listed).

**Outcome:** skipped: filed off-milestone as T-222 by the person's choice (2026-11-25); nothing drawn is wrong

### Export cannot be cancelled: a cancelled or timed-out export bakes on and then writes the .glb
- **Where:** `src/Carve.Server/ExportTool.cs:123` (no `CancellationToken` in `Run`), `:165` (`bakes.Resolve(model, CancellationToken.None)`), `:184-195`
- **Severity:** Minor
- **Rubric:** 1 (cancellation), 7
- **What is wrong:** The retrospective lists this under "Never verified" ("cancelling mid-bake (export bakes with no cancellation token)"), but the code settles it: nothing can stop an export's bake. A client that cancels, or gives up at its own timeout, has been told the call failed. The server goes on baking (6 to 14 s a material at 1024 in Debug, by B-11) and then writes the .glb and any limits file. If the agent retries in the meantime, two exports run at once toward one path, and the second either finds the file there or overwrites it. T-163 made render's bake honour its token between tiles and maps; export, which bakes the same slots, takes none. This is a known gap, wrongly recorded as unverified.

**Outcome:** fixed

### A bake that fails on the mesh tells the agent to redefine the material or restore the file, which is the wrong fix
- **Where:** `src/Carve.Server/GraphSlotProblem.cs:60-61` (`Failed`), used by `GraphBakes.cs:91-94, 115-118`
- **Severity:** Minor
- **Rubric:** 1 (errors: what would be right)
- **What is wrong:** Every `render` and `export` bake failure ends with "; the model is unchanged: define the material again with a graph that bakes, or restore the file". That includes a `MeshLayoutException` (no UVs, overlapping UVs) and a `MeshMaskException` (a group or material the mesh lacks). For those causes the graph and file are fine, and the cause's own text says to unwrap, set UVs or move islands. The render error therefore gives two contradictory fixes, and the trailing one is wrong. The wording is pinned only for file and fault causes (`GraphMaterialErrorsTests.cs:138-145, 170-172`, `GraphBakesTests.cs:118`). No test renders or exports a mesh cause and reads the message.

**Outcome:** fixed

### `PieceExport.Select` now carries the model's materials, a real fix for a pre-existing failure, with no test
- **Where:** `src/Carve.Server/PieceExport.cs:34`, `src/Carve.Server/ExportTool.cs:246-251`; T-164 As built, line 77
- **Severity:** Minor
- **Rubric:** 2, 3
- **What is wrong:** The change is right: before it, `Model.Create` was given no materials, so a piece export of any mesh wearing a material (PNG or graph) could not be built, and it threw from `Select`. After it, `Copies` runs over the piece's meshes only, which is the right rule. But this fixes behaviour outside the graph feature, for every textured piece export, and no test in `tests/Carve.Server.Tests` exports a piece with a material of either kind. No test file names `"piece"` together with `define_material`. A revert would pass every suite. The As built says so in passing ("a piece with a graph" is under Not checked). The pre-existing failure was not filed as a bug.

**Outcome:** fixed

### The server suites read sample files their `tests.json` inputs leave out
- **Where:** `tests.json:90-125` (server-unit), `:135-150` (server-regression); `tests/Carve.Server.Tests/GraphMaterialErrorsTests.cs:180, 190-191`, `GraphMaterialRenderTests.cs:65-67`
- **Severity:** Minor
- **Rubric:** 4 (honest Verify scope)
- **What is wrong:** server-unit runs `GraphMaterialErrorsTests`, which opens every `samples/*.carve` (12 files) and copies `samples/barrel.carve` and `samples/graphs/weathered-iron.texgraph`. Its inputs list four samples and nothing under `samples/graphs/`. server-regression's `GraphMaterialRenderTests` copies `samples/graphs/weathered-iron.texgraph`, `weathered-staves.texgraph` and its nested `planks.texgraph`, none of which is an input. A change to a sample graph, or a new sample `.carve` (`barrel-weathered.carve` is one), therefore lets both suites be carried rather than re-run.

**Outcome:** fixed

**Covered:** I read whole `GraphBakes.cs`, `GraphSlotProblem.cs`, `GraphSlotBake.cs` (to judge the key), `MaterialTools.cs` from `Run` on (`ParseGraph`, `LoadGraph`, `ReadGraph`, `set_material`), `ExportTool.cs` lines 150 to 357, `CheckTool.cs`'s graph path, `PbrMaterials.cs`, `PieceExport`'s change, `RenderPump.cs` lines 100 to 240, `RenderTool.cs` lines 425 to 782 (the shaded and `uvs` paths), and the three refactor commits' diffs. The refactor kept both callers' wording and checks: `ParseGraph` is the old body with the tool and the two nouns passed in, render's `Problem()` check is kept, and `open`'s `Missing` lists the same files. I also read `GraphBakesTests`, `GraphMaterialErrorsTests` (lines 120 to 202), `GraphExportTests` (head), `GraphMaterialRenderTests` (lines 80 to 204), the `CarveProcess` diff, T-163's file and T-164's As built. B-11's 120 s is used only on calls that bake at 1024, as ruled. T-166's `ThrowsAny` is sound: T-166 added the subclasses `GraphOutputException` and `GraphMissingException`, and each call still pins its message. The tiers are right (the process-starting classes are `regression`). I ran two unit-tier probes on `ToolHarness` in a scratch copy (`%TEMP%\m29-rv82-opus2`); both are quoted above. I did not read `GetMeshTool.cs`, `OpenTool.cs` beyond its diff, `BakeTool`, `GraphImageToolTests`, `GraphSlotToolTests` or `GraphExportLoadTests` in full, and did not check each T-165/T-166 Scope bullet against a test. I ran nothing that needs a device, so the race in the second finding comes from reading the code, not from a run.

## Findings: the descriptions, help and get_mesh (claude-opus-5-5)

### `reads_mesh`, in every graph slot that define_material and get_mesh answer, is explained nowhere an agent can read
- **Where:** `src/Carve.Server/MaterialTools.cs:32` (the field), against `src/Carve.Server/MaterialTools.cs:79` and `src/Carve.Server/GetMeshTool.cs:25` (the answer shapes)
- **Severity:** Minor
- **Rubric:** the descriptions as an agent reads them; correctness of a stated return shape
- **What is wrong:** `MaterialJson.Of` answers a graph slot as `{graph, output, size, reads_mesh}` (true, false, or null when the file cannot be read now), and get_mesh's `materials` uses the same JSON (held by `GraphBakesTests.cs:126-127`). define_material's description gives the result as "{graph, output, size (the one taken)} for a graph", and get_mesh's as "{graph, output, size} for a graph output". I searched every served description, every parameter schema and every `help` entry in a `tools/list` dump of HEAD taken on `ToolHarness` in a scratch copy: no text says `reads_mesh`, and neither does README.md. So an agent gets a field that nothing explains, and nothing tells it what null means. T-163's spec (`docs/tasks/T-163-...md:40`) has define_material and get_mesh report the field, so the omission started in T-163. This is across the line into the reviewers of render and errors, but the text that leaves it out is in my part.

**Outcome:** fixed

### render's description no longer holds T-165's image-graph sentences, against T-207's own rule, and its one `size` rule misleads for `image.size`
- **Where:** `src/Carve.Server/RenderTool.cs:187` (the description's `uvs`/`image` clause), `src/Carve.Server/RenderTool.cs:287` and on (the sentences now only in the entry); `docs/tasks/T-207-carve-s-tool-descriptions-under-claude-code-s-cut.md:54`, `:144-145`
- **Severity:** Minor
- **Rubric:** the Scope and its `### As built`; the descriptions as an agent reads them
- **What is wrong:** T-207's Scope says every sentence M-29 added about graph materials stays in a description, and its Done-when says "every M-29 graph-material sentence is still in a description (not only in `help`)". I took the sentences that are in the pre-T-207 description (`e4d6dd4`) but not on origin/main, and checked each against HEAD. Five of render's are only in the entry:
  - the graph output is evaluated with the mesh's real curvature, occlusion, masks and position;
  - any role or named output may be named, and how each one is drawn;
  - `size` defaults to the graph's own and is a power of two from 64 to 4096;
  - the caption form `'image weathered-iron.texgraph wear 256 px'`, and the `baked` text on first use;
  - the refusals (an output the graph lacks, a graph that cannot be baked for this mesh, an unknown key).

  What is left in the description is the clause "'image' (a PNG path, or a graph output {graph, output, size} evaluated on the mesh)". The only size rule the description gives is "'size' is 256, 512 (the default) or 1024", so an agent that does not call `help` has nothing to tell it that `image.size` follows a different rule. Nothing is out of an agent's reach, because all five sentences are in `help {"tool":"render"}`. But the As built does not record that these moved, and the Done-when it claims is not met as written. check's T-166 sentences are still in the description, shortened with the meaning kept, so they are not a fault.

**Outcome:** fixed

### check's and select's descriptions say there is no delete, yet `delete` exists and get_mesh points to it
- **Where:** `src/Carve.Server/CheckTool.cs:23` (and its `Reference`, `:59`); `src/Carve.Server/SelectTool.cs:39` (and `:66`)
- **Severity:** Minor
- **Rubric:** is anything in a description contradicted by another tool's description or by the code
- **What is wrong:** check's description says "There is no delete yet, so removing a vertex or a face means define_mesh with the lists you want." select's says "Ids never change and no edit removes anything yet, so a selection names the same elements after an edit." Both are false:
  - `delete` is a tool;
  - get_mesh's description says "A deleted vertex or face (see delete) is null in its slot";
  - my probe on `ToolHarness` ran `delete {"faces":[3]}` on the cube, and `get_mesh {"faces":[3]}` was then refused with "faces[3]: deleted".

  An agent that believes check will rebuild the mesh with `define_mesh`, which loses its groups and colours, instead of calling `delete`, which keeps every id. Both sentences are on origin/main (`git show origin/main:src/Carve.Server/CheckTool.cs` and `SelectTool.cs`, one match each), so M-29 did not write them. But T-207 rewrote both descriptions sentence by sentence and kept them in the served text and in the entries.

**Outcome:** fixed

### export's description lost a space: "the mesh's own:+Y up"
- **Where:** `src/Carve.Server/ExportTool.cs:23`
- **Severity:** Nit
- **Rubric:** tracker hygiene (`### As built` matches the code)
- **What is wrong:** On origin/main the text reads "Axes and units are the mesh's own: +Y up, ...". My sentence diff found it missing at HEAD only because the space after the colon is gone. T-207's As built quotes the sentence with the space, as the D-dup reason for one of define_mesh's drops (`T-207-...md:337`).

**Outcome:** fixed

### The count of `help` entries and T-207's figures are out of date or wrong in three places
- **Where:** `docs/milestones/M-29-graphs-on-a-mesh.md:285`; `README.md:31-35`; `docs/tasks/T-207-carve-s-tool-descriptions-under-claude-code-s-cut.md:300`, `:316`, `:391`
- **Severity:** Nit
- **Rubric:** tracker hygiene; the README kept current
- **What is wrong:**
  - The retrospective says `help` "serves what left eight of them". `HelpTool.Entries` has nine: B-10 added get_mesh, and `help {}` at HEAD lists nine.
  - README's paragraph on `help` names the entries without get_mesh. Only the get_mesh bullet at `README.md:96` mentions it.
  - In T-207's length table, render's "Before" is 19,122 (at `edb34ba`) with no bracketed figure, but what was served before T-207 (`e4d6dd4`, after T-165) was 19,954. I measured this from a `tools/list` dump.
  - T-207 says "the largest entry ... 19,571". The served entry at HEAD is 19,568, and no later commit changes render's text.
  - T-207 says "the 11 tool files". `git diff --stat e4d6dd4 eb4b4ff -- src/` shows 12 tool files, 14 files in all; the total of 14 is right.

**Outcome:** fixed

### Nothing holds the open and define_material entries, or a `More: help` pointer without an entry, and the digest leaves out help and bake
- **Where:** `tests/Carve.Server.Tests/HelpToolTests.cs:45-60`; `tests/Carve.Server.Tests/PartToolsListTests.cs:37`
- **Severity:** Nit
- **Rubric:** tests cover the Scope; a test that cannot fail
- **What is wrong:**
  - `Each_entrys_tool_ends_with_the_pointer_and_help_lists_every_entry` loops over the table only. A description that ends `More: help {"tool":"x"}` for a tool with no entry would pass.
  - No test checks that an entry is not empty.
  - Grepping every test for `WithReference`/`Entry(` finds reads of the render, export, bake, save, select, get_mesh and check entries (check through `PartTexts.Check`), and none of open's or define_material's. Either entry's `Reference` could be emptied and every test would pass.
  - `NewTools` keeps `help` and `bake` out of the tools/list digest. help's description and schema are held only by "names every entry, under 1,000 characters".

**Outcome:** fixed

### B-10's default-answer digests do not reach a holed face, a deleted face, a morph target or a graph slot
- **Where:** `tests/Carve.Server.Tests/GetMeshPartsTests.cs:36-52`
- **Severity:** Nit
- **Rubric:** tests cover each member's Scope ("with neither argument the answer is byte for byte as today")
- **What is wrong:** The six digests are:
  - `{}` on the wrench, barrel-textured and gardener;
  - `{"mesh":"Body"}` on gardener;
  - `Cab` and `Cab` at level 0 on the rover.

  None of these meshes has a hole, a tombstone, a `target` read or a graph-slot material (barrel-weathered is not among them). Those default paths are new code in `GetMeshTool.Run` (`Where(Keep)`, the `vertexSet` filter on `target.positions`, the `Where(... g.faces.Count > 0)` on groups). I read each one and found it unchanged when neither argument is given. But only reading holds that for these features, apart from the target example, which `EditToolDefinitionTests.cs:65-80` compares with its written output.

**Outcome:** fixed

**Covered:** I read these files whole: `HelpTool.cs`, `GetMeshTool.cs`, `HelpToolTests.cs` and `GetMeshPartsTests.cs`, T-207's and B-10's files, and the milestone file's rulings and retrospective. I also read the served descriptions and schemas at HEAD of render, export, bake, define_material, help, save, open and check, and get_mesh's in its source. For the sentence check I built three scratch copies (origin/main, `e4d6dd4` just before T-207, and HEAD) and dumped `tools/list` and every `help` entry through `ToolHarness` with a test of my own. A script then split the old descriptions of the twelve tools into sentences and looked each one up in the HEAD description plus its entry, after normalising whitespace. Against both bases, every sentence that was not found is one of these:
- a segmentation artefact, with the text present;
- a drop or tightening listed in T-207's As built;
- B-10's rewrite of get_mesh;
- the export space above.

No `Example:`, `Output:` or `Result:` line was lost. The select example that was dropped is a character-for-character duplicate, so the change to `SelectToolDefinitionTests` follows it and loosens nothing. Entries repeat almost nothing of their descriptions (at most three short sentences, in check). I probed get_mesh on `ToolHarness` with:
- a deleted id, a negative id and a fractional id;
- an empty group name, a group that only another mesh has, and a group whose face was deleted;
- `include` with a number, with `level`/`target`, and with an unknown name;
- an unknown argument;
- a holed face with `faces`;
- a model of two meshes.

Every answer matched the descriptions. The length test measures the served `tools/list` text, interpolated constants included, so it cannot pass with a served description over the limit. I judge the root-swap in the digest test sound on any Windows checkout: both sides are escaped by the same default JSON encoder, and the replace ignores case. It would fail only where the path after the root is not spelled with `\`, that is off Windows. I did not cover: the code behind render's and export's graph claims (the bake cache, the 5 s warning, the `<material>@<mesh>` copies), beyond reading the text; the other members' description sentences in tools outside the twelve; T-207's per-tool sentence counts; and running any regression-tier test.

## Findings: patina, the sample graphs and the taught examples (claude-opus-5-5)

### A mesh node with a bad parameter is reported as evaluating, then swatch, export and check fail with no message
- **Where:** `src/Carve.Graphs/MeshNodes.cs:44-50` (`Positive`) and `src/Carve.Graphs/MeshMaskNode.cs:28-34` (`Selection`), which throw only when the node is evaluated; patina catches neither: `src/Patina.Server/SwatchTool.cs:158-190`, `src/Patina.Server/ExportTool.cs:109`, `src/Patina.Server/CheckTool.cs:119`
- **Severity:** Major
- **Rubric:** Correctness (a wrong answer presented as right, then a useless error on reachable input); tests cover Scope (no patina test of a faulted mesh node)
- **What is wrong:** `mesh_mask`'s "exactly one of group or material" and the "above 0" rule for `mesh_curvature.width` and `mesh_occlusion.reach` are enforced only by `MeshParameterException` at evaluation, because the declared ranges start at 0 and the validator cannot check "one of". I ran a probe on `PatinaHarness` in a scratch copy with four graphs: `mesh_mask` with no params, `mesh_mask` with both `group` and `material`, `mesh_curvature` width 0, and `mesh_occlusion` reach 0. For each one, `define_graph` answered `no faults: it evaluates` and `save` wrote the file. Then `export`, `swatch` (flat) and `check` each came back `isError` with only the SDK's generic `An error occurred invoking 'export'.` (or `'swatch'`, `'check'`). The error names no node and no rule, so the agent has nothing to fix from. carve does handle this: `MeshParameterException` derives from `MeshMapsException`, which `GraphSlotProblem.IsCause` (`src/Carve.Server/GraphSlotProblem.cs:64`) turns into a named error. patina, which the person chose as the place to author these graphs, does not. Note for the Core and `Carve.Graphs` reviewer: the root cause is on that side of the line.

**Outcome:** fixed

### A graph that nests a mesh-reading graph gets no note in any answer, and check calls its mask broken
- **Where:** `src/Patina.Server/ToolSupport.cs:87-91` (`MeshNote` reads `graph.Nodes` only)
- **Severity:** Major
- **Rubric:** Correctness (a wrong result presented as right); listed under "Never verified", and worse than that listing says
- **What is wrong:** The retrospective lists this as never verified ("patina's stand-in note does not look into one"). I verified it, and the result is a definite wrong answer. My probe wrote `inner.texgraph` (`mesh_curvature` width 0.02 feeding a named output `edge`) and `outer.texgraph` (a `graph` node on it, output `g.edge`).
  - `open` and `get_graph` say `no faults: it evaluates` and name the nested file, with no note.
  - `swatch` flat answers `swatch flat: edge of outer.texgraph, 32 x 32 px, exact texels`, with no note.
  - `export` writes `edge.png` (every texel 128), with no note.
  - `check` answers `one-value` on `edge` with "every texel is 0.502 (128) ... Fix: it is clipped or flat; widen its levels ..., or drive it from a noise ...", and its `notes` list is empty. So the agent is told to fix a mask that is only a stand-in.
  - `save` of the outer graph succeeds with no note.

  Nesting is how the samples themselves are built (`weathered-staves` nests `planks`), so an agent that layers `weathered-staves` or `weathered-iron` into another graph hits this case. The fix is one recursion over `GraphValidator.Uses` (`OpenTool` already calls it).

**Outcome:** fixed

### The scenario's break cannot fail on its own: it negates a looser form of the check on the same two numbers
- **Where:** `scenarios/Carve.Scenario.Patina/MeshNodesStage.cs:43-44, 74-80`
- **Severity:** Minor
- **Rubric:** A check that cannot catch what it is there for (the break is meant to prove the title check)
- **What is wrong:** The check is `extent - plain >= 80`. The break runs `probe.Check(extent - plain < 8)` on the same `extent` and `plain` and requires it to fail. Whenever the real check passes (a gap of 80 or more), the break fails automatically, so `mesh-nodes-breaks.txt` records a failure that proves nothing. The rule is never run on a title that lacks the note. An honest break would apply the real rule (gap of at least 80) to two titles that have no note, for example `plain` against a second capture of the plain sheet, and show that it fails. Separately, because the title's text is not decoded, any suffix of about 10 or more characters passes as "the note". As built discloses this ("text is not decoded"), so I list it here only as context.

**Outcome:** fixed

### The worked-example test only validates the examples, so it cannot catch what the changed `mesh_curvature` caption claims
- **Where:** `tests/Patina.Server.Tests/NodeToolTests.cs:50-62`; the caption is at `src/Patina.Server/NodeCatalogue.cs:133`
- **Severity:** Minor
- **Rubric:** Tests cover Scope ("a worked example ... run by its test")
- **What is wrong:** "Run as written" means only that `define_graph` answers `Evaluates(9)`. `define_graph` does not evaluate: the first finding shows it answering "it evaluates" for graphs that throw. So the test would pass for an example that faults when evaluated, or one whose patina mask is not 0. The T-168 caption says "in patina the stand-in gives 0.5, so the mask is 0 and the image is the base colour", and no test checks that. I checked it by probe and it holds: on the neutral registry the mask's maximum is 0 and `base_colour` is 48,50,54 (`#303236`) on every texel I read. A synthetic curvature band (peak 1, falling to 0.5 over 4 texels each side, 256 rows) shows the mask comes and goes along the edge:
  - 63% of band texels are worn, 28% at 0.5 or more, 4% saturated, mean 0.27;
  - on the centre line, 61% of rows are at 0.1 or more, in 4 separate runs;
  - nothing outside the band is worn.

  The caption is true today. Nothing holds it true.

**Outcome:** fixed

### swatch's mesh note is in no unit test, and the sphere, tiled and sheet captions' note in no test at all
- **Where:** `tests/Patina.Server.Tests/MeshNodeToolTests.cs:36-52`; `src/Patina.Server/SwatchTool.cs:119-121`
- **Severity:** Minor
- **Rubric:** Tests cover Scope ("Tests (`patina-unit`): every tool with a mesh-node graph ... the notes")
- **What is wrong:** `Every_tool_takes_a_mesh_graph_and_says_the_inputs_are_neutral` covers `define_graph`, `get_graph`, `edit_graph`, `save`, `open`, `export` and `check`, but not `swatch`. The harness serves swatch's flat and tiled views without an engine (my probe called flat through it). The swatch note is held only by the GPU scenario, which checks the flat view's text and the sheet title's pixel width. Nothing in either tier checks the note on the tiled, sphere, cube or sheet text results, though `Show` adds it to all of them.

**Outcome:** fixed

### `CheckSamplesTests` skips by file name, and its stand-in covers only the two names it lists
- **Where:** `tests/Patina.Graphs.Tests/CheckSamplesTests.cs:31-34`; `tests/Patina.Server.Tests/MeshNodeToolTests.cs:75-105`
- **Severity:** Nit
- **Rubric:** Tests cover Scope (judge whether the replacement checks are equal)
- **What is wrong:** For these two graphs the checks are equal. Both use the same `CheckRules` at `graph.Size` and the same no-faults load. The only difference is the expected `one-value` on `wear` or `grime`, which T-162's Notes record. The PNG equality is held by `The_committed_pngs_equal_the_graph`, and canonical text by `NestedGraphTests.A_file_without_graph_or_input_nodes_is_unchanged_by_the_expansion`, which reads every sample. But the skip keys on the `weathered-` prefix, while the replacement names its two graphs in `InlineData`. A third `weathered-*.texgraph` would therefore be clean-checked by neither, and its PNGs compared by neither. Keying the skip on "has a mesh node", and having the server test take the same `MemberData`, would close that gap.

**Outcome:** fixed

### As built leaves open what patina's output gained; Carve.Core is new to it
- **Where:** `docs/tasks/T-162-patina-knows-the-mesh-nodes-and-the-weathered-sample-graphs.md:106`
- **Severity:** Nit
- **Rubric:** Tracker hygiene (Notes asked As built to say what the publish folder gained; planning listed it as not checked)
- **What is wrong:** As built says "`Carve.Graphs.dll` (and `Carve.Core.dll` if not already there)". It was not already there. On `main`, patina references `Carve.Extensibility`, `Carve.Hosting` and `Patina.Graphs`, and none of those references `Carve.Core`. A scratch Debug build of patina now has `Carve.Core.dll` (597,504 B) and `Carve.Graphs.dll` (39,936 B), and no other new assembly; `Carve.Core` and `Carve.Graphs` take no package references. `PatinaReferencesNoExtensionTests` still passes, so no extension, engine or scenario library is reachable. All of carve's model library is now loaded in patina, which also loads extensions, and this is not recorded anywhere.

**Outcome:** fixed

**Covered:** I read these whole: T-162's file, the milestone's file, the diff of `src/Patina.Server` (Program, ToolSupport, FileTools, CheckTool, ExportTool, SwatchTool, NodeCatalogue, csproj), `tools/Patina.SampleGraphs`, both weathered graphs, `MeshNodesStage.cs`, `MeshNodeToolTests`, `NodeToolTests`' changes, `CheckSamplesTests`, `ProductReferenceGuard`, the patina reference tests, `Carve.Graphs`' `MeshNodes`/`MeshMaskNode`/`MeshCurvatureNode`, T-168's commit to the example (`d09e21a`), and the `patina-unit`, `scenario-patina` and `server-unit` inputs in `tests.json`. In a scratch copy I ran the `MeshNodeToolTests` and `NodeToolTests` (15 pass, length test included; it measures `tools/list` as served through `Program.AddTools`) and a probe test on `PatinaHarness`, and ran `tools/Patina.SampleGraphs`. What the probes showed:
- **Sample PNGs:** the tool reproduces every committed PNG byte for byte and prints `(mesh inputs neutral)` on the two weathered lines. `wear.png` and `grime.png` have no non-zero colour byte and full alpha. The staves' four shared PNGs are byte-equal to the planks' files.
- **Sample graph files:** both graphs are in canonical text (they differ only by CRLF from the checkout) and at version 2, the lowest that holds `parameters`.
- **A group name with a quote, a backslash, a space and `ü`:** it saves, opens and reads back exactly.
- **An unknown group:** patina accepts it silently (stand-in 1, note given). carve's `MeshMaskException` is handled by `GraphSlotProblem`.
- **The guard:** adding `Carve.Graphs` admits only that exact name; the closure walk still catches anything forbidden behind it.
- **The scenario:** it references `Carve.Extensibility` and `Carve.Debug`, as before M-29; no new product library.
- **The sample `weathered-iron` on the same synthetic band:** wear on 100% of centre rows in one unbroken run. This is the even "outline" the T-168 caption warns against, and it is already listed as not retuned.

I did not get to: anything that needs a device. That covers the sheet title's width with long parameter text (whether the note can be cut off) and the frames by eye. I also did not reach the README's patina section beyond its weathered paragraph.

## Findings: the scenarios, the acceptance and the whole diff (claude-opus-5-5)

### The acceptance crevice set counts face 99, a dark socket face, as "drive", against the ruling and the record
- **Where:** `tests/Carve.WeatheringAcceptance.Tests/WeatheringAcceptanceTests.cs:216,239,248-250`; `docs/tasks/T-168-graphs-on-a-mesh-acceptance-run.md:99,140`; `CLAUDE.md:123`
- **Severity:** Major
- **Rubric:** Correctness; a ruled check implemented as ruled; a record that claims what was not shown
- **What is wrong:** `NoteJunction` takes the texels of `all = DriveFaces.Concat(SocketFaces)` (100 to 105, then 118 and 99) and flags each `Drive` with `f < 106`. Face 99 (the hub ring's underside, group `dark`, no steel material) is under 106, so its texels go into the "drive only" set, and only face 118 is counted as socket. The person ruled "the drive's texels within 0.004 m of the 8 edges of the drive's two ends", and the task's file says twice that "faces 118 and 99 ... are not in the set". I split the set by face with a probe in a scratch copy (a console on `Carve.Graphs` that bakes each round's saved file as the test does). Its counts add up exactly to `numbers.tsv`. Round 1: 10,357 = 6,589 drive + **3,768 on face 99**. Round 2: 2,604 = 1,662 drive + **942 on face 99**. So 36% of each measured set is a face the steel is never drawn on. The negatives' 2,604 in `%TEMP%\carve-t168-negatives` include the same 942. On the set as ruled, both rounds still pass: round 1 94.05 − 45.97 = 48.1, round 2 93.32 − 68.68 = 24.6 (recorded as 49.143 and 24.862). So the verdict holds, but the numbers and the claim of what the set holds do not. The ruled set is also weaker than it reads. 41% of the drive's own band texels (2,650 of 6,589 and 682 of 1,662) lie on the end faces 104 and 105. Those sit flush against faces 99 and 118 (z −0.020 and −0.042 against −0.020 and −0.0425), so they are never seen and any occlusion darkens them. On the four visible side faces alone, round 2's margin is 93.32 − 78.11 = 15.2. Nobody told the person this when the set was ruled.

**Outcome:** fixed

### Suite inputs miss sample files that M-29's new tests read at run time
- **Where:** `tests.json:32-49` (graphs-unit), `tests.json:86-129` (server-unit), `tests.json:131-155` (server-regression)
- **Severity:** Minor
- **Rubric:** Tests are placed; tracker hygiene (a suite's inputs)
- **What is wrong:** I wrote a script that follows every suite's `dotnet-closure` projects, their `ProjectReference`s and every `Compile`/`Content` link outside each project, and checks each against the suite's inputs. The two Weathered suites, scenario-bakes and scenario-patina are complete. Two gaps are left. First, server-unit lacks `tests/Carve.CharacterLoad.Tests/RunContent.cs`, linked by the new `Carve.WeatheringAcceptance.Tests` (the two older acceptance projects already miss it the same way). Second, and more significant, files that the new tests open from `Paths.RepoRoot` are in no input list:
  - graphs-unit: `GraphSlotBakeTests.cs:20,44` reads `samples/graphs/planks.texgraph` and `samples/graphs/planks/*.png`, but the suite lists only `samples/wrench.carve`.
  - server-unit: `GetMeshPartsTests.cs:37-42` (B-10) pins answer digests of `wrench.carve`, `barrel-textured.carve` and `rover.carve`. `GraphMaterialErrorsTests.cs:180-191` opens every `samples/*.carve` (now including `barrel-weathered.carve` and its graphs), `barrel.carve` and `samples/graphs/weathered-iron.texgraph`. Of these the suite lists only `gardener.carve`.
  - server-regression: `GraphMaterialRenderTests.cs:65-67` (T-163, T-164, B-11) copies `samples/graphs/weathered-iron`, `weathered-staves` and `planks.texgraph`, and none of `samples/graphs/` is an input.

  An edit to a sample graph or a sample model therefore carries those suites over unrun, although it changes what they check.

**Outcome:** fixed

### Two graphs-unit tests take 21 s and 12.7 s in the unit tier, one of them "not a check"
- **Where:** `tests/Carve.Graphs.Tests/MeshNodesTests.cs:89-91`, `:269-271`
- **Severity:** Minor
- **Rubric:** Tests are placed (the right `Tier` trait)
- **What is wrong:** CLAUDE.md's tiers rule says `unit` means about 1 s or less a test. I ran graphs-unit's command in a scratch copy with a trx log: 40 tests in 43 s, 6 of them over 1 s. `Each_node_equals_its_bake_at_every_tile_size_and_a_zero_cap(size: 1024)` took 21.3 s. `Times_each_map_on_the_wrench` took 12.7 s, and its own summary says "Not a check: each map's time ... written to the temp folder for the As built". So a timing probe with no assertion costs every graphs-unit run 13 s. This may overlap area 1 (Carve.Graphs); it is filed here as a tier-placement point.

**Outcome:** fixed

### CLAUDE.md says the acceptance grip is checked "against the clean stand-in", which the person's ruling replaced
- **Where:** `CLAUDE.md:123`
- **Severity:** Nit
- **Rubric:** The README and CLAUDE.md kept current
- **What is wrong:** The `tests/Carve.WeatheringAcceptance.Tests` row lists "the steel's base colour bake (hard-edge wear, drive crevice ..., grip against the clean stand-in ...)". Ruling 4 of 2026-11-24 (T-168:93) replaced that check: the grip is held to what its faces wear in the saved file, against the starter file's grip (`The_grip_wears_no_more_spread_than_the_starters_grip`), and that is not the steel slot's bake.

**Outcome:** fixed

### README's list of unit suites leaves out the new graphs-unit
- **Where:** `README.md:50`
- **Severity:** Nit
- **Rubric:** The README kept current
- **What is wrong:** "unit, no GPU: `core-unit` and `server-unit` (the two above), and `patina-unit`". T-161 added `graphs-unit` (tests.json:32), and every regression suite M-29 added is listed two lines below.

**Outcome:** fixed

### Stage 3's restore is said to check for a cache hit; it only logs one
- **Where:** `scenarios/Carve.Scenario.Weathered/RebakeStage.cs:117`; `docs/tasks/T-167-the-weathered-barrel-and-its-scenario.md:247-248`; `CLAUDE.md:103`
- **Severity:** Nit
- **Rubric:** An honest record
- **What is wrong:** The As built says "the graph restored (cache hit, byte-identical)", and the Layout row says that in stage 3 "each step checks the `baked:` line". After the restore the code writes "baked" or "nothing baked" to `sets.txt` and asserts only that the frame is byte-identical. Every other step calls `Baked(...)` with an expectation.

**Outcome:** fixed

### T-168's round 2 bake times mark one 512 bake as 1024
- **Where:** `docs/tasks/T-168-graphs-on-a-mesh-acceptance-run.md:111`
- **Severity:** Nit
- **Rubric:** An honest record
- **What is wrong:** The row reads "all 1024 unless noted: 471 (512), 810, 1329 (512), 1042 + 114 (512), 1073, 940 + 123 (512), 335". In round 2's `transcript.jsonl`, the 1073 ms entry is `{"material":"worn-steel.texgraph","slot":"image","size":512}`. Everything else in both rounds' rows matches the round folders: 31 and 25 calls, the refused Bash at line 15, `nodes` at calls 5 and 6, `get_mesh` with `include`, every `numbers.tsv` and `differences.tsv` figure, and the identical prompt and plant (they differ only in line ending and key order).

**Outcome:** fixed

**Covered:** I read these whole: the milestone's file, T-167's and T-168's files, the Weathered scenario (every stage, the sets, the csproj), `tests/Carve.WeatheredLoad.Tests`, `scripts/run-weathered-scenario.ps1`, `Carve.Scenario.Bakes/GraphImageStage.cs` and the BakesChecks/BakesStages diff, T-168's tests, the Acceptance scenario's `WeatheringStep.cs`, and `prepare-weathering-round.ps1`, `run-weathering-negatives.ps1` and `weathering-round/`. I also read the diffs of `tests.json`, `Carve.slnx`, `CLAUDE.md` and `README.md`. I checked each of T-167's five thresholds and three rulings and T-168's margins, grip and crevice against the code: all are as ruled except the crevice set above. The D-6 step after the re-unwrap holds by construction, as the retrospective already says. Things I ran:
- the link-against-inputs script over every suite;
- a face-by-face probe of the crevice set on both rounds' saved files (a scratch copy, Debug, CPU bake only);
- Python recounts of both rounds' hard-edge sets (they match `numbers.tsv` exactly);
- summaries of both transcripts by script;
- a comparison of the two rounds' starting files;
- in the scratch copy, the unit tests touched by the refactor pass (107 server-unit tests matching Graph, SaveOpen, FileTool, Help, RenderDefinition and MaterialTool; the 10 GraphSlotBake tests), all passing, since the recorded verify predates those three commits;
- graphs-unit with timings;
- `cairn lint` (clean).

In the whole-diff pass I checked: the neutral values (one definition each, `Mesh*Node.Neutral`, used by the nodes, `bake` and patina's stand-ins); the size range, the 5 s warning, padding 8 and the 3,686 budget, which are stated consistently; the refactor pass's removed APIs, with no stale reference left; every member's status, `Depends on:` and `**Verify:**`; the Required list against the 12 merges; the changelog; and product source for break switches (none) and committed machine paths (only B-10's known `TakenIn`). I did not get to: patina's `MeshNodesStage` beyond the scenario-rule grep; T-162's sample PNGs; whether the unruled frames' clean-colour relations ever skip a set silently (no `sets.txt` from T-167's runs survives on disk); the T-165 claim that six M-24 frames moved; and whether the description texts are true, which is area 3's. I opened no image.

## Summary

The diff (149 files, about 8,300 added lines, at `4138e51`, after the refactor pass) was split by area and not by
member, because several members each touched the same files (the server's tools above all) and one area's faults are
easier to see whole. Five reviewers ran in parallel, each reading its area's changed files whole and proving what it
could in a scratch copy; the fifth also read the whole diff for what falls between the areas. No rebase was needed:
`origin/main` had not moved since the milestone forked.

36 findings: 6 Major, 18 Minor, 12 Nit. Two of the Majors are one fault found from both sides (the bake cache's key
leaves out the slot), and the patina reviewer's first Major shares its cause with a Core Minor (a mesh node's bad
parameter passes validation). What held: no sentence was lost from the trimmed descriptions; the committed sample
PNGs equal their graphs; T-167's thresholds and T-168's margins are coded as ruled; no scenario references a product
library; the refactor pass kept both callers' wording.
