---
description: >-
  export writes each material as a glTF metallic-roughness material with its textures embedded, static and skinned, and the engine's loader reads every field back
---
# T-76 — Materials in export

**Status:** In Progress

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

### As built (draft, 2026-11-16, Claude Sonnet 5.5; verify, the regression test and finish are still to come, see Hand-off)

**How a material is written** (`GlbExport.Material(Carve.Core.Material, Images)`, shared by static and skinned export), as read back by the engine's `ParsedMaterial`:

| Carve field | glTF | `ParsedMaterial` |
|---|---|---|
| `BaseColour` | `pbrMetallicRoughness.baseColorFactor` = `FaceStyle.ToLinear(hex)`, alpha 1 | `BaseColor` (same Vector4) |
| `Metallic`, `Roughness` | `metallicFactor`, `roughnessFactor`, always written | `Metallic`, `Roughness`; `Pbr` true |
| `Emissive` | `emissiveFactor` = linear RGB, written only when not black | `EmissiveColor` (zero when absent) |
| `Occlusion` | `occlusionTexture.strength`, written only with `OcclusionTexture` (without a map it is not written) | `Occlusion` (1 when no map) |
| `DoubleSided` | `doubleSided` | `DoubleSided` |
| `BaseColourTexture` | `pbrMetallicRoughness.baseColorTexture` | `TextureBytes` (flat) |
| `MetallicRoughnessTexture` | `pbrMetallicRoughness.metallicRoughnessTexture` | `MetallicRoughnessTexture.Bytes` |
| `NormalTexture` | `normalTexture` | `NormalTexture.Bytes` |
| `EmissiveTexture` | `emissiveTexture` | `EmissiveTexture.Bytes` |
| `OcclusionTexture` | `occlusionTexture` | `OcclusionTexture.Bytes` |

- Each texture is the PNG bytes held in `MaterialTexture.Png`, embedded unchanged; one image per distinct byte set (SharpGLTF writes identical images once, `Images` keys them by SHA-256 as well). The loader hands every reference to one image the same `Bytes` array. No `sampler`, no `alphaMode` or `alphaCutoff` (engine needs 7 and 8).
- **An emissive texture shows only with a non-black `Emissive`**: the factor is left out when black (the Scope) and the loader then reads `EmissiveColor` as zero, so a texture with the default black emissive does not glow. T-75's `define_material` text should say so (this export's description does). Question for the person if that is not wanted: write a white factor when an emissive texture is set with black emissive.
- `TEXCOORD_0` is written whenever a face has UVs or any material in use (on a live face) has any texture; unmapped corners are (0, 0), as before. The UV of each corner is Carve's, unflipped.

**Static file** (`GlbExport.Write(mesh, triangles, name, materials)`; the old overloads call it with no materials, so every existing byte is unchanged): primitives are today's colour primitives (`carve` first when any face has neither colour nor material, then `carve #RRGGBB` in order of first use), then one per material in order of first use by face, named for the material (the glTF material's name is the primitive's). `GlbExport.MaterialNames(mesh, materials)` lists them. `ExportTool` passes `model.Materials` and `Combined`.

**Skinned file** (`SkinnedExport.Primitives(piece, taken)`): per piece, today's primitives (uncoloured and colours in order of first use), then the piece's materials in order of first use, named for the material. A material used by several pieces is one glTF material and one primitive, at the place of the first piece using it, holding every piece's faces of it (the mesh builder keeps one primitive per material). The uncoloured primitive's material is `<mesh>`, renamed `<mesh>:uncoloured` (with `_` added while a group of the piece or a colour hex has that name) only when a material in use anywhere in the model has the mesh's name; a material name holds no `:` so nothing else can clash. `SkinnedGlb.Materials` lists each glTF material once (in order written) and the new `SkinnedGlb.Primitives` is the submesh count that `GameLimits.Warnings` now takes (`ExportTool` passes it; with no materials it equals `Materials.Count`, as before). `SkinnedExport.Materials(piece)` still returns the colour primitives only.

