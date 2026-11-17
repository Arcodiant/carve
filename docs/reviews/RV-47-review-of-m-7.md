---
description: >-
  Review of M-7
---
# RV-47 — Review of M-7

**Status:** Open

**Reviews:** M-7

**Round:** 1

**Previous:** —

**Diff:** 3ccb6c9a8c14e0d8a054e66130f84eb6e644fa3c against 5bc4559

**Reviewer:** four reviewers, all claude-opus-5-5: Core and the material tools; export and render; the viewer, sample textures and scenario; the whole diff

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Core and the material tools (claude-opus-5-5)

### set_material's worked example promises a silent check that the tool never gives

**Where:** `src/Carve.Server/MaterialTools.cs:204`, `tests/Carve.Server.Tests/MaterialToolTests.cs:59-62`
**Rubric:** 1 Correctness
**Severity:** Major

set_material's description is the one worked example agents learn from. Step 3 says that after the call "get_mesh lists faces 2 and 3 under crate and check has no warning". That is false. Step 1's `project_uvs box` maps only faces 2 and 3, so the cube is partly mapped. `MeshUvHealth` then gives the generic `no UVs, so the face samples the texture's corner` for each of faces 0, 1, 4 and 5. The test that runs this example as written asserts exactly that: `Assert.Equal(4, warnings.Count(w => w.EndsWith("no UVs, so the face samples the texture's corner")))`. So the code knows the example gives four warnings, but the text tells agents it gives none. An agent following the example sees four warnings it was told not to expect, and may "fix" faces that were meant to stay untextured. The fix is either to say the four untextured faces are warned of, or to map all six faces in step 1. The test then needs to assert whichever the description says.

**Outcome:** —

### open mesh= is refused by a missing texture on a material the mesh does not use

**Where:** `src/Carve.Server/OpenTool.cs:61`, `src/Carve.Core/CarveFile.cs:612`
**Rubric:** 1 Correctness
**Severity:** Minor

T-74's kept ruling says `open mesh=` "brings in and compares only the materials the opened mesh's faces use; the rest of the file's list is ignored". But `OpenTool` reads the whole file with `CarveFile.Read(content, folder)` before it picks the mesh, and `ReadMaterial` loads every texture of every material. So an unused material whose PNG is gone refuses the open. I checked this with a scratch program against `Carve.Core`. A file had meshes `a` (material `used`, no texture) and `b` (material `unused`, with `a.png`). After `a.png` was deleted, `Read` failed with `materials[1].base_colour_texture: cannot read 'a.png' (...)`, so `open mesh=a` cannot succeed. open's description (line 25) does not mention this case either. Either the ruling's "ignored" should cover read errors, or the description should say an unreadable texture anywhere in the file refuses `open mesh=` too. No test covers this case either way.

**Outcome:** —

### save writes a file that open will refuse when a held texture is gone from disk

**Where:** `src/Carve.Server/SaveTool.cs:105-107`
**Rubric:** 1 Correctness
**Severity:** Minor

The model holds each texture's bytes from `define_material`, but save writes only the path, and open reads the disk again. If the PNG is deleted or moved after `define_material`, `save` succeeds with no warning. The file it writes then fails `open` (`materials[i].<field>: cannot read ...`), and the model's held bytes are lost with the session. `TextureProblem` checks only that a relative path exists, not that a file is there. A warning in save's result (or a refusal) for a texture whose `Path` no longer exists would catch this before it costs the work. T-81's planted fault was this same "missing texture at open" path, found only afterwards.

**Outcome:** —

### The tool-level refusals T-75's Scope lists are mostly untested through the tools

**Where:** `tests/Carve.Server.Tests/MaterialToolTests.cs:151-165`, `src/Carve.Server/MaterialTools.cs:98-128`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

The Scope asks for "argument parsing and every refusal", each a tool error naming the argument. Through the real `carve`, `Refusals_name_the_argument_and_change_nothing` covers these: an unknown argument on each tool, `roughness` out of range, a missing texture, a non-`.png` path, an unknown material, an unknown face and an unknown group. These have no tool-level test:

- each name rule (empty, `:`, `#`, `carve`);
- a bad `base_colour` or `emissive` hex;
- `metallic` and `occlusion` out of range;
- a value of the wrong kind, such as `double_sided: "yes"` or `metallic: "1"`;
- a texture path naming a folder;
- `faces` and `group` both given, or neither;
- an unknown mesh in `set_material`.

