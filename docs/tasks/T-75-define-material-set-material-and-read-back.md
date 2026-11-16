---
description: >-
  An agent defines named materials from PNG textures and PBR values, assigns them to faces or groups, and reads them back through get_mesh and check
---
# T-75 — define_material, set_material and read-back

**Status:** In Progress

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
