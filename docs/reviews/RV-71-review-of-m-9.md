---
description: >-
  Review of M-9
---
# RV-71 — Review of M-9

**Status:** Open

**Reviews:** M-9

**Round:** 1

**Previous:** —

**Diff:** 0fc3fab856b627199a665f2b5c48b1ef57c6fbf2 against 631a596

**Reviewer:** four reviewers, each claude-opus-5-5: Core and the Carve file; the part tools and render; the export, scenario, sample and acceptance; the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Core and the Carve file — T-91, T-92, T-132 (claude-opus-5-5)

### A bare top-level empty part is lost by save and open

- **Where:** `src/Carve.Core/CarveFile.cs:334-337` (`HasPartTree`, `!p.IsPlain`) with `src/Carve.Core/Part.cs:48` (`IsPlain`); test hole at `tests/Carve.Core.Tests/PartTreeFileTests.cs:122` (rubric 1, 2)
- **What is wrong:** `IsPlain` is true for `new Part("E")`, which is exactly what `define_part part=E` (no other argument) makes: an empty with no parent, no transform, no envelope. `HasPartTree` therefore says no part tree, so the file is written at version 6 or lower with no `parts` key. An empty has no mesh, so nothing in the file holds it. I checked this with a scratch program against the built `Carve.Core.dll`: `Model.Empty.With("A", quad).WithPart(new Part("E"))` has parts `'A', 'E'` and `HasPartTree` false, and after `CarveFile.Read(CarveFile.Write(m))` it has only `'A'` (version 2), with `FindPart("E")` null. The test that each trigger moves the file to version 7 (`Triggers`) has no case for a bare empty, so nothing catches this.
- **Consequence:** an agent can save a model and open it back, and silently lose a part. This happens when it lays out a group or pivot first and gives it children or a placement later, or leaves the empty as a placeholder. A later `define_part parent=E` is then refused with "no part named 'E'". This breaks the spec's "the Carve file holds the tree", and "read back" is not true for this model. A possible fix: a part with no mesh is never plain for the trigger, e.g. `model.Parts.Any(p => !p.IsPlain || model.Find(p.Name) is null)`, plus a trigger case.
- **Severity:** Major

**Outcome:** —

### Plain parts defined in a different order from their meshes come back reordered, so `Combined` and `OffsetOf` change across save and open

- **Where:** `src/Carve.Core/CarveFile.cs:334-337` (no `parts` written for a plain model) with `src/Carve.Core/Model.cs:220` (`Create` puts mesh-only parts in mesh order) (rubric 1)
- **What is wrong:** `Combined` draws in part pre-order (`_parts` order), but a plain model's file stores only mesh order. If parts are recorded before their meshes in another order, the order changes on the round trip. For example `define_part A`, `define_part B`, then `define_mesh B`, then `define_mesh A` gives parts `'A','B'` and meshes `'B','A'`. Every part is still plain, so the file is version 2, and reading it back gives parts `'B','A'`. Scratch-program result: `Combined.Vertices[0]` goes from (0,0,0) to (5,0,0), and `OffsetOf("A")` goes from (0, 0) to (4, 1).
- **Consequence:** after a save and open, the same model draws and exports (`ExportTool` uses `model.Combined` for a model with no `moves`) with its occurrences in another order. Anything keyed on positions in `Combined` (render offsets, the weights heat runs) differs from before the save. The model is equal by every record, but not "read back as it was". The fix for the finding above (an empty forces the tree) covers only some of this. The order itself needs either a trigger (part order differs from mesh order) or `With`/`Create` keeping the two orders in step.
- **Severity:** Minor

**Outcome:** —

### Core lets an existing part's `instance_of` change; the rule lives only in `define_part`