**Warnings.** `GlbExport.MaterialUvWarnings(model)`: `material 'wood' has a texture, but N faces using it have no UVs; their corners are written as (0, 0) (box faces 1, 2, ...)` (faces per mesh, the first five of each then `and K more`; `1 face ... its corners` for one), after the limit and UV warnings in both modes. The generic `UvWarnings` no longer names a face whose material has a texture (the same rule the person gave `check`: the material's warning replaces the generic one on that face); with no material every warning is as before. `GameLimits.Warnings`: unchanged text when no material is used; otherwise `N submeshes, M colours and K materials, above the game's max_submeshes X; share colours and materials between pieces where you can`. Clips mode writes no materials or images. The tool description is updated (materials, TEXCOORD_0, the `<mesh>:uncoloured` rule, a worked example).

**Byte-identical guard.** A throwaway harness (not committed) exported the five committed samples (barrel, gatehouse, leaky-shed static; gardener and gardener-soft static, skinned and clips) before and after: the SHA-256 lists are identical. The scenario suites in Verify are the frame and committed-output check.

**Tests.** `tests/Carve.Server.Tests/MaterialExportTests.cs` (unit, 14): primitive order and names, every field and texture through `IModelGeometryLoader.Parse`, defaults, shared texture once, no alpha/sampler/strength-without-map, TEXCOORD_0 rules, UV bits unflipped, warnings, skinned order, sharing and the name clash, clips, determinism, the limit warning. Each new check failing on a broken product (the table is also in the ignored `artifacts/review/T-76/mutation-table.md` of the task worktree):

| Broken on purpose | Tests that failed |
|---|---|
| UV v flipped (1 - v) | Each_corners_UV_is_written_as_Carve_holds_it_so_a_texture_lands_unflipped |
| metallic left at glTF's default | A_material_at_its_defaults_is_matte_white_single_sided_with_no_emissive_or_occlusion_written,Every_field_and_texture_is_read_back_by_the_engines_parser,A_skinned_model_writes_primitives_per_piece_and_a_material_shared_between_pieces_is_one_primitive |
| a texture embedded once per use (my cache bypassed) | none: SharpGLTF writes identical image bytes once itself, so the test guards the output, not the cache |
| emissive factor always written | none: SharpGLTF omits an all-zero emissive factor itself |
| skinned always renames the uncoloured primitive | A_material_named_like_a_mesh_renames_that_pieces_uncoloured_primitive_only_when_they_clash,A_skinned_model_writes_primitives_per_piece_and_a_material_shared_between_pieces_is_one_primitive |
| TEXCOORD_0 only when a face has UVs | A_textured_material_writes_TEXCOORD_0_even_when_no_face_has_UVs_and_an_untextured_one_does_not |
 `MaterialExportToolTests.cs` (regression) is written against T-75's tool names and arguments (`define_material`, `set_material`).

### Hand-off (Claude Sonnet 5.5, 2026-11-16)

- **Done:** all the code (`GlbExport.cs`, `SkinnedExport.cs`, `ExportTool.cs` and description, `GameLimits.cs`), the 14 unit tests (pass; the whole `Tier=unit` Server suite passes), the byte-identical hashes, the mutation table. Code committed as `4aae6cd`.
- **Left:** (1) `MaterialExportToolTests` (the real-`carve` regression test) calls `define_material` and `set_material`, which T-75 has not landed. It cannot pass until T-75 lands, and `open` still reads a `.carve` without its folder, so no existing tool can give carve a textured material. After T-75 lands: `git rebase origin/milestone/M-7`, check the test's argument names against T-75's (`faces`, `material`, `name`, `*_texture`, `double_sided`) and that `set_uvs` still takes that shape, then run it. (2) `cairn task T-76 verify` has not been run. (3) Finalise the As built, commit with the `Co-Authored-By` trailer, `cairn lint`, `finish`.
- **Surprising:** the mesh builder merges two pieces' uses of one material into a single primitive (recorded above). The untracked `tests/Carve.Server.Tests/ZzHashHarness.cs` is a throwaway hash harness (env `HASH_OUT`, `HASH_ROOT`); delete it before the final commit.
