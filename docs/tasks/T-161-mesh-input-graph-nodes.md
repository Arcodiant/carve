---
description: >-
  Carve.Graphs, a new engine-free project: mesh_curvature, mesh_occlusion, mesh_mask and mesh_position node types fed from Carve.Core.Baking on a mesh's UV layout, neutral where there is no mesh
---
# T-161 — Mesh-input graph nodes

**Status:** Done

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, patina-unit, graphs-unit

## Goal

A texture graph can read the mesh it is put on. Four node types, in a new engine-free project that both servers
reference, give a graph the mesh's curvature, occlusion, a mask of a group or material, and position, computed by
`Carve.Core.Baking` (T-120) on the mesh's UV layout. With no mesh they give neutral values, so the same graph still
evaluates in patina.

## Scope

- **A new project, `src/Carve.Graphs`** (engine-free: it references `Carve.Core` and `Patina.Graphs` only; a guard test
  like `PatinaGraphsIsEngineFreeTests` holds it, with no `System.Random` and no clock). It joins `Carve.slnx`. Tests go
  in a new `tests/Carve.Graphs.Tests` (`unit`), suite `graphs-unit` in `tests.json` with `src/Carve.Graphs/**`,
  `src/Carve.Core/**`, `src/Patina.Graphs/**`, the test project and `samples/wrench.carve` as inputs.
- **The node API is M-28's** (T-133's As built: `InputRect`, `Evaluate(NodeEvaluation)` filling `e.Rect` tile by tile,
  one stateless shared instance per type, tiles in any order and possibly twice). Read it first.
