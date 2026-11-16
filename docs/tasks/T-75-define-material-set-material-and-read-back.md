---
description: >-
  An agent defines named materials from PNG textures and PBR values, assigns them to faces or groups, and reads them back through get_mesh and check
---
# T-75 — define_material, set_material and read-back

**Status:** Done

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-74

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-cuts, scenario-gatehouse, scenario-rig

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent gives faces materials through MCP: it defines a named material from PNG files and PBR values, assigns it to faces or a group, and reads back what each face has.

## Scope

- **`define_material`** (new file `src/Carve.Server/MaterialTools.cs`), one undo step:
  - Arguments: `name`, `base_colour`, `metallic`, `roughness`, `emissive`, `occlusion`, `double_sided`, and the five texture paths `base_colour_texture`, `metallic_roughness_texture`, `normal_texture`, `emissive_texture` and `occlusion_texture`.
  - Paths go through `ToolPaths.Resolve` with the verb "reads", as `open` does: relative to the working folder, or absolute. The file is read and checked when the material is defined.
  - It returns the material as stored: every field, each texture's absolute path and size, and whether it replaced an existing material.
- **`set_material`**, one undo step: `mesh`, then `faces` or `group` (exactly as `set_colour` takes them), and `material`. It clears those faces' colours. `set_colour` now clears a face's material, and its description says so.
- **Read-back:**
  - `get_mesh` reports each face's material beside its colour, and the model's materials (every field and each texture's path and size).
  - `check` warns of a face whose material has a texture but whose corners have no UVs (M-6's unmapped). The warning names the material and the faces, in the form `check`'s other UV warnings use.
- **Refusals**, each a tool error that names the argument:
  - every problem T-74's validation reports;
  - a texture path that does not exist: the error lists the PNG files in the folder it looked in, as `open`'s error lists `.carve` files;
  - a file that is not a PNG;
  - an unknown material in `set_material`, listing the defined names;
  - an unknown mesh, face or group, as `set_colour` refuses them;
  - unknown arguments, as every tool refuses them.
- **The descriptions are content.**
  - `define_material` says what each field does in the game's PBR shading, in a sentence each: metal reads by reflection, roughness 1 is matte, the normal map is tangent-space and linear, the metallic-roughness map has roughness in G and metallic in B, the occlusion map is in R, and base colour and emissive are sRGB. It says that the defaults are matte white, that textures need UVs (M-6), and that materials are opaque.
  - `set_material` carries a worked example on `define_mesh`'s unit cube: a material with a texture on two faces after `project_uvs`.
- **Tests:**
  - `unit`: argument parsing and every refusal.
  - `regression`, through the real `carve`: define, assign by faces and by group, replace, colour and material clearing each other, `undo` of each, the `get_mesh` and `check` read-back, and a save and open round trip through the tools with a texture in a subfolder.
  - Use a small fixture PNG committed under `tests/`. T-77 makes the sample's textures in the same wave.

## Notes

- **Sonnet.** The record and the rules are T-74's. This task is tool surface and text.
- Runs side by side with T-76 and T-77. In this wave it owns:
  - `MaterialTools.cs`;
  - the tool registration in `Program.cs`;
  - `FaceStyleTools.cs`, `GetMeshTool.cs` and `CheckTool.cs`, and their tests.
- **Write down for the dependants**, in this file's As built: the tool names and arguments as registered, the `get_mesh` shape, and the `check` warning's text (T-80 and T-81 drive them).
- **Before the first code**, read this Scope against T-74's As built, and against what M-6 landed (`set_uvs`, `project_uvs`, `check`'s UV warnings). Stop if they disagree.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - any change to an existing tool's behaviour beyond `set_colour` clearing a material;
  - an expected schema or unknown-argument text changed beyond adding the new tools (M-16 ruled those may be updated for new arguments only);
  - any loosened check.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame, by hash before and after;
  - each new check shown failing on a broken product, as a table;
  - a wording fix searches every text that says the same thing;
  - `finish` read whole, with its `Merge task/T-NN` line;
  - artifacts by job id;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-75/`.
- Run `finish` from inside the worktree and leave the folder.

### Checked against landed code (2026-11-16)

- **`check` already warns on unmapped faces.** `MeshUvHealth` gives `faces[n]: no UVs, so the face samples the texture's corner` for every unmapped live face, but only when the mesh has at least one mapped face.
  - In a partly mapped mesh, a textured-material face with no UVs would get that warning and the new one.
  - In a mesh with no UVs at all, it gets only the new one.
  
  Suggest the Scope say which applies: one warning per face that names the material, either replacing the existing one for that face or added only where it does not fire. Put this to the person; T-74's Plan collects the other open rules.
- **Files outside the owned list.**
  - `set_material` and `define_material` need the model's materials. `ModelStore.Restyle` hands the change a mesh only, so a model-level store method is needed in `ModelStore.cs`.
  - `save` and `open` must pass the `.carve` file's folder to `CarveFile.Write`/`Read` (T-74's Plan: `Write(Model, folder)`, `Read(text, folder)`), in `SaveTool.cs` and `OpenTool.cs`.
  
  Suggest adding all three files to this task's ownership. T-76 does not touch them.