Core's `MaterialTests` covers `Material.Problems`, but not `DefineMaterialTool.Run`'s own merging of argument errors with `Problems`. That merging has its own branch, `if (errors.Count == 0 || materialName is not null) ... if (field is not "name" || materialName is not null)`, and no test would fail if that branch dropped or doubled a problem.

**Outcome:** —

### The byte-identical guard leaves out the only committed version 6 file

**Where:** `tests/Carve.Core.Tests/MaterialFileTests.cs:225-243`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

`Every_committed_file_reads_and_writes_unchanged` is a fixed `InlineData` list of six files, versions 1 to 5. It does not include `samples/barrel-textured.carve`, which T-80 committed and which is the only version 6 file in the repo. The test also asserts `Assert.Empty(read.Model!.Materials)`, so it could not take a version 6 file as written. No test reads that sample and checks that it writes back to its own bytes. `MaterialsLoadTests.The_saved_file_is_the_sample_by_content` compares `Write(sample)` with `Write(saved)`: that is Write against Write, not against the committed text, and it runs only in the GPU suite. I checked with a scratch program that it does round-trip byte-identically today, so this is a coverage gap, not a bug. The version 6 writer's key order and default-skipping rule are guarded only by `A_version_6_file_is_written_as_the_format_says`'s synthetic model.

**Outcome:** —

### Texture paths are deduplicated without regard to case

**Where:** `src/Carve.Core/CarveFile.cs:502`
**Rubric:** 1 Correctness
**Severity:** Nit

`ReadVersion2` caches the textures it loads in a dictionary keyed by absolute path with `StringComparer.OrdinalIgnoreCase`. On a case-sensitive file system (Linux, or a case-sensitive Windows folder), `Tex/a.png` and `tex/a.png` are different files. The second would get the first's bytes, and its `MaterialTexture.Path` would be wrong. `Ordinal` (or the platform's path comparer) costs nothing on Windows, where the two paths resolve to the same file anyway.

**Outcome:** —

## Findings: export and render (claude-opus-5-5)

### Render's texture and material caches only grow, and nothing ever releases them

**Where:** `src/Carve.Server/PbrMaterials.cs:29-49`, `src/Carve.Server/RenderSystem.cs:54-58`
**Rubric:** 1 Correctness
**Severity:** Minor

`PbrMaterials` keeps every texture it uploads (`_textures`, keyed by PNG hash and colour space) and every engine `Material` it builds (`_materials`, keyed by the whole Carve record) for the life of the process, and it never calls `Destroy`. Every change to a material builds a new entry: a new PNG, but also a tweak to roughness alone. The old entries stay after no model uses them.

T-78's Scope (line 37) says textures are "held for the session (released as `IGraphicsResourceManager` requires, after every material holding them)". The Plan cut that back to "held for the process, like `Checker`", but `Checker` is one fixed texture, and these caches have no upper bound.

The engine docs give the limits this grows into:
- Each owned texture holds a bindless 2D slot until it is destroyed (`Texture.md`, "Bindless slot"; the heap has `sampler2D[8192]`), plus its VRAM. A 4096² RGBA8 texture with mips is about 85 MB.
- A code-built `Material` is "released only when a holder holds them" (changelog 0.78-0.94, T-906), so each one keeps a material-table entry. The table has 4096 entries, and a full table throws.

An agent that keeps regenerating a 2048² texture during one long session adds roughly 22 MB of VRAM per version, and none of it comes back. No test or As built line puts a bound on this.

**Outcome:** —

### Nothing tests the colour-space half of the texture cache key

**Where:** `src/Carve.Server/PbrMaterials.cs:29`, `:44`; `tests/Carve.Server.Tests/PbrEqualityTests.cs:181-205`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

The Plan says textures are cached by "(SHA-256 of the PNG bytes, sRGB), so one PNG used as both colour and data is two textures". No test uses one PNG in both colour spaces:
- The equality cube's five PNGs are all distinct, and `marker.png` is used only as base colour, twice.
- `MaterialRenderTests` uses `marker.png` only as base colour.
- The materials scenario gives every slot its own file.

So if the key dropped `Srgb`, a PNG used as both base colour and occlusion map would be served one texture with the wrong format, and every test would still pass. T-78's break table has no row for this.

**Outcome:** —

### "metallicFactor and roughnessFactor always written" is not what SharpGLTF writes

