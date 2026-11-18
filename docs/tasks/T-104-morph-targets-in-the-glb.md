---
description: >-
  export writes each target's position and normal deltas and its name as glTF morph targets, read back through the engine's loader
---
# T-104 — Morph targets in the glb

**Status:** Open

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
