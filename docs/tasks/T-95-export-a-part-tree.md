---
description: >-
  export writes the part tree as glTF nodes, merging static parts into their nearest kept ancestor and sharing one mesh between instances, and a skinned model bakes its parts
---
# T-95 — Export a part tree

**Status:** In Progress

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

### As built

Opus 5.5, 2026-11-18 (two agents, both Opus 5.5). For T-96 and T-97.

- **The person's rulings (2026-11-18, through Waystone, `G:\My Drivertifacts\carve\M-9\T-95ound-1\`):** (1a) a moving part whose folded transform shears is refused, writing nothing, naming the part and the static ancestor; node order among siblings is pre-order, as `Model.Parts`; game limits count the whole file as drawn (Combined's triangles, distinct materials), both as recommended; (4a) a moving instance with static descendants with geometry gets its own mesh (source geometry plus theirs), the other instances still share; (5b) morph targets with moving parts are written on each glTF mesh that holds them, and export warns that a split load refuses them.
- **Node layout as landed.** `GlbParts.Plan(model, name)` gives a `Layout`: the root node named after the file, at the identity, whose mesh (named after the file) is every static part whose nearest moving ancestor is none, joined by `Model.Join` at its transform to the root (static instances copied in; null when nothing draws there). Each moving part is a node named after it, under its nearest moving ancestor or the root, siblings in pre-order, with a TRS: its own composed with each static ancestor's up to that node (uniform scales and rotations compose; a non-uniform scale under a rotation is kept only when it decomposes back exactly, a quarter or half turn about one of the scale's axes, else refused). A moving part with no static descendants with geometry uses the one glTF mesh of its geometry alone, named after its source and shared by every node showing it (the rover's wheels: one mesh `Wheel_FL`, four nodes). A moving part with such descendants gets a mesh named after the part. A moving empty is a node with no mesh. Materials are shared across meshes by primitive key. A model with nothing marked `moves` never takes this path: `GlbExport.Write` is unchanged in output.
- **Result line:** `, <n> nodes: '<file>' and the moving part(s) '<a>', ...` after the bounds; the rover gives `6 nodes: 'rover' and the moving part(s) 'Mast', 'Wheel_FL', 'Wheel_FR', 'Wheel_RL', 'Wheel_RR'`.
- **Skinned export:** `SkinnedExport.Baked` places each part's geometry at its world transform (weights kept) and copies instances, in pre-order; a model whose drawn parts are its meshes at the identity, in mesh order, is returned as is (the gardener's bytes are today's). `MovesWarning` names the moving parts. A piece (`mode: piece`) is `PieceExport.Select`, which drops the parts, so it is the part's own geometry after the bake.
- **Split-load facts measured (engine 0.119.0, CPU, `ModelParseOptions(SplitParts: true)`), for T-97:** the rover reads back as parts `rover`, `Mast`, `Wheel_FL`..`Wheel_RR`, all with parent index 0, no warnings; each part's translation and rotation equal `Model.WorldTransform`'s decomposition (Wheel_FL at (0.7, 0.4, 0.72), Mast at (-0.6, 0.8, 0)); 3 meshes (root 40 triangles, Mast 40, the shared wheel 124), the four wheels on one mesh index; the merged load's bounds equal `Combined`'s, 576 triangles. A split load keeps only mesh nodes, animated nodes and their ancestors, so a moving empty with no moving descendant and no animation is dropped by it (the file still has the node). A split load throws `NotSupportedException` on a file with morph targets; the merged load reads them.
- **Rebase onto the new milestone tip (M-27 under it):** replayed with `--onto`; the test project's link list kept T-93's `RoverSpec.cs` and T-95's once (the auto-merge had left it twice).
- **tools/list:** `PartToolsListTests`' digest taken again (`DCE3D7BA...`): on T-95 with `ExportTool.cs` as at 431c75c the digest is the one before (`EBD17198...`), and export's description is the only field of tools/list that changes.
- **Byte identity:** the export, character, soft, gatehouse, UVs and materials suites' `.glb` files (11) equal by SHA-256 between a run of 431c75c (the milestone tip without T-95, through the queue's `--commit`) and T-95's verify run; and `A_two_mesh_static_model_writes_todays_bytes` pins a two-mesh static model to the bytes taken on the code before T-95.
- **Tests (`unit`):** `GlbPartsTests` and `ExportPartsToolTests` in `Carve.Server.Tests` (links `GlbParts.cs`, `RoverSpec.Model.cs`).
- **Break table:** `G:\My Drivertifacts\carve\M-9\T-95reak-table.tsv`, 26 breaks, each made in a temporary worktree, tested and reverted. Every new check fails under at least one. Three breaks fail nothing and are equivalent: materials not shared by key (SharpGLTF writes content-equal materials once), and two weakenings of the shear test (`Matrix4x4.Decompose` already fails on the tested shear; B24, dropping the check, fails both refusal tests). Two checks were added on the way: a smooth part's corner normals by the inverse transpose (a flat face's normal is recomputed from its corners, so the flat check could not see the normals' transform), and a moving part under a turned, uniformly scaled static parent (no rover ancestor is turned); plus a tool test for the morph-target warning, and the piece test now checks the piece after the bake.
- **Not checked:** no real MCP client run and no game load on a device; the split load is the engine's CPU parser.