**Where:** `src/Carve.Server/ExportTool.cs:22`; `docs/tasks/T-76-materials-in-export.md:97` (As built table); `src/Carve.Server/GlbExport.cs:95`
**Rubric:** 7 Tracker hygiene (As built does not match the diff); also tool-description content
**Severity:** Minor

I checked in a scratch project with SharpGLTF.Toolkit 1.0.7, outside the repo and since deleted. `MaterialBuilder.WithMetallicRoughnessShader().WithMetallicRoughness(1f, 1f)` serialises as `"pbrMetallicRoughness": {}`, and `(0f, 1f)` as `{"metallicFactor": 0}`. SharpGLTF leaves out any factor equal to glTF's default (metallic 1, roughness 1).

So a material at roughness 1 has no `roughnessFactor`, and a metal at metallic 1 has no `metallicFactor`. A loader reads the same values, so the game sees no difference. But the tool description tells agents both factors are "always written", and so do T-76's Scope wording and As built table. The only test, `A_material_at_its_defaults_...` line 141, asserts `metallicFactor` 0 alone, so nothing caught it.

**Outcome:** —

### Byte-identical guard for gardener-soft and static coloured exports is not committed

**Where:** `docs/tasks/T-76-materials-in-export.md:117`; `src/Carve.Server/GlbExport.cs:48-56` (the colour-primitive code that changed)
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

T-76's Scope asks: "Every existing export (crate, gardener flat and soft, clips) is byte-identical to before, compared by hash". These hashes are pinned in committed tests:
- crate: `ExportViewerTests:214`, `UvExportTests:174-178`
- gardener and gardener clips: `CharacterLoadTests:32-34`

Nothing pins the hash of the soft gardener's export (no hash in `tests/Carve.SoftLoad.Tests`). Nothing pins a static export with colour faces either: the gatehouse checks have no hash, and the cube, L-block and crate in `UvExportTests` have no colours.

Static colour primitives now come from the rewritten `Primitives` (colours filtered by `MaterialOf`, new dictionary keys). Their byte-identity rests on a throwaway harness that was "not committed". A later regression in the static colour path would not fail any test.

**Outcome:** —

### `SkinnedGlb.Primitives` always equals `Materials.Count`

**Where:** `src/Carve.Server/SkinnedExport.cs:74`, `:93-94`, `:101-102`; `src/Carve.Server/ExportTool.cs:195`; `docs/tasks/T-76-materials-in-export.md:113`
**Rubric:** 3 No scope creep / 7 As built
**Severity:** Nit

`primitiveCount++` runs only where `materials.Add` runs. In the material branch both sit inside `if (!shared.TryGetValue(...))`, and in the colour branch they run one after the other. So the new `Primitives` field is always `Materials.Count`, and switching `ExportTool` from `glb.Materials.Count` to `glb.Primitives` changes nothing.

The As built's "with no materials it equals `Materials.Count`, as before" suggests the two differ once materials are used. They do not.

**Outcome:** —

### Export description contradicts itself on TEXCOORD_0

**Where:** `src/Carve.Server/ExportTool.cs:25-26`
**Rubric:** 1 Correctness (tool-description content)
**Severity:** Nit

The new text says `TEXCOORD_0` is written "when any face has UVs, or any material in use has a texture". The old clause after it still says "a model with no UVs has no TEXCOORD_0". With a textured material on an unmapped model (ruling: TEXCOORD_0 whenever a used material has a texture), the second sentence is false.

**Outcome:** —

## Findings: the viewer, sample textures and scenario (claude-opus-5-5)

### T-81's session under test ran with `--model sonnet`, a run-condition change nobody ruled on

**Where:** `docs/tasks/T-81-materials-acceptance-run.md:81`; Scope "Run conditions" and Notes "Open rules" (`any change to the run conditions above`)
**Rubric:** 7 Tracker hygiene (also 1 Correctness of the acceptance claim)
**Severity:** Major

As built says the session under test was "a fresh `claude -p --model sonnet` ... (the Notes say Sonnet)". The Notes' "**Sonnet.**" names the model for the member agent. It does not name the model for the session under test. The Scope's run conditions have no `--model` flag, and T-70 (line 30) fixed them as "no other flag". Earlier rounds (T-28 line 70, T-62 line 55, T-70 line 85) all ran the `claude -p` default model. Changing the run conditions is one of the listed stop-and-ask rules, and `### Rulings` records no ruling on it. The milestone Retrospective reports the round-1 pass but leaves this out of "Readings landed without asking". The run may well count, but the person needs to rule on it, or the retrospective needs to list it.

