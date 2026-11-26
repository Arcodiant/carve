---
description: >-
  export embeds each graph slot's evaluated PNG in the .glb, with one <material>@<mesh> copy per mesh when a mesh-reading material is used by several, and render_glb agrees with render
---
# T-164 — Graph materials in export

**Status:** Done

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-163

**Verify:** server-unit, server-regression, scenario-export, scenario-materials, scenario-planks, scenario-parts

## Goal

`export` writes a graph material into the `.glb` as ordinary PNG textures, baked from the model as it is at that
moment, so the game loads it with no knowledge of graphs (D-7). A material that reads the mesh and is used by several
meshes is written once per mesh.

## Scope

- **`GlbExport`, `SkinnedExport`, `GlbParts`, `ExportTool`**: each graph slot of each material in use is baked through
  T-163's call and cache (the same key, so an export after a render of the same model bakes nothing) and embedded as
  the slot's image, exactly as a PNG texture is embedded (images shared by hash, as `ImageBuilder.Of` does).
- **Per-mesh copies**:
  - a material with no mesh-reading slot is one glTF material under its own name, whatever uses it;
  - a material with a mesh-reading slot used by faces of one mesh is one glTF material under its own name;
  - used by faces of several meshes, it is written once per mesh as `<material>@<mesh>` (`weathered-iron@barrel`,
    `weathered-iron@lid`), each with that mesh's bakes, and its plain name is not written. The result's `warnings`
    carry one line saying so and naming the copies.
  - In a part tree, instances of one mesh share one copy; a piece export (one mesh) follows the same rule on the
    meshes it writes.
