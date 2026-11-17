---
description: >-
  export writes the part tree as glTF nodes, merging static parts into their nearest kept ancestor and sharing one mesh between instances, and a skinned model bakes its parts
---
# T-95 — Export a part tree

**Status:** Open

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-91

**Verify:** core-unit, server-unit, scenario-export, scenario-character, scenario-soft, scenario-gatehouse, scenario-uvs, scenario-materials

**Perf:** skip: Carve declares no heavy suites

## Goal

The `.glb` keeps a node only where a part moves. Everything static merges, and moving instances share one mesh, as the person ruled at M-9's planning (2026-11-17). A model with nothing marked `moves` exports exactly as today.

## Scope

- **Static export:** the root node is named after the file, as today, with the identity transform. It holds every static part's geometry, merged, with each part's transform relative to the root folded into its vertices and normals. Static instances are copied in.
- **A part marked `moves`** is a child node named after the part, under its nearest moving ancestor or else the root. Its transform is its own composed with every static ancestor's up to that node. Its mesh holds its own geometry plus its static descendants', folded in the same way.
- **A moving instance** is a node that references the glTF mesh of its source's own geometry, so all of them share one mesh. If the source is static, one glTF mesh is still written for the instances to share. An empty that moves is a node without a mesh.
- **Skinned export (ruled):** each part's geometry is placed at its world transform and instances are copied, then written exactly as today. `moves` has no effect, and export says so in one warning naming the parts. A skinned model with no part transforms is byte-identical (the gardener).
- **Piece export** (M-19's `mode: piece`) on a part: its own geometry, as `save mesh=` does (T-92's ruling).
- **Result line and description:** the line says how many nodes were written and names the moving parts. The description says how the tree becomes nodes, with a worked example on the rover: root `rover` (Chassis and Cab), with children `Mast` (and Lamp) and `Wheel_FL` to `Wheel_RR` sharing one mesh.
- **Content: the rover's split load (`unit`).** The rover from `RoverSpec` is written through `GlbExport` and read with `IModelGeometryLoader` using `ModelParseOptions(SplitParts: true)` (CPU, no device). Parts in pre-order are `rover`, `Mast`, `Wheel_FL`, `Wheel_FR`, `Wheel_RL`, `Wheel_RR`, with parents as built. Each part's translation and rotation equal the composed ones from `RoverGeometry` (T-91), so `Wheel_FL` sits at (0.7, 0.4, 0.72) and `Mast` at (−0.6, 0.8, 0). The four wheels share one mesh. Triangle counts per part equal T-91's, and the merged mesh's bounds equal `Combined`'s. There are no parse warnings.
- **Tests (`unit`):** the above; byte identity of every existing export (the export, character, soft, gatehouse, UVs and materials suites' `.glb` files by hash, plus a `unit` test pinning a two-mesh static model to today's bytes); a static non-uniform scale folded into vertices with its normals transformed by the inverse transpose; an instance under a static parent; a moving empty. Each is shown failing on a broken product.

## Notes

- **Decides the export shape** M-12 (LOD nodes) and M-13 (sockets as kept nodes) build on: Opus.
- **Engine facts checked at planning** (0.119.0 docs): a split load keeps every mesh node, every animated node and their ancestors, and two parts using one glTF mesh share one `Mesh`. It refuses a skinned file, morph targets, and a node whose transform mirrors. Scales are positive (T-91), so nothing mirrors. Read the version-matched docs, not the engine source.
- **File ownership in its wave:** `ExportTool.cs`, `GlbExport.cs`, `SkinnedExport.cs`, and a new `GlbParts.cs`. M-19's `PieceExport.cs` and `GlbScene.cs` will be on `main` by then (M-9 starts after M-19 lands), so build on them.
- **Open rules: stop and ask.** If folding a static ancestor's transform into a kept node gives a matrix that is not a TRS (shear from a non-uniform scale above a rotated part): refuse it, warn, or bake it. Do not choose. Also: node order among siblings (recommendation: pre-order, as `Model.Parts`); any existing export whose bytes change; game-limit counting with several nodes (recommendation: as today, over the whole file).
- **Write down for dependents (As built):** the node layout as landed and the split-load facts measured, for T-97.
- **Carried:** each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-95` line given; report a `verify` that waits more than a few minutes.