**Outcome:** —

### `viewer-angle` gets no rune check, and no viewer frame gets the orientation (P5) check

**Where:** `tests/Carve.MaterialsLoad.Tests/MaterialsLoadTests.cs:184`; `scenarios/Carve.Scenario.Materials/MaterialsScenario.cs:291-305`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

Scope: "In the viewer frames, the rune and the hoops as in Carve's frames", and "Every frame gets a positive check and one that a mirrored camera or a v-flipped texture would fail." The load test checks the rune only `if (view == "viewer-front")`. `viewer-angle` uses the eye (1.6, 1.2, 2.2), which is `barrel-front`'s eye, where the scenario does check the rune (`frontPool.RuneGlows()` with at least 8 samples). So the rune is in sight in `viewer-angle` but is never checked there. Also, `viewer-samples.tsv` writes only the view, x, y, class and face. It does not write the `FlipV` and `MirrorU` flags that `Pool.Orientation` uses, so no viewer frame checks that at least 3 samples would change class under a flip or a mirror. As built does not mention either gap.

**Outcome:** —

### The bottom cap (face 32, end grain) is in sight in `barrel-back` and the sheet's back tile but has no pixel check

**Where:** `scenarios/Carve.Scenario.Materials/MaterialsScenario.cs:146-149`, `:172`
**Rubric:** 2 Tests cover the Scope
**Severity:** Minor

`Rings()` runs only on `barrel-top` and the `+y` tile, so only on face 33. `backPool` runs `NoRune`, `IronAndRust` and `Wood`, and the sheet's `back` tile runs `NoRune` and `IronAndRust`. The Scope says `back` "shows the bottom cap", and the review frame `G:\My Drive\artifacts\carve\M-7\T-80\round-1\barrel-sheet.png` shows face 32's rings clearly in the back tile. Face 32's material is checked through `get_mesh`, and its UV centre through `CheckUvs`. No pixel shows that the right-hand disc of the end-grain texture is drawn there lit and warm. The Retrospective names the end grain as weak ("checks only 'pale ring: lit and warm'"), but it does not say the bottom cap gets no check at all.

**Outcome:** —

### `BarrelSpec` moved into its own file and linked into the materials scenario, which never uses it; the Uvs Layout row is now stale

**Where:** `scenarios/Carve.Scenario.Uvs/BarrelSpec.cs`, `scenarios/Carve.Scenario.Uvs/UvsScenario.cs` (lines removed), `scenarios/Carve.Scenario.Materials/Carve.Scenario.Materials.csproj:18`, `tests.json` (scenario-materials input `BarrelSpec.cs`), `CLAUDE.md:68`, `CLAUDE.md:69`
**Rubric:** 3 No scope creep / 7 Tracker hygiene
**Severity:** Minor

T-80 moved `BarrelSpec` out of `UvsScenario.cs` so it could be linked. Nothing in `scenarios/Carve.Scenario.Materials/*.cs` refers to `BarrelSpec`: the scenario `open`s `samples/barrel.carve`, and `MaterialsSpec` is its spec class. So the move, the link, the tests.json input and the "`BarrelSpec.cs` ... linked" text in the Materials Layout row are all unneeded. The move also left the Uvs Layout row wrong: it still says "`BarrelSpec` in `UvsScenario.cs`".

**Outcome:** —

### T-80's hash-guard tables and break-run folders, cited in As built, are not on disk

**Where:** `docs/tasks/T-80-textured-barrel-scenario.md` As built ("tables in `artifacts/review/T-80/base.sha256` and `new.sha256`", "each run in `artifacts/scenarios/materials/`"); `docs/milestones/M-7-materials-and-textures.md` Retrospective, "Known and left"
**Rubric:** 7 Tracker hygiene
**Severity:** Minor

