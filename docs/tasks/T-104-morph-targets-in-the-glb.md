---
description: >-
  export writes each target's position and normal deltas and its name as glTF morph targets, read back through the engine's loader
---
# T-104 — Morph targets in the glb

**Status:** Done

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-102

**Verify:** server-unit, server-regression

## Goal

`export` writes targets a game can drive: `POSITION` and `NORMAL` deltas and the target names, read back by
Empyrean's own loader with the values Carve meant.

## Scope

- Every export mode that writes a mesh (model, skinned, piece from M-19's T-50) writes `primitive.targets`. The
  target set is the **union of names across the model's meshes**, in first-definition order. Carve writes every
  mesh as a primitive of one glTF mesh (D-6, engine need 4), so a primitive whose Carve mesh lacks a target gets
  zero deltas for it. Empyrean's limit of one morphing glTF mesh per file is never met. Say so in D-6.
- **Dense accessors** (the person, 2026-11-16). `POSITION` deltas are per exported vertex: its Carve vertex's
  delta. `NORMAL` deltas are **computed**: the corner normal from `Shading` of the mesh at weight 1, minus the
  corner normal at rest, per exported vertex. A flat mesh uses face normals, and so a flat face's corners differ.
  No `TANGENT`.
- `mesh.weights` all 0. The names go where Empyrean's `ParsedMorphTargets.TargetNames` reads them: probe that
  first, with `mesh.extras.targetNames` as the glTF convention. Record the answer.
- Clips-only export writes no targets. A model without targets exports byte-identical to today.
- **Tests** (unit where the loader is CPU-only, as `CharacterLoad.Tests` does):
  - read through `IModelGeometryLoader.Load`: `TargetNames`, `DefaultWeights`, and every `PositionDeltas` and
    `NormalDeltas` entry against Carve's own, flat and smooth, skinned and not;
  - two Carve meshes with one shared name;
  - `ParsedModel.Warnings` empty;
  - byte-identity of an export without targets.

  Show the delta test failing on swapped targets and on normals left out, and record it.

## Notes

- **Decides how targets reach a game: stronger model.**
- Owns `GlbExport.cs`, `SkinnedExport.cs` and the export side of M-19's `GlbScene.cs` and `PieceExport.cs` in its
  wave.
- **Goes back to the person, stop and ask:** sparse accessors; writing `TANGENT`; any engine limit met (record it
  as an engine need in D-1 only after asking); any change to a file without targets.

### As built

Built by Claude Opus 5.5 (the stronger model the Notes ask for).

- **What the `.glb` holds.** `src/Carve.Server/GlbTargets.cs`, called by `GlbExport.Write` (mode model, on
  `Model.Combined`) and `SkinnedExport.Write` (skinned, and piece through it). When the model has targets, the one glTF
  mesh carries one target per name of `Model.TargetNames` (the union, first-definition order) on **every** primitive:
  dense `POSITION` and `NORMAL` accessors (float3, one entry per written vertex), no `TANGENT`, no sparse accessors.
  `mesh.weights` is all 0, and the names are in `mesh.extras.targetNames`. A primitive whose Carve mesh lacks a name
  has zero deltas for it. Position delta: the Carve vertex's `DeltaOf`. Normal delta: `Shading.Corners` of
  `mesh.Morphed({name: 1})` at the corner, less the normal written at rest (the face's on a flat mesh).
- **Names probe.** Empyrean 0.129.0's `ParsedMorphTargets.TargetNames` reads `mesh.extras.targetNames`: the names came
  back as written on the first run, and `DefaultWeights` came back as `mesh.weights` (all 0).
- **Vertices split where deltas differ.** SharpGLTF's builder merges equal vertices. Two coplanar flat faces share a
  corner normal at rest but not once a target bends them, and coincident vertices can come apart. So each written vertex
  carries a tag (`Tagged<T>`, never written) naming its set of deltas, and two corners merge only when their deltas
  agree too. A model with targets therefore writes more vertices than the same model without them where a target bends
  coplanar flat faces apart (the 3x3 flat grid test). This is how "per exported vertex" stays exact. It never touches a
  file without targets, but the person may want to see it. The person ruled on it after T-104 landed (recorded here for RV-64, Opus 5.5):
  kept, as built. Corners merge in the export only when their deltas agree too.
- **Unchanged without targets.** No target, no tag, no `Tagged<T>`: the code path is the old one. Four SHA-256 hashes
  (static and skinned, flat and smooth, a cube and a grid) were taken from the export with this change stashed and
  pinned in `A_model_without_targets_exports_the_bytes_it_did`. They match after the change.
- **Clips only** has no mesh, so it has no targets. Empyrean's loader refuses a file with no mesh ("glTF contains no
  meshes"), so that test reads the file through SharpGLTF.
- **Engine limit.** Targets on two or more glTF meshes would throw at parse, but every Carve mesh is a primitive of the
  one glTF mesh (engine need 4), so the limit is never met. D-6's *Morph targets* says so. No engine need was recorded.
- **Tests** (`tests/Carve.Server.Tests/MorphExportTests.cs`, unit, through `IModelGeometryLoader.Parse`): for every
  vertex the loader gives (a cube with Bulk and Lift beside a grid with Bulk and Bend, flat and smooth, static and
  skinned), every `PositionDeltas` and `NormalDeltas` entry equals Carve's exactly (`==`), and so does the rest normal.
  Also checked: `TargetNames` `[Bulk, Lift, Bend]` (Bulk shared by both meshes, one target), `DefaultWeights` all 0,
  `Warnings` empty, a piece's own targets (`[Bulk, Bend]`), the bent flat grid, clips-only, and byte identity.
- **Broken product shown.** With the targets written in reverse order (swapped), 7 of 12 tests failed, for example
  "Bulk position delta of vertex 0 (Carve 1): <0, 0, 0>, Carve's <0.25, 0, 0>". With `NORMAL` left out of the targets,
  6 failed, for example "Lift normal delta of vertex 16 (Carve 3, face 4): <0, 0, 0>, Carve's <0.0825, 0.3529, 0.1835>".
  After restoring, all 12 pass.
- **What T-108 reads.** `ParsedModel.MorphTargets` from the exported bytes. `TargetNames[i]` is Carve's name `i` in
  `Model.TargetNames` order, so a weight given by name maps to the index in that list. On a skinned file the delta arrays
  are index-parallel to `Skin.SkinnedMeshData.Vertices` (the skinned tests index them that way and pass), and on a
  static file to `MeshData.Vertices`. Morph is before skin, as `MorphWeights` does it.
- **What T-106 reads.** The names in `mesh.extras.targetNames` and the weights in `mesh.weights` of the one glTF mesh.
  Every export mode with a mesh writes them, and clips-only writes none.
- **Not checked:** nothing visible. No frame was rendered, since the weights through the engine are T-108's work.