- **The mesh source.** The nodes are bound to a source when the registry is made:
  `MeshNodes.Register(NodeRegistry registry, IMeshMaps? source)` (names are this task's to settle) returns
  `registry.With(...)` the four types. `IMeshMaps` answers "the map for these settings at this size" as a whole image;
  the node copies texels of `e.Rect` out of it. A source computes each distinct map once per size and keeps it for its
  own lifetime, so a tile asked twice is the same and occlusion is not cast per tile. With `source` null the nodes
  give the neutral value everywhere.
- **`MeshBakeSource`**: the real source, made from a `Model`, a mesh name and nothing else. It builds
  `BakeLayout.Create(mesh, size)` once per size and calls `Bake.Curvature`, `Bake.Occlusion` (a `RayCaster` over every
  mesh of the model at rest, as M-24 ruled), `Bake.Masks` and the layout's texel positions. Each map is padded 8 texels
  past island borders with `Bake.Pad`; a texel no island or padding covers reads the neutral value. A layout refusal
  (no UVs, overlapping UVs with the faces named, a bad size) is thrown as a typed exception carrying the Core's own
  message, which T-163 and T-166 turn into the tool's error; unmapped faces are reported as a warning the source
  exposes.
- **The four types**, each one file, output port `out`:

  | Type | Carries | Parameters | Neutral |
  |---|---|---|---|
  | `mesh_curvature` | grey: 0.5 flat, toward 1 at convex edges, toward 0 at concave ones (`Bake.Curvature`) | `width` in metres, above 0 to 1, default 0.01 | 0.5 |
  | `mesh_occlusion` | grey: 1 open, toward 0 occluded (`Bake.Occlusion`) | `rays` 1 to 1024, default 32; `reach` in metres, above 0 to 10, default 0.1 | 1 |
  | `mesh_mask` | grey: 1 on the faces of a group or material, 0 elsewhere (`Bake.Masks`) | exactly one of `group`, `material`, a name as carve has it | 1 |
  | `mesh_position` | colour: r, g, b are x, y, z, each 0 to 1 across the mesh's own bounding box at rest | none | #808080 |

  - Widths and reaches are metres on the mesh, not fractions of the image, so the picture is the same at every size;
    the type's summary says so, since `NodeType`'s rule is "relative to the image".
  - A group or material name is free text. If no `ParamKind` takes one, add a text kind to `Patina.Graphs`
    (`NodeType.cs`, `TexGraphFile.cs`) with its round-trip test; that is the only change this task makes to
    `Patina.Graphs`.
  - `mesh_mask` naming a group or material the mesh does not have is a typed exception listing the mesh's groups and
    materials.
- **Tests** (`graphs-unit`):
  - on `samples/wrench.carve` at 256 and 1024: each node's output, packed to bytes, equals the `Bake` call's image packed
    the same way, texel for texel, at tile sizes 16, 64 and 256 and under a zero cache cap;
  - the numbers M-24 approved hold through the node: a texel on a convex steel edge at least 218, a flat handle texel
    exactly 128 (`mesh_curvature`, width 0.003, 1024); the drive crevice at least 50 below every open texel
    (`mesh_occlusion`, 32 rays, reach 0.035);
  - the raised-hoop barrel built in the test (`TestMeshes.SeamedBarrel`, faces 0 to 7 and 24 to 31 extruded 0.015,
    unwrapped): hoop rims read 0.9 or more in curvature at width 0.02, the staves' occlusion at reach 0.15 has a minimum
    of 0.55 or less and a mean of 0.95 or more, the plain barrel's occlusion is exactly 1 (planning's probe, below);
  - neutral values with no source; determinism (two runs, equal bytes); every refusal; a table of broken copies (the
    mask inverted, curvature read as occlusion, position's y flipped, the padding left out) and what caught each.
- CLAUDE.md: a Layout row for `src/Carve.Graphs` and `tests/Carve.Graphs.Tests`, and an architecture rule line
  ("`Carve.Graphs` is engine-free too: the mesh-input nodes and, from T-163, the bake of a graph slot").

## Notes

- **Decided at planning (2026-11-20, by the person):** mesh inputs are node types in a new engine-free project that both
  servers reference, neutral where there is no mesh (patina's side is T-162). M-24's bakes are read through
  `Carve.Core.Baking`'s API, never through its PNGs (D-7, answered at M-24's planning).
- **The orchestrator's calls, each a stop if it proves wrong:** the project's name; the four type names and their
  parameters and defaults; position normalised per axis over the mesh's own bounding box; inputs padded 8 texels;
  occlusion against the whole model at rest with no `occluders` parameter.
- **Planning's probe** (a throwaway console in the temp folder, Debug, 2026-11-20), on `samples/barrel.carve` and on
  the same with both hoop bands extruded 0.015 and unwrapped (3 islands, fill 0.50, no overlap, 98 faces):

  | Mesh, size | Curvature (width 0.02) | Occlusion (32 rays, reach 0.15) | Time |
  |---|---|---|---|
  | plain, 1024 | staves mean 0.519, max 0.807; hoop bands max 1.000 | exactly 1.000 on every texel | curvature 0.6 s, occlusion 3.5 s |
  | raised hoops, 1024 | hoop tops mean 0.628, 95th percentile 0.996 | staves min 0.500, 5th percentile 0.911, mean 0.986; hoop walls 5th percentile 0.283 | curvature 0.6 s, occlusion 5.6 s |

  The test thresholds above are set from these with room; if a measured value misses one, report both numbers and the
  statistic (`a - b`, each named) and ask.
- Design-heavy, and it decides the API T-162, T-163 and T-165 build on: Opus; plan for a hand-off.
- **Write down for them** (As built): the registration call, `IMeshMaps` and `MeshBakeSource` with a worked use, the
  typed exceptions, the padding and neutral rules, and the time each map took on the wrench at 512 and 1024.
- Not covered, and to be said in As built: occlusion between parts moved by a part tree (occluders use each mesh's
  stored positions, as M-24 left it), and a bake at 4096.
- This task creates suite `graphs-unit`; add it to this file's `**Verify:**` line in the commit that adds it to
  `tests.json` (cairn's lint refuses a suite id that does not exist yet, so planning could not name it).
- Runs beside T-160 and shares no file with it but `Carve.slnx`, `tests.json` and CLAUDE.md, where conflicts only add.
- **Stop and ask:** a change to `Carve.Core.Baking`'s behaviour or to any M-24 number; a second change to
  `Patina.Graphs`; a threshold that would have to loosen.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), in two agents (the first handed off at its 60-call checkpoint).

**`src/Carve.Graphs`** (new, engine-free: `Carve.Core` and `Patina.Graphs` only, held by `CarveGraphsIsEngineFreeTests`;
in `Carve.slnx`):
- **Registration:** `MeshNodes.Register(NodeRegistry registry, IMeshMaps? source)` returns `registry.With(...)` the four
  types bound to `source`; `MeshNodes.TypeNames` lists them. Each type is one file with `TypeName` and `Neutral`:
  `MeshCurvatureNode` (`mesh_curvature`, `width` default 0.01, neutral 0.5), `MeshOcclusionNode` (`mesh_occlusion`,
  `rays` default 32, `reach` default 0.1, neutral 1), `MeshMaskNode` (`mesh_mask`, `group` or `material`, neutral 1),
  `MeshPositionNode` (`mesh_position`, colour, neutral #808080). Output port `out`. Each node copies `e.Rect` out of the
  source's whole image; with `source` null it fills the neutral value.
- **`IMeshMaps`:** `Curvature(size, width)`, `Occlusion(size, rays, reach)`, `Mask(size, MaskSource, name)`,
  `Position(size)`, each a whole `size` x `size` `BakeImage`, padded and neutral outside the padding.
- **`MeshBakeSource(Model model, string mesh)`:** the real source. `BakeLayout.Create` once per size; each map once per
  size and settings, kept for the source's lifetime, under a lock (safe to share). Occlusion casts against every mesh of
  the model at its stored positions. Position is each axis 0 to 1 over the bounding box of the live faces' corners.
  Also `MeshName`, `Groups`, `Materials` (those the live faces use), `Unmapped` (live faces with no UVs) and `Warnings`
  (one line naming them), and `Padding = 8`.
- **Padding and neutral rule:** the bake's uncovered texels are set to the node's neutral value, then `Bake.Pad(image,
  layout, 8)`: the 8 texels past each island take the island's values, beyond that reads neutral.
- **Typed exceptions** (all `MeshMapsException`, thrown out of the evaluation): `MeshLayoutException` (Message is the
  Core's own: no UVs, overlapping UVs naming the faces, a bad size; plus `Mesh`, `Size`); `MeshMaskException` (a group
  or material the mesh lacks; `Mesh`, `Kind`, `Name`, `Groups`, `Materials`, all listed in the message);
  `MeshParameterException` (`width` or `reach` 0, a mask naming both or neither: the file's float ranges start at 0, so
  "above 0" is refused at evaluation, with or without a source). An unknown mesh name is an `ArgumentException` from the
  constructor listing the model's meshes. Ranges the file holds (`width` over 1, `rays` 0 or 1025, `reach` over 10, an
  empty name) are ordinary graph faults.
- **Worked use** (what T-162 and T-163 call):
  `var registry = MeshNodes.Register(NodeRegistry.BuiltIn, new MeshBakeSource(model, "wrench"));` then
  `TexGraphFile.Read(text, registry)` and `new GraphEvaluator(graph, registry)` as for any graph; patina passes `null`.
- **Times** through the source on the wrench, Debug, at the defaults (layout + position, curvature 0.01, occlusion 32
  rays 0.1, mask `steel`): 512: 178, 209, 2190, 201 ms; 1024: 690, 760, 7351, 766 ms (one run; an earlier run on a
  busier machine gave 3806, 1719, 13744, 960 ms at 1024).

**`Patina.Graphs`**: its one change, `ParamKind.Text` and `ParamSpec.Text(name)` (optional free text, not empty), with its
line in `GraphParameters`. `TexGraphFile` needed no change. Round trip in `tests/Patina.Graphs.Tests/TextParamTests.cs`
(a space, a quote and a non-ASCII letter read, written, read again; empty text and a number refused).

**Tests** (`tests/Carve.Graphs.Tests`, suite `graphs-unit`): each node equal byte for byte to its `Bake` call at 256 and
1024 at tiles 16, 64, 256 and cap 0 (occlusion 32 rays, reach 0.035); the steel mask's faces; M-24's numbers (every convex
jaw edge at least 218 and the flat handle exactly 128 at width 0.003, 1024; the drive crevice at least 50 below the open
grip, 32 rays, reach 0.035, at 512); the raised-hoop barrel at 512 (the hoop tops' 95th percentile, planning's statistic,
0.9 or more; staves' minimum 0.55 or less and mean 0.95 or more); the plain barrel exactly 1; neutral; determinism; every
refusal. Broken copies (`BrokenCopiesTests.cs`, a source wrapping `MeshBakeSource`):

| Broken copy | Caught by |
|---|---|
| the mask inverted | the comparison with `Bake.Masks`; `MaskCheck` |
| curvature read as occlusion | the comparison with `Bake.Occlusion`; `AllOpen` on the plain barrel |
| position's y flipped | the comparison with the layout's texel positions |
| the padding left out | the comparison with the padded `Bake.Curvature` |

**Not checked:** occlusion between parts moved by a part tree (occluders use each mesh's stored positions, as M-24 left
it); a bake at 4096; the raised-hoop and crevice numbers at 1024 (run at 512).

**README:** nothing needed; no tool reaches the nodes until T-162 and T-163.