`D:\Repos\carve\branches\milestone\M-7\artifacts\review\T-80\` exists but is empty, and `D:\Repos\carve\branches\task\T-80\` is empty. The Retrospective says that folder "holds the only copy of evidence ... T-80's hash tables and break-run folders". `G:\My Drive\artifacts\carve\M-7\T-80\` holds only the frame and texture review rounds. So nothing on disk now backs the hash guard (229 PNGs) or the break table (vflip, rune-column, metallic, mirror, and sky removed). As built and the Retrospective should say the evidence is lost, or point to where it now is.

**Outcome:** —

### T-80's own agents did not look at the frames, against the Scope's "Look at the frames, say what was seen"

**Where:** `docs/tasks/T-80-textured-barrel-scenario.md` As built, "Frame review" bullet ("Frames were not opened by this agent")
**Rubric:** 7 Tracker hygiene
**Severity:** Minor

The Scope asks the member to look at the frames, say what was seen, and then put them to the person. As built records only the person's "Looks good" and says the agent did not open the frames. No agent's own description of the frames appears anywhere. The Retrospective's "Every look above is a member's or the person's" suggests a member looked, but for T-80 only the person did.

**Outcome:** —

### The caption exclusion in `Pool` covers only the top row of a sheet

**Where:** `scenarios/Carve.Scenario.Materials/MaterialsChecks.cs:62-63`
**Rubric:** 1 Correctness
**Severity:** Nit

`shot.Sites((int)(520 * scale), (int)(28 * scale))` drops sites with frame x < 520 and y < 28. That fits one caption at the frame's top-left. But a single render draws no caption, and a sheet draws one in each tile's corner (`RenderTool.cs:219`). So the `+y` and `-x` tiles' captions, at frame y 262 to about 276, are not excluded. Today the barrel does not reach those corners (see `barrel-sheet.png`), so nothing goes wrong. The exclusion should be per tile, using `Shot.InFrame`'s origin.

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### set_material's worked example says check gives no warning, and its own test asserts four

**Where:** `src/Carve.Server/MaterialTools.cs:201-205`, `tests/Carve.Server.Tests/MaterialToolTests.cs:41-62`, `src/Carve.Core/MeshUvHealth.cs:28-36`
**Rubric:** 1 Correctness
**Severity:** Major

The `set_material` description's worked example ends with step 3: "set_material is this call, after which get_mesh lists faces 2 and 3 under crate and check has no warning". Step 1 box-maps only faces 2 and 3 of the cube (`project_uvs {"method":"box","faces":[2,3]}`). That leaves the mesh partly mapped, and `MeshUvHealth` then warns `no UVs, so the face samples the texture's corner` for every other live face (0, 1, 4, 5). The test that runs this exact example (`The_two_descriptions_examples_define_and_assign_on_the_cube_and_get_mesh_and_check_read_it_back`) asserts the opposite of the description: `Assert.Equal(4, warnings.Count(w => w.EndsWith("no UVs, so the face samples the texture's corner")))`, under the comment "the other four are the generic unmapped ones". T-75's Scope says the descriptions are content, and T-81 checks that a fresh agent can work from them alone. An agent following this example is told to expect a silent `check`, gets four warnings, and has nothing in the description to explain them. One of the two has to change: the example (map all six faces, or say the other four faces warn) or the test.

**Outcome:** —

### RenderSky's summary still says the viewer links the file, against T-79's ruling 4

**Where:** `src/Carve.Server/RenderSky.cs:6-8`, `spikes/Carve.ModelViewer/ViewerSystem.cs:148`, `docs/tasks/T-79-viewer-sky-and-material-report.md:72`
**Rubric:** 1 Correctness
**Severity:** Minor

T-78 wrote `RenderSky`'s doc comment: "The one definition, in engine types only, so that the viewer links this file and lights a model as `render` does." T-79's ruling 4 then decided against linking. The viewer uses `ProceduralSky.Default` directly, and the guard `The_viewer_references_no_Carve_code` forbids a linked Carve source. `ViewerSystem.cs:148` now says the opposite of `RenderSky.cs`: "the viewer links no Carve code, so it is repeated here". The comment on the product side was not updated after the ruling. It now describes a link that does not exist, and that a guard forbids. Someone who trusts it would expect a change to `RenderSky.Sky` to reach the viewer, and it does not.

**Outcome:** —

### check's reason for skipping cross-material overlap is false for materials that share a PNG, as the committed sample's do

**Where:** `src/Carve.Server/CheckTool.cs:32`, `src/Carve.Core/MeshUvHealth.cs:61-63`, `samples/barrel-textured.carve:4,7`, `src/Carve.Server/ExportTool.cs` (description: "a PNG used by several materials or channels is embedded once")
**Rubric:** 1 Correctness
**Severity:** Minor