- **`save mesh=` and `open mesh=`** carry materials by T-74's rules for Q2 and Q3. The regression test should cover the ruled behaviour.

### Rulings (2026-11-16, the person)

- `merge_faces`' warning names a face's material rather than saying "colour none".
- In `check`, the material warning for a textured face with no UVs replaces the generic unmapped-face warning on that face: one warning per face.
- `save mesh=` writes only the materials that mesh uses; `open mesh=` refuses a same-named material defined differently; a texture with no relative path from the save folder refuses the save (all three in T-74's Rulings).
- Occlusion strength set without an occlusion map has no effect (glTF scales only the map); render and export ignore it, and `define_material` or `check` warns that it has no effect (the person, 2026-11-16, from T-78's research).
- `define_material` sets the emissive colour to #FFFFFF when an emissive texture is given and no emissive colour is set (in glTF the glow is emissive colour times emissive texture, so black never glows); an explicitly given colour still wins (the person, 2026-11-16, from T-76's work).

### As built (2026-11-16, Claude Sonnet 5.5)

The Scope with the Rulings. Differences are called out.

**Tools as registered** (`src/Carve.Server/MaterialTools.cs`, in `Program.cs`'s list after `set_colour`; each one undo step, each argument set closed):

- `define_material(name, base_colour?, metallic?, roughness?, emissive?, occlusion?, double_sided?, base_colour_texture?, metallic_roughness_texture?, normal_texture?, emissive_texture?, occlusion_texture?)`. Fields left out are the defaults, and a redefinition replaces the material whole (fields left out go back to their defaults). Texture paths go through `ToolPaths.Resolve(..., ".png", verb: "reads")`, relative to the working folder or absolute; the file is read with `MaterialTexture.Load` when called. Result: `{"replaced":bool,"material":{name, base_colour, metallic, roughness, emissive, occlusion, double_sided, base_colour_texture, metallic_roughness_texture, normal_texture, emissive_texture, occlusion_texture},"warnings":[]}`; a texture is `null` or `{"path":"<absolute>","width":w,"height":h}`. All problems are listed in one error (`define_material failed with N problem(s)`), each as `<argument>: <text>`: T-74's `Material.Problems`, a missing file (`no file at <abs>; the .png files in <folder> (n) are: a.png, b.png`, or no `.png` files, or the folder does not exist), a folder, a path not ending `.png` (`define_material reads .png files`), a file that is not a PNG or is outside 1 to 4096, a value of the wrong kind, an unknown argument.
- `set_material(material, faces | group, mesh?)`. Result `{"mesh","material","faces":[ids sorted],"warnings":[...]}`, `warnings` only when the material has a texture and some of the faces have no UVs (`faces[0, 5]: no UVs, so the texture of 'wood' is not mapped onto them; give them UVs with project_uvs, unwrap or set_uvs`; it still assigns). Refusals: `material: no material named 'x'; the materials are 'a', 'b'` (or `the model defines none yet, define one with define_material`), and `set_colour`'s for faces and group. It clears the faces' colours (T-74's `WithMaterial`); `set_colour`'s description now says it clears a material.
- `ModelStore.DefineMaterial(tool, material, out replaced, out problems)` and a `Restyle` overload whose change is given the model beside the mesh. `OpenTool.ListLimit` is internal for the shared `.png` listing.
- `save` calls `CarveFile.TextureProblem(model, folder)` (its text, prefixed `save: `) then `Write(model, folder)`; `open` calls `Read(text, folder)`. Both descriptions say how materials and textures travel.

**`get_mesh` shape.** Unchanged for a model with no materials. With materials: `materials:[{...every field as define_material reports it...}]` (the whole model's, definition order) before `uvs`, and `face_materials:[{"material":"wood","faces":[ids]}]` when a face of the mesh has one (grouped by name, ids ascending), after `groups`. A face with a material is absent from `colours`.

**`check` text.** In `src/Carve.Core/MeshHealth.cs`/`MeshUvHealth.cs`: `MeshHealth.Create(mesh, materials)` (the one-argument form is as before).

- `faces[3]: material 'wood' has a texture but the face has no UVs, so it samples the texture's corner`, one per such face (a material with any of the five textures), replacing the generic `no UVs, so the face samples the texture's corner` on that face (Ruling), and given in a mesh with no UVs at all too, where it is the only UV warning (no `uvs` stats, as before). A face of a plain material, or a coloured one, gets the generic warning only where it already did.
- `materials['wood']: occlusion is 0.5 but the material has no occlusion_texture, so it has no effect (glTF scales only the map)`, for a material the mesh's faces use (kind `MeshWarningKind.MaterialNoEffect`). `define_material` returns the same fact as `occlusion: 0.5 has no effect without an occlusion_texture (glTF scales only the map); give one or leave occlusion at 1`.
- `check` gives `ok:false` for either, as it does for any warning.

**`merge_faces` wording** (`FaceMerge.ColourWarnings`): colours only is as before, byte for byte. When either face has a material: `faces[1]: merged with material 'wood', and the merged face keeps faces[0]'s look, no colour or material` (looks are `material 'x'`, `colour #RRGGBB` or `no colour or material`), and two faces with the same material and colour do not warn. The tool description says so.

**Emissive ruling.** Built as ruled: `emissive_texture` with no `emissive` stores `#FFFFFF`, shown in the result; the description says so. Test: `An_emissive_texture_turns_the_default_emissive_white_unless_a_colour_is_given`.

**Differences from the Scope.**

- **`unit` refusal tests are store and Core level, the tool refusals are `regression`.** `Carve.Server.Tests` never links the server's tools ("started as real processes, never linked"), so `define_material`/`set_material` argument parsing runs through the real `carve` in `MaterialToolTests` (Tier regression, no GPU used). The unit tier has `MaterialStoreTests` (the two store steps, undo, a refused change) and `MaterialHealthTests` in `Carve.Core.Tests` (the check warnings and merge wording).
- The `Core` files `MeshHealth.cs`, `MeshUvHealth.cs`, `MeshReport.cs` and `FaceMerge.cs` are touched (a model-aware `MeshHealth.Create`, a warning kind, the merge text); the Notes' ownership list named the tools, but the Rulings need these.
- The fixture is `tests/Carve.Server.Tests/Fixtures/swatch.png` (4 x 4), listed in `tests.json`'s `server-unit` and `server-regression` inputs.
- The occlusion-without-map warning, and `set_material`'s no-UV warning, are my additions on the Rulings' "define_material or check warns"; the second is not in the Scope.
- Not changed: T-76's export code. The export description still says what it did; it does not know materials until T-76.

**Tests.** `MaterialToolTests` (18 cases, regression): each description's example run as is on the cube, `get_mesh` read-back, `check` in a mesh with and without UVs, occlusion, replace, colour and material clearing each other and `undo` of each, group assignment, every refusal, a missing texture's `.png` listing, a non-PNG, an unknown material, save and open through a new process with a texture in `textures/wood/` and the file saved into `out/`, a deleted texture refusing the open, `save mesh=` writing only the used materials, `open mesh=` refusing a differently defined one.

**Each new check failing on a broken product:**

| Broken on purpose | Tests that failed |
|---|---|
| the textured warning no longer replaces the generic one | `In_a_partly_mapped_mesh_the_material_warning_replaces_the_generic_one_for_that_face_only`, `Check_names_the_material_of_a_textured_face_without_UVs_once_per_face_...` |
| `open` ignores the file's folder | `Save_and_open_round_trip_materials_with_a_texture_in_a_subfolder_...`, `Open_refuses_a_file_whose_texture_is_gone_...` |
| the occlusion warning never fires | `Occlusion_strength_without_a_map_warns_in_define_material_and_in_check_...` |
