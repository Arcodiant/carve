---
description: >-
  export embeds each graph slot's evaluated PNG in the .glb, with one <material>@<mesh> copy per mesh when a mesh-reading material is used by several, and render_glb agrees with render
---
# T-164 — Graph materials in export

**Status:** Open

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