T-80's ruling 5 is only the rule: no overlap warning when both faces have a material and the materials differ. The `check` description and the code comment add a reason that the ruling does not give: "since they sample different textures". Materials may share PNGs, and export documents that it embeds a shared PNG once. The committed sample does exactly that: `staves` and `rune-stave` both use `staves-base.png`, `staves-normal.png` and `staves-occlusion.png`. On faces with overlapping UVs, such a pair does show the same texels, yet `check` stays silent. An agent who reads the stated reason will believe a silent `check` means no shared texels across materials. The ruled rule can stay. The reason should go, or should say that the skip holds even when the textures are the same.

**Outcome:** —

### The retrospective says suites were carried from the last code commits, but three code commits follow it

**Where:** `docs/milestones/M-7-materials-and-textures.md:171`, `docs/milestones/M-7-materials-and-textures.md:226`; commits `2da69a7`, `d20e60f`, `bcc7a9f`
**Rubric:** 7 Tracker hygiene (and 4 Honest Verify scope)
**Severity:** Minor

The retrospective (`5f982eb`) says: "All 15 were carried from `ed16e5d` and `b35b0e9`, the last code commits, and T-81 added only docs." Three commits after it change product code and tests:

- `2da69a7` changes `Material.cs`, `MaterialTools.cs` (it restructures how `define_material` builds and validates the material), `CarveFile.cs`, `MeshHealth.cs` and `GlbExport.cs`;
- `d20e60f` changes `GlbExport.HashOf` and `PbrMaterials`' texture cache key;
- `bcc7a9f` changes `MaterialExportTests` and `MaterialRenderTests`.

No member's As built and nothing in the milestone file records these commits or a run on them. The milestone's carried verify therefore no longer covers the head being merged. I ran Core's `~Material` tests (96 passed) and Carve.Server.Tests' unit `MaterialExport|MaterialSubmesh|MaterialStore` tests (21 passed) on the head, and both pass. The regression and GPU suites that cover `define_material` and `PbrMaterials` (server-regression, scenario-materials) have not run on these commits, as far as the tracker shows.

**Outcome:** —

### check's description quotes the occlusion warning without its last clause

**Where:** `src/Carve.Server/CheckTool.cs:33`, `src/Carve.Core/MeshHealth.cs:98`
**Rubric:** 1 Correctness
**Severity:** Nit

Every other warning in `check`'s description is quoted exactly as `check` emits it. The occlusion warning is quoted as `"occlusion is 0.5 but the material has no occlusion_texture, so it has no effect"`, but `MeshHealth` emits `... so it has no effect (glTF scales only the map)`. An agent or test that matches the quoted text exactly will not match the real warning.

**Outcome:** —

### view.json spells the base colour two ways in one material object

**Where:** `spikes/Carve.ModelViewer/ViewerSystem.cs:210`, `spikes/Carve.ModelViewer/ViewerSystem.cs:220`
**Rubric:** 1 Correctness
**Severity:** Nit

Each material entry has the factor as `baseColor`, which was already there, and T-79 added its texture slot as `textures.baseColour`. The two spellings name the same channel inside one object. The neighbouring keys (`metallicRoughness`, `emissive`) keep a single spelling. Anyone writing a check against `view.json` has to remember which key takes which spelling.

**Outcome:** —

## Summary

Round 1 of M-7's review, after the refactor pass (`bcc7a9f`) on `milestone/M-7`, 56 commits ahead of `main` at `5bc4559`; `main` had not moved, so no rebase was needed. About 5,500 lines in 72 files outside `docs/`.

Four reviewers, all Opus 5.5 (at least as strong as every implementer: Opus on T-74 and T-78, Sonnet on the rest), run in parallel. The diff was split by area, because the members are separate pieces that meet at the material record:

- **Core and the material tools:** T-74, T-75, and T-80's change to `check`.
- **Export and render:** T-76 and T-78.
- **The viewer, sample textures and scenario:** T-77, T-79, T-80 and T-81.
- **The whole diff:** one material field and one texture path followed end to end, for what falls between the parts.

25 findings: 3 Major (two are the same defect, `set_material`'s worked example, found independently), 16 Minor, 6 Nit. The person ruled four (2026-11-16), each as recommended:

- T-81's pass counts, with its `--model sonnet` recorded as a run-condition change ruled after the fact;
- `check`'s cross-material rule stays, and its false reason goes;
- `save` warns, naming each held texture whose file is no longer at its path;
- render's texture and material caches are fixed now, releasing what no model uses.