- `export`'s result gains `baked` and the 5 s warning, as `render`'s does (T-163's gathering, not a second one), and its
  description the paragraph on graph materials: baked at export from the graph files as they are on disk then, the
  sizes, the copies' names. `ExportTool`'s UV warning ("has a texture, but N faces using it have no UVs") covers
  graph materials.
- **`render_glb` agrees with `render`**: a `server-regression` class exports the raised-hoop barrel with T-162's two
  graph materials at 512 and compares `render_glb` with `render` from the front and three-quarter under D-6's frame
  rule (`ComparisonPng`), with a control that must fail (the file exported with the graphs' `wear_amount` and
  `grime_amount` at 0 through a copy of the graphs).
- **Tests**:
  - `server-unit`: the three naming cases, read back from the written `.glb` with SharpGLTF; images shared between
    slots and materials where bytes are equal; `baked` empty after a render of the same model; the bytes of an export
    equal on two runs.
  - `server-regression` (`UvExportTests`' manner): the exported file read through `IModelGeometryLoader.Load`, each
    copy's textures byte-equal to the bake's PNGs, no loader warning.
- Every existing export hash and frame of `scenario-export`, `scenario-materials`, `scenario-planks` and
  `scenario-parts` is unchanged.

## Notes

- **Decided at planning (2026-11-20, by the person):** a bake per mesh, and copies named `<material>@<mesh>` only when
  more than one mesh uses a mesh-reading material (T-160 keeps `@` out of new material names). Graphs are baked to PNG
  on export and the game never evaluates one (D-7).
- **The orchestrator's call, a stop if it proves wrong:** a warning line, not a refusal, when copies are written.
- Sonnet, with Opus if the part-tree and skinned paths prove tangled. Runs beside T-165 and T-166: it owns the export
  files and touches neither `RenderTool.cs` nor `MaterialTools.cs`. Errors reach the agent through T-163's
  `GraphSlotProblem`; its wording is T-166's.
- **Write down for T-167 and T-168** (As built): the result's shape with `baked` and the copies warning, and the measured
  export time of the barrel at 512 and 1024.
- **Stop and ask:** an existing export's bytes change; `render_glb` and `render` disagree past D-6's rule and the cause
  is not found (M-24's was a loader option: check `GenerateMipmaps` first).

### As built

One agent, Sonnet 5.5 (`claude-sonnet-5-5`).

**What was built.** `ExportTool` (now `internal`, taking `GraphBakes`) calls `GraphBakes.Resolve` for any model with a graph slot (not in clips mode, which writes no material), then hands the resolved model to the existing paths. `Resolve` already gives each baked slot as a `MaterialTexture`, so `GlbExport`, `SkinnedExport` and `GlbParts` needed no change: images are shared by hash through `ImageBuilder.Of` as for a PNG. No change to `GraphBakes`' API.
- Naming: `Resolve` makes one `<material>@<mesh>` copy per mesh. `ExportTool.Copies` turns a material that exactly one written mesh wears back to its plain name and keeps the copies (with one warning line naming them, in the result's `warning(s)`) when several meshes wear it. A material with no mesh-reading slot is already one material under its own name. Instances are parts without geometry, so they share their source mesh's copy. A piece export applies the rule after selecting the meshes it writes.
- The result gains render's `baked: [...]` and the 5 s warning lines (`GraphBakes.Text`), empty after a render or an earlier export of the same model. The UV warning covers graph materials (they are PNG textures by then).
- The interim refusal is gone: `ExportTool`'s check, `MaterialJson.GraphsNotDrawn`, its test, define_material's sentence (now "export bakes it and writes the baked PNGs") and the README's.
- `PieceExport.Select` dropped the model's materials, so a piece of a mesh with any material could not be built; it now carries `model.Materials` (a change outside the listed files, needed for a piece with a graph; no existing file's bytes depend on it).
- Errors from a bake reach the agent as `GraphSlotProblemException.Message` unprefixed, as render returns them (wording is T-166's).

**Tests.** `server-unit` `GraphExportTests`: plain name for one mesh, one material for a mesh-independent graph on two meshes, `@` copies and the warning for two meshes, shared images, no `baked` on a second export, equal bytes on two runs. `server-regression`: `GraphExportLoadTests` (two copies loaded through `IModelGeometryLoader.Load`, byte-equal to the bake's PNGs, no loader warning; the two meshes' PNGs are not asserted to differ), `GraphMaterialRenderTests.The_exported_barrel_is_drawn_by_render_glb_as_render_draws_it` (front and three-quarter at 512 under D-6's rule through `Passes`, which is the `RenderGlbTargetsTests.Measure` rule the repo has; control exported through graph copies with `wear_amount`/`grime_amount` at 0 fails it) and `The_barrel_export_bakes_and_says_how_long_it_took` (512 and 1024, writes the time to the test log).

**tools/list digest** retaken to `9DFF83594F018F57FD3CF237B029D4F37667A09938EF02A53E2F6CE1814435D6` (was `D5334566...`); only export's description and define_material's sentence changed. `PartToolsListTests` is in server-unit, which verify runs. `export`'s description is about 13,480 characters before and 14,015 after: over Claude Code's 4,096 cut before and after (it was already over; render, save and open are too).

**Checked.** verify PASS (server-unit, server-regression, scenario-export, scenario-materials, scenario-planks, scenario-parts); the four scenario suites' hashes and frames are unchanged (they passed their baselines).

**Not checked.** Export time of the barrel at 512 and 1024 (the test logs it; I did not get the runner's log, so T-167 and T-168 should read it from a `server-regression` job log); a part tree, skinned file or piece with a graph material (only the shared static path is tested; those paths take the same resolved model); a kept level; cancellation (export bakes with no cancellation token); the three-quarter frames and control by eye.

**For T-167 and T-168.** The result of `export` now ends `...; N warning(s): ...` as before, then `\nbaked: [{material, mesh, slot, size, ms}]` (and `\nwarning: material 'x' took S s to bake ...` over 5 s) when anything was baked; the copies line is `material 'm' reads the mesh and is used by several meshes, so it is written once per mesh as 'm@a', 'm@b'`.

**README.md:** the interim sentence on graph slots now says export writes them (baked PNGs, one copy per mesh); nothing else.
