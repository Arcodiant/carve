---
description: >-
  export writes each material as a glTF metallic-roughness material with its textures embedded, static and skinned, and the engine's loader reads every field back
---
# T-76 — Materials in export

**Status:** Open

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-74

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character, scenario-soft, scenario-uvs, scenario-cuts, scenario-gatehouse, scenario-rig

**Perf:** skip: Carve declares no heavy suites

## Goal

A model's materials reach the game inside the one `.glb`: every PBR field and texture, which the engine's own loader turns into the material Carve authored.

## Scope

- **Static export (`GlbExport`):**
  - Faces with a material are written as one primitive per material, under the material's own name. Colour and uncoloured faces keep today's primitives and names.
  - Primitives come in today's order for colours, then the materials in order of first use by face.
  - Each material is written as glTF `pbrMetallicRoughness`, with these fields:
    - base colour factor, the linear form of the sRGB hex, as colours are;
    - metallic and roughness factors, always written (glTF's default metallic is 1);
    - emissive factor, linear, and written only when not black;
    - occlusion strength, written with the occlusion texture;
    - `doubleSided`;
    - each texture.
  - Textures are embedded in the `.glb` as their PNG bytes, unchanged. Each distinct set of bytes is written once, however many materials use it.
  - The texture coordinates are M-6's `TEXCOORD_0`.
- **Skinned export (`SkinnedExport`):** the same, per piece. A material's primitive is named after the material, not `<mesh>:<group>`, and several pieces may share one glTF material. The submesh count in the limit warning counts material primitives too.
- **Warnings and refusals:**
  - `export` warns, and does not refuse, when a face's material has a texture but the face has no UVs, naming the material and the faces (the same rule as T-75's `check`).
  - `ExportResult`'s material list reports each material written.
- **`clips` mode** writes no materials, as today.
- **Tests:**
  - `unit`, reading the exported bytes through the engine's parsers (`IModelGeometryLoader` and `ParsedMaterial`, as `tests/Carve.CharacterLoad.Tests` does), for both a static and a skinned model:
    - every `ParsedMaterial` field equals the material given;
    - each texture's bytes equal the PNG given;
    - a texture shared by two materials is embedded once;
    - no parse warnings;
    - primitive order and names.
  - Every existing export (crate, gardener flat and soft, clips) is byte-identical to before, compared by hash.
  - `regression`: one `export` through the real `carve` of a textured cube, loaded back as above.

## Notes

- **Sonnet.** SharpGLTF's `MaterialBuilder` takes every channel. The record is T-74's.
- **Checked at planning (2026-11-11):** a `.glb` written with SharpGLTF 1.0.7, with embedded PNGs for base colour and metallic-roughness plus metallic, roughness and emissive factors, loaded through `IModelLoader` in `spikes/Carve.ModelViewer`. It drew with `pbr: true` and every factor as written. The texel at image (0, 0) landed at UV (0, 0), and the metallic-roughness map's channels read as glTF packs them. The probe's files are in `%TEMP%\m7-probe`.
- **Engine need 7** (D-1): the engine's glTF loader reads no `alphaMode` or `alphaCutoff`, so Carve's materials are opaque. Write no alpha fields.
- Runs side by side with T-75 and T-77. In this wave it owns `GlbExport.cs`, `SkinnedExport.cs`, `ExportTool.cs` and their tests.
- **Write down for the dependants**, in this file's As built: the primitive order and names, and how a material maps to glTF. T-78's equality test exports through this code.
- **Before the first code**, read this Scope against T-74's As built, T-59's `TEXCOORD_0` and vertex splitting, and T-51 (per-game limits) if it has landed. Stop if they disagree.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - any change to an existing export's bytes;
  - a material-name clash T-74's name rules do not prevent;
  - any loosened check.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame and exported file, by hash before and after;
  - each new check shown failing on a broken product, as a table (include a texture written with its v flipped and metallic left at glTF's default);
  - `finish` read whole, with its `Merge task/T-NN` line;
  - artifacts by job id;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-76/`.
- Run `finish` from inside the worktree and leave the folder.

### Checked against landed code (2026-11-16)

- **Static export sees a mesh, not a model.** `ExportTool` passes `model.Combined` to `GlbExport.Write(PolygonMesh?, …)`, and the material definitions live on `Model` (T-74). Pass the model's `Materials` into `GlbExport` as well. `Combined` carries each face's material name (T-74).
- **Skinned names the uncoloured primitive after the bare mesh name**, not `carve` (`SkinnedExport.Materials`, line 128). So a material named like a mesh clashes, and T-74's name rules do not prevent it. This is T-74's open question Q1, and its recommended answer puts the fix here.
- **No `TEXCOORD_0` when no face has UVs.** `GlbExport.HasUvs` writes UVs only when some live face is mapped, so a textured material on an unmapped model would reference texcoords that are not there. Suggest writing `TEXCOORD_0` (unmapped corners at (0, 0), as today) whenever a used material has a texture. No existing export has a material, so no existing bytes change. Put this to the person.
- **An export UV warning already exists.** `GlbExport.UvWarnings` warns per piece (`<mesh>: N faces have no UVs; …`) whenever any piece has UVs. Suggest the Scope say how the new per-material warning sits beside it: an addition, or the same faces named once.
- **The submesh limit is `GameLimits.Warnings`**, not `SkinnedExport`. It counts distinct colours and its text says "colours". Counting materials means editing `GameLimits.cs` and possibly that text, which is outside the owned list and an existing warning's wording.
- **`ExportTool`'s description** (line 20) says the export holds only `carve` and `carve #RRGGBB` materials. It is content and needs the material sentence. `ExportTool.Materials` (line 199) summarises colours only.

### Rulings (2026-11-16, the person)

- A material may share a mesh's name. Skinned export renames the uncoloured primitive's material only when the names clash.
- Export writes `TEXCOORD_0` whenever a material in use has a texture (faces without UVs get (0, 0) and are warned of already).