- **Where:** `src/Carve.Server/PartTools.cs:225-229` (the rule) against `src/Carve.Core/Model.cs` `WithPart(Part, out string?)` / `Placement` (no such check) (rubric 1, placement)
- **What is wrong:** the person ruled that an existing part's `instance_of` does not change (T-93). Core's public `WithPart` accepts `existing with { InstanceOf = "T" }`, which re-points an instance to another source. It also accepts `with { InstanceOf = null }`, which quietly turns an instance into an empty. I confirmed both with the scratch program. The refusal is decided in `DefinePartTool.Run` inside the `ModelStore.Change` lambda. That is Server deciding what an edit does to the model. Core already guards the neighbouring invariants itself (`With` refuses an instance's name, `Placement` refuses `instance_of` on an owner).
- **Consequence:** the ruling holds only for callers that go through `define_part`. Any other Core caller, now or later (a future tool, or `WithPartPieceFrom`-style helpers), can break it without a refusal. My view on the refactor pass's question is yes: move it into Core. `WithPart` should refuse a changed `InstanceOf` on an existing record (`instance_of: ... an existing part's instance_of does not change`), keeping the two Server messages' wording. `DefinePartTool` would then only pass the problem through. No current Core caller changes `InstanceOf`, so the move is safe.
- **Severity:** Minor

**Outcome:** —

### `CarveFile`'s doc comments still give the version range as ending at 8

- **Where:** `src/Carve.Core/CarveFile.cs:26` ("1 to 8"), `:85` ("versions 2 to 8"), `:160` ("version 2 to 8") (rubric 7)
- **What is wrong:** T-132 made 9 readable and written (`ReadableVersions` is 1 to 9, `PartsAndTargetsVersion` 9), but these three summaries were not updated.
- **Consequence:** the API docs say a version 9 file cannot be read or written. Misleading only.
- **Severity:** Nit

**Outcome:** —

### `Model.Piece` has no product caller left and silently drops levels

- **Where:** `src/Carve.Core/Model.cs:243` (rubric 3, 7)
- **What is wrong:** since T-92, `save mesh=` calls `PartPiece`, so `Piece` is used only by older tests (`CarveFileV2Tests`, `ModelMaterialTests`, `MorphTargetFileTests`). On a part with kept levels it returns `Part.Of(name)` with none. T-92's As built says "the old `Model.Piece` and `OpenMesh` are unchanged". Since `e835a72`, the old `OpenMesh` survives only as its no-refuse overload, which also has no caller.
- **Consequence:** this is a second public piece API that differs from the one the product uses. A future caller choosing `Piece` would lose the levels without any error, and the tests that pin `Piece` guard a path `save` no longer takes. Either point those tests at `PartPiece` and drop `Piece`, or document `Piece` as level-less.
- **Severity:** Nit

**Outcome:** —

## Findings: the part tools and render — T-93, T-94 (claude-opus-5-5)

### Whole-model labels and highlight still use the pre-instance window rule, so a single mesh with instances is labelled on every copy

- **Where:** `src/Carve.Server/RenderTool.cs:464` and `:479` (rubric 1)
- **What is wrong:** With no `part`, render decides whether to window labels and highlight with `current.Model.Meshes.Count > 1`. But since M-9, `Model.Combined` (`snapshot.Mesh`, what `drawn` is) holds one occurrence per part with geometry *and per instance* (`Model.cs` `Occurrences`). Take a model with one mesh and instances of it, for example `Wheel_FL` plus `Wheel_FR`/`RL`/`RR` as instances. `Meshes.Count` is 1, so no window is set and `idsIn = drawn` is the combined mesh. `labels: faces` then puts ids 0..199 across all four wheels, where only 0..49 are real. `MeshEdits.UnknownElements(idsIn, ...)` accepts `highlight {faces:[120]}`, which no edit tool will take. Naming `mesh` explicitly doesn't help either: the `part is null && level is null` arm still checks `Meshes.Count > 1`. The same happens with `level` and no `mesh`, because the `if` at 464 isn't entered. As built says the whole-model path keeps "the old offset and window rule (`Meshes.Count > 1`)", and it does. That rule was right only while one mesh meant one occurrence.
- **Consequence:** An agent picks face ids off a labelled render and gets ids that don't exist, shown on instances. This breaks the T-94 rule that ids are "shown on its own occurrence and not on its instances". No test covers it: the rover has several meshes, so it always takes the windowed path.
- **Severity:** Major

**Outcome:** —

### The frame checks build their expected image with the same Core functions the product draws with

- **Where:** `tests/Carve.Server.Tests/RoverRenderTests.cs:85` (`Expect` uses `model.CombinedOf(part, level)`) and `:75` (`FacesOf` uses `Model.OffsetOf(name, Part, Level)`) (rubric 4)
- **What is wrong:** Render draws `Model.CombinedOf(part, level)` and windows with `OffsetOf(name, part, level)`. The test's ray-test solid, its camera bounds, the inner-cap face ranges and the labelled frame's expected counts (`wheel.Mesh.LiveVertexCount`) all come from those same two functions. `SubtreeOf`, `CombinedOf(part, level)` and `OffsetOf(name, part, level)` have no Core unit test of their own: the only `OffsetOf` tests in `PartTreeTests` are the whole-model overload. Suppose `CombinedOf` ignored `part` or `level`, or `OffsetOf` used live counts at a level. The expected image would move with the product, and `axle-f`, `wheel-fl` and `rover-front-level-0` would still pass. Only the caption prefix `part Axle_F, ` is asserted for the axle, not As built's `128 vertices, 100 faces`.
- **Consequence:** The subtree filter and level choice that T-94 adds aren't independently pinned in the server or Core suites. They are caught only by scenario-parts (`part-not-isolated`, `levels-ignored`, built from `get_mesh` and `RoverSpec`'s tree) and by the person's frame review. An offset bug at a level, in labels with `level`, is not covered anywhere: no frame renders labels at a level.
- **Severity:** Minor

**Outcome:** —

### The tools/list guard takes T-94's paragraphs out but nothing checks they are there

- **Where:** `tests/Carve.Server.Tests/PartToolsListTests.cs:28`, `:48` (rubric 4)
- **What is wrong:** `Digest` replaces `RenderPartTexts.Parts` and `GetMeshTool.PlacementText` with "" in every tool's description. T-93's sentences get a matching `Each_agreed_sentence_is_in_its_tools_description_once` check; T-94's two paragraphs get none. A search of `tests/` finds no other use of `PartsText`, `RenderPartTexts`, `PlacementText`, `PartArgumentText` or `LevelArgumentText`. The test's own comment says that, because the digest takes a sentence out, "it cannot see one go missing".
- **Consequence:** If render's part/level paragraph or get_mesh's placement paragraph were dropped, or added to the wrong tool, both guard tests would still pass, and agents would lose the only text describing `part`, `level`, `parent` and `world`.
- **Severity:** Minor

**Outcome:** —

### get_mesh's `world` is a `Decompose` whose failure is ignored, and a parent's non-uniform scale gives a TRS that is not the transform

- **Where:** `src/Carve.Server/GetMeshTool.cs:123` (rubric 1)
- **What is wrong:** `Matrix4x4.Decompose(model.WorldTransform(partName)!.Value, ...)` ignores its return value. define_part accepts any per-axis scale > 0. So a parent with scale `[2,1,1]` and a rotated child give a sheared world matrix, which no translation/rotation/scale triple can express. The `world` returned then doesn't place the part. Yet `PlacementText` says it is "the part's space to the model's". get_parts' `world_bounds` uses the full matrix, so the two tools would disagree.
- **Consequence:** An agent placing things from `world` gets a wrong rotation or scale on a non-uniformly scaled tree, with no sign of it. The rover doesn't reach this, and nothing tests it.
- **Severity:** Minor

**Outcome:** —

### get_mesh and check read `store.Current` more than once per call, and get_mesh can throw on a model that changed between the reads

- **Where:** `src/Carve.Server/GetMeshTool.cs:67`; `src/Carve.Server/CheckTool.cs:79` (rubric 2)
- **What is wrong:** get_mesh gets its mesh through `Target(store, ...)`, which reads one snapshot. Then it reads `store.Current.Model` again and does `model.Target(name, out _)!.Name`. If an `undo` or `open` from another request lands between the two reads and removes the mesh, the `!` throws a `NullReferenceException`, not a tool error. Even without that, `parent`, `world` and `level` can come from a different model than the vertices. check likewise computes envelope warnings from a fresh `store.Current` after checking the mesh from an earlier one. `MeshToolSupport.Target(ModelSnapshot, ...)` exists for exactly this: read the snapshot once.
- **Consequence:** It's a rare race, but it can give an unhandled exception, or an output mixing two models. The write side (`ModelStore.Change`) is correctly under the lock.
- **Severity:** Minor

**Outcome:** —

### "Naming the part to use instead" / "saying which part to keep instead" is not true of an empty part's refusal

- **Where:** `src/Carve.Server/PartTools.cs:15` (`PartTexts.PartSpace`), `:290` region (`KeepLevelTool.Text`); `src/Carve.Core/Model.cs:491` (rubric 3)
- **What is wrong:** The edit tools' shared sentence says an instance or an empty part "is refused, naming the part to use instead". keep_level's text says the same of instances and empties ("by name, saying which part to keep instead"). That holds for an instance, where the source is named. An empty's refusals name no other part: `mesh: 'Axle_F' is an empty part with no geometry; define_mesh name=Axle_F gives it some` and `'Axle_F' is an empty part with no geometry to keep as a level`.
- **Consequence:** The descriptions promise a pointer the empty-part refusal doesn't give. That's small, but these texts are what agents learn from.
- **Severity:** Nit

**Outcome:** —

### Open question: the "an existing part's instance_of does not change" rule belongs in Core

- **Where:** `src/Carve.Server/PartTools.cs:225` (`DefinePartTool.Run`) (rubric 5)
- **What is wrong:** The person's ruling is a rule about what a change does to the part tree: an instance doesn't become something else, and an owner or empty doesn't become an instance. It lives only in the tool. `Model.WithPart` and `PartProblems` check the new part on its own and accept a changed `InstanceOf`. Any other Core caller, such as a later tool, a piece open (`WithPartPieceFrom`) or a scenario through Core, can change it without the rule. CLAUDE.md's rule is that if the server "is deciding what an edit does to a mesh, that belongs in the Core". My view is to move the comparison with the existing part into `PartProblems` (or `WithPart`), as `("instance_of", ...)`, and keep only the wording that mentions the tool ("give a new name to make an instance") in `PartTools.cs`. That is also where the T-93 break "an instance_of change allowed" would then be caught by Core's own tests.
- **Consequence:** For now there is one caller, so nothing is wrong today. The rule would be lost silently by the next caller.
- **Severity:** Nit

**Outcome:** —

## Findings: the export, the rover scenario, the sample and acceptance — T-95 to T-98 (claude-opus-5-5)

### server-unit's inputs miss six files its project now links

- **Where:** `tests.json:67` (suite `server-unit`, inputs) against `tests/Carve.Server.Tests/Carve.Server.Tests.csproj:72-81` (rubric 6)
- **What is wrong:** M-9 links `tests/Carve.Core.Tests/RoverSpec.cs`, `RoverSpec.Model.cs`, `PartTreeSample.cs`, `scenarios/Carve.Scenario.Crate/ColourRule.cs`, `scenarios/Carve.Scenario.Shed/ShedGeometry.cs` and `scenarios/Carve.Scenario.Cuts/CutsGeometry.cs` into `Carve.Server.Tests`. None of them is in `server-unit`'s inputs. A `dotnet-closure` follows project references, not `<Compile Include>` links outside the project, which is why the suite already lists `TestMeshes.cs`, `CameraPixels.cs` and the other linked files by name. `scenario-parts` and `core-unit` list some of these files, but `server-unit` lists none of them.
- **Consequence:** an edit to only one of these files (a rover number in `RoverSpec.cs`, or a `CutsGeometry.FirstHit` change like T-96's) leaves `server-unit`'s fingerprint unchanged. `GlbPartsTests`, `ExportPartsToolTests`, `RoverRenderTests` and the part tool tests are then not re-run, and a cached pass stands for code it never compiled.
- **Severity:** Minor

**Outcome:** —

### Carve.PartsAcceptance.Tests has no Layout row and is in no suite's inputs, unlike T-49's

- **Where:** `CLAUDE.md:106-107` (Layout; no `tests/Carve.PartsAcceptance.Tests` row) and `tests.json:67` (`server-unit` inputs); `docs/tasks/T-49-character-acceptance-run.md:81` (rubric 6, 7)
- **What is wrong:** T-49's acceptance projects have no suite of their own. The person later ruled them in as `dotnet-closure` inputs of `server-unit` ("Add as inputs", 2026-11-17), and `CLAUDE.md` gives each a row saying so. T-98's `Carve.PartsAcceptance.Tests` is in `Carve.slnx` but has no `CLAUDE.md` row and is an input of no suite. Its csproj comment also mentions "the frame reader of the viewer checks", but the project links only `RunContent.cs`.
- **Consequence:** every suite builds the solution, so a compile break in this project fails all of them. Yet no suite's fingerprint moves when it changes, so a cached result can hide the break until something else changes. Nothing in the docs says the project runs only on an acceptance round's folder.
- **Severity:** Minor

**Outcome:** —

### The tree export's ruled limit counting and the skinned moves warning have no tool-level test

- **Where:** `src/Carve.Server/ExportTool.cs:189-190` (`Tree`: limits on `Combined`'s triangles and distinct materials) and `ExportTool.cs:224-225, 259` (the skinned `MovesWarning` and `Baked` wired into the result); `tests/Carve.Server.Tests/ExportPartsToolTests.cs` and `GlbPartsTests.cs` (rubric 2)
- **What is wrong:** the person ruled that game limits count the whole file as drawn: four shared wheels are four wheels' triangles. No test exports a tree with a limits file, and none of the 26 breaks in T-95's table touches the count. A change that counts each glTF mesh once (the shared wheel once) would pass. The skinned export's moves warning is tested only as `SkinnedExport.MovesWarning`'s string, and no tool test shows it in the result line. `Baked`'s `WithWeights` step is tested only on an unweighted model, so dropping the weights from a placed part would also pass.
- **Consequence:** three Scope points that were ruled on can regress silently: the limits counting, the skinned warning, and the skinned bake keeping its weights.
- **Severity:** Minor

**Outcome:** —

### Moving_parts_share_one_glTF_material_per_colour cannot fail on materials

- **Where:** `tests/Carve.Server.Tests/GlbPartsTests.cs:251-257`; `src/Carve.Server/GlbExport.cs` `Build`'s `shared` dictionary (rubric 2)
- **What is wrong:** T-95's break B10 (materials not shared across meshes) fails nothing, because SharpGLTF writes content-equal materials once anyway. The test fails only under B4, through its mesh-count assert. Its title claims a property it cannot see, and the `shared` dictionary has no effect anyone can observe.
- **Consequence:** the test reads as coverage of the "material sharing" ruling that it doesn't give. Either drop the claim, or test something sharing actually changes (for example, the same `MaterialBuilder` object across meshes).
- **Severity:** Nit

**Outcome:** —

### T-97's As built break-table path has lost its backslashes to control characters

- **Where:** `docs/tasks/T-97-the-rover-in-a-game.md:53` (rubric 7)
- **What is wrong:** the path reads `G:\My Drive<BEL>rtifacts\carve\M-9\T-97<BS>reak-table.tsv`. `\a` and `\b` were taken as escapes, and the file holds bytes 0x07 and 0x08 where `\artifacts` and `\break-table.tsv` should be.
- **Consequence:** the evidence path can't be followed or copied, and the file carries control characters.
- **Severity:** Nit

**Outcome:** —

### T-96's As built still says tests.json passes -TimeoutSeconds 900

- **Where:** `docs/tasks/T-96-the-rover-through-the-tools.md:69` (rubric 7)
- **What is wrong:** T-97 (1c1c545) replaced T-96's direct `run-scenario.ps1 parts -TimeoutSeconds 900` suite line with `scripts/run-parts-scenario.ps1 -NoBuild`. The 900 is now that script's `-ScenarioTimeoutSeconds` default (`scripts/run-parts-scenario.ps1:16, 29`), and `CLAUDE.md:120` says so. T-96's As built was not updated.
- **Consequence:** the tracker names the wrong place to change the scenario's timeout.
- **Severity:** Nit

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### A placed rigged part is skinned in part space by render and weigh, but in model space by the export

- **Where:** `src/Carve.Core/Skinning.cs:175` and `:186` (T-91), against `src/Carve.Server/SkinnedExport.cs:53-62` (T-95) and `src/Carve.Server/ModelStore.cs:354` (`weigh`) (rubric 1)
- **What is wrong:** `Skinning.Posed` skins each mesh's part-local vertices against the skeleton, whose bone heads are in model space (`Skinning.Heads`), and only then places the result with `WithPartsFrom`. So `render` with `pose`/`clip` is `T·skin(v)`. `SkinnedExport.Baked`, the ruled skinned export, folds `WorldTransform` into the vertices first and keeps the weights, so the game skins `skin(T·v)`. `weigh` also measures distance from the part-local vertices to model-space bones. Each member picked its own reading of "a part's mesh is local" against a model-space skeleton, and nothing reconciles them. Instances make it worse: `Posed` skins the source once and every instance copies that shape, while `Baked` gives each copy its own weights at its own place.
- **Consequence:** If a weighted mesh has any non-identity part transform (one `define_part` with a translation is enough), `render` with a pose or clip no longer matches what the game draws from the exported file, and `render_glb` against `render` stops agreeing. That breaks CLAUDE.md's "Renders are Empyrean's" rule. `weigh` then weights the part against bones that sit somewhere else. No M-9 sample is both rigged and placed, so no test or frame shows it. B-7 covers only the weights look's ordering.
- **Severity:** Major

**Outcome:** —

### `save mesh=` refuses an instance or an empty with an edit-tool hint, while `open mesh=` has its own piece texts

- **Where:** `src/Carve.Core/Model.cs:752-758` (`PartPiece` goes through `Target`), against `src/Carve.Core/Model.cs:795-798` (`WithPartPieceFrom`) and `src/Carve.Server/SaveTool.cs:77`; pinned by `tests/Carve.Server.Tests/PartPieceToolTests.cs:60-61` (rubric 1)
- **What is wrong:** The rule "a piece of an instance or an empty is refused with a pointer" is written twice. `save` reuses the edit tools' text: "mesh: 'Copy' is an instance of 'Arm'; edit 'Arm', and every instance follows", and for an empty "define_mesh name=Empty gives it some". `open` has piece-specific texts: "open into 'Arm'", and "a piece is one part's own geometry, so name a part that has some". Save's own description promises "(save the source part an instance shows)", which its error does not say.
- **Consequence:** An agent saving `Wheel_FR` is told to edit `Wheel_FL`, and one saving `Axle_F` is told to give it geometry with define_mesh. Neither is what save wants. The two halves of one T-92 rule read differently to agents.
- **Severity:** Minor

**Outcome:** —

### `export mode=piece` on an instance or an empty gives "no mesh named", not save's pointer

- **Where:** `src/Carve.Server/PieceExport.cs:20-22`, against `docs/tasks/T-95-export-a-part-tree.md:31` ("as `save mesh=` does") and `src/Carve.Server/ExportTool.cs:41` (rubric 1)
- **What is wrong:** Piece export finds names with `model.Find`. An instance or an empty part is not a mesh, so it is refused as "no mesh named 'Wheel_FR'; the model's meshes are ...". The error does not say it is an instance or name its source. T-95's Scope ties piece export to `save mesh=`, and the planning ruled that an instance or an empty is refused with a pointer.
- **Consequence:** The same refusal is decided a third way. An agent that sees `Wheel_FR` in `get_parts` is told it does not exist.
- **Severity:** Minor

**Outcome:** —

### get_mesh reports a TRS "world" for a transform that export refuses as a shear

- **Where:** `src/Carve.Server/GetMeshTool.cs:123` (T-94), against `src/Carve.Server/GlbParts.cs:83-85` (T-95) (rubric 1)
- **What is wrong:** `define_part` allows a non-uniform scale on a parent above a rotated child, so a world transform can shear. T-95 checks the `Matrix4x4.Decompose` result and refuses a shearing transform, because it is not a TRS. `get_mesh` ignores `Decompose`'s return value and reports `world:{translation, rotation, scale}` as "the part's space to the model's", with no warning.
- **Consequence:** For such a part, `get_mesh`'s `world` does not reproduce where `render` draws it. An agent placing geometry from it gets it wrong, while export calls the same transform impossible.
- **Severity:** Minor

**Outcome:** —

### The part-space sentence is missing from the other tools that take `mesh`, and project_uvs still promises render's view

- **Where:** `src/Carve.Server/MeshToolSupport.cs:91-95` (`PartSpaceText` is used only by the 11 tools in `docs/tasks/T-93-part-tools.md:54`); `src/Carve.Server/ProjectUvsTool.cs:19` (rubric 1, 2)
- **What is wrong:** T-93's Scope (line 31) says "every edit tool" names a part with geometry, works in part-local space, and says so in the shared words. `set_colour`, `set_material`, `set_shading`, `mark_edges`, `project_uvs`, `unwrap`, `set_uvs`, `weigh`, `set_weights`, `get_weights` and `delete_target` do target a part through `Model.Target`, but their descriptions say nothing about it. `project_uvs` planar still says it lays faces out "as render would show them from view". Render now draws the placed model, but the projection uses the part's local axes, and `cylindrical`'s axis and centre are also local.
- **Consequence:** On a rotated part, `project_uvs view=front` does not match `render view=front`, and the descriptions contradict each other. Agents get no word that these tools refuse instances and empties.
- **Severity:** Minor

**Outcome:** —

### CLAUDE.md layout: no row for `tests/Carve.PartsAcceptance.Tests`, and the scenario's frame count is wrong

- **Where:** `CLAUDE.md:84` and the layout table around `:106`; `Carve.slnx:26` (rubric 7)
- **What is wrong:** T-98 added `tests/Carve.PartsAcceptance.Tests` to `Carve.slnx`. It reads `CARVE_PARTS_ACCEPTANCE_RUN` and is in no suite, but CLAUDE.md has no layout row for it, while T-49's equivalent `Carve.AcceptanceLoad.Tests` has one. The `Carve.Scenario.Parts` row says "seen in eleven frames". `PartsScenario.cs` captures 13, including `part-wheel-plain` and `instance-undone`, which the person ruled be kept, and T-96's As built and the retrospective both say 13.
- **Consequence:** The next agent cannot learn from CLAUDE.md how the parts acceptance checks run, or that they run outside any suite, and the frame count it reads is wrong.
- **Severity:** Minor

**Outcome:** —

### Retrospective and changelog wording do not match the record

- **Where:** `docs/milestones/M-9-parts.md` (Retrospective, "T-95: four export rules", followed by five bullets); `docs/changelog/UNRELEASED.md` (T-132 line) (rubric 7)
- **What is wrong:** The retrospective says "four export rules" and lists five. The T-132 changelog line says "so save no longer refuses a model with both". That refusal was added and removed inside M-9 and never reached `main`, so to a reader of the release notes nothing is "no longer" refused.
- **Consequence:** Small inaccuracies in the permanent record.
- **Severity:** Nit

**Outcome:** —

### `Model.Piece` is left with no product caller after `save` moved to `PartPiece`

- **Where:** `src/Carve.Core/Model.cs:243`; its tests in `tests/Carve.Core.Tests/CarveFileV2Tests.cs:445`, `ModelMaterialTests.cs:201`, `MorphTargetFileTests.cs:102` (rubric 3)
- **What is wrong:** T-92 switched `SaveTool` from `model.Piece(name)` to `model.PartPiece(name, out _)`, and nothing in `src/` calls `Piece` now. Its old tests still pin the unused method, not the path `save` takes. The last refactor commit (e835a72) removed the matching dead `OpenMesh` overload but left this one.
- **Consequence:** Dead code, with tests that give false confidence about how `save mesh=` behaves without a part tree.
- **Severity:** Nit

**Outcome:** —

## Summary

Round 1, at `e835a72` after the refactor pass. The diff (69 files, +6,746 lines outside `docs/`) touches a file format, the glTF export and the public tool API across nine members, so it was split by area into three reviewers (Core and the file: T-91, T-92, T-132; tools and render: T-93, T-94; export, scenario, sample and acceptance: T-95 to T-98), plus one pass over the whole diff for what falls between them. All four on Opus 5.5, as strong as the strongest implementer. 26 findings: 3 Major, 14 Minor, 9 Nit. Two reviewers independently answered the refactor pass's question (move the instance_of rule into Core): yes.
