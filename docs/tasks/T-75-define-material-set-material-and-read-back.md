---
description: >-
  An agent defines named materials from PNG textures and PBR values, assigns them to faces or groups, and reads them back through get_mesh and check
---
# T-75 — define_material, set_material and read-back

**Status:** Open

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-74

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft

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
