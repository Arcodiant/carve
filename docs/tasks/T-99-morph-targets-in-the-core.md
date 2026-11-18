---
description: >-
  A mesh holds named targets as sparse position deltas, defined from a full or sparse position set or a mesh of the same topology, evaluated at any weights and kept exactly through subdivide
---
# T-99 — Morph targets in the Core

**Status:** Done

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

## Goal

`PolygonMesh` carries named morph targets: a second set of positions on the same vertices, held as deltas. The
Core can make one from positions or from a mesh of the same topology, give the mesh's positions at any weights,
and keep targets exactly through `subdivide`. Every other M-21 task builds on this type.

## Scope

- **`MorphTargets`** (new file, `src/Carve.Core/MorphTargets.cs`), immutable, beside `VertexWeights`. It holds
  the targets in definition order. Each target is a name and a sparse map from vertex id to a `Vector3` delta. A
  vertex not listed has a zero delta, and a delta of exactly `(0,0,0)` is never stored. `PolygonMesh.Targets` is
  null when there are none, and `WithTargets(...)` follows the `WithWeights`/`WithUvs` pattern. Every existing
  `With*` and constructor path carries targets, as it carries `Uvs`.
- **Names**: non-empty, no colon (as with mesh names), unique in the mesh, and case-sensitive. The same name on
  two meshes of a model means one dial: it is one glTF target at export (T-104), and one weight in `render`.
- **Making a target** (static constructors returning a result and `MeshError`s, as `PolygonMesh.Create` does):
  - from **absolute positions, full**: one per vertex slot, with tombstones skipped. The delta is
    `given - base` in float.
  - from **absolute positions, sparse**: vertex id to position. An unknown or deleted id is refused, named
    `positions[<id>]`.
  - from **a second mesh**: the topology must match. That means the same vertex slot count, the same tombstones,
    and the same faces in the same order with identical loops and holes. A mismatch fails and names the
    **first** difference, for example `vertices: 88 here, 90 in the other mesh` or
    `faces[12][2]: vertex 40 here, 41 in the other mesh`.
  - A non-finite coordinate is refused.
- **Evaluation**: `Morphed(IReadOnlyDictionary<string, float> weights)` gives the mesh at
  `base + Σ wᵢ·deltaᵢ`, the same shape as `WithVertices` (unchecked, keeping faces, style and UVs). Targets are
  added in definition order so the sum is the same bits every time. Names the mesh does not have are ignored
  here. The tools refuse them (T-102, T-103).
- **Posing**: `Skinning` applies the morph **before** skinning (glTF's order, and Empyrean's). Add the
  overload, or the weights parameter, that T-103 calls.
- **Subdivide**: `Subdivision.Level` carries targets, and every new vertex's delta uses the **same stencil and
  order** as its position. Because Catmull-Clark (crease rules included) is linear in positions, the subdivided
  target equals the subdivided base plus the subdivided delta. Tombstones stay tombstones.
- **`Model.Combined`** offsets target vertex ids as it offsets weights.
- **D-6**: add a *Morph targets* section with the decisions in M-21's *Decided at planning*, worded as rules.
- **Tests** (`tests/Carve.Core.Tests`, unit):
  - each constructor, including every refusal and the first-difference text;
  - a zero delta not stored;
  - `Morphed` at 0, 0.5 and 1, and at two targets mixed;
  - morph then skin against skin alone on a bent chain;
  - **subdivide exactness**: the arm cage of M-21's sketch (11 rings of 8 at x = 0.00…0.60, radii as listed, 8-gon
    caps). Subdivide (base + Bulk delta) as its own mesh, and compare with subdivide(base) + subdivided delta,
    every vertex within 1e-6 of the bounding-box diagonal. Also on a mesh with hard edges and a crease (the M-18
    cylinder), and at levels 1 and 2;
  - the counts: cage 88 / 82, level 1 338 vertices / 336 faces;
  - a Combined model with targets on two meshes.

  Show at least the subdivide test failing on a broken product, such as a delta given the face-point stencil of
  the wrong face, and record it.

## Notes

- **Decides a format and an API that T-100 to T-107 build on. Design-heavy: plan for the stronger model.**
- Bound by M-21's decisions (the person, 2026-11-16): sparse deltas; normal deltas never stored; no corrective
  targets; no morph weights in clips.
- **Goes back to the person, stop and ask:** any change to D-1's id rule; a target name rule beyond the one above;
  the 1e-6 tolerance if it cannot be met; storing absolute positions instead of deltas.
- Write in As built: the API names T-100 to T-104 call, and whether delta-of-the-sum rounding ever showed.

### As built

> 2026-11-18: the rebase onto T-71 moved the carriage. `Subdivision.Level` no longer has a `Targets` field: the
> `StandardLayers.Targets` layer in `LayerRegistry.Standard` carries the deltas (`LayerRules.BlendDeltas` and
> `ReblendDeltas`), through `Subdivision.Once`'s provenance. The text below was written before.

Built by Claude Opus 5.5 (the stronger model the Notes ask for).

- **Type.** `src/Carve.Core/MorphTargets.cs`: `MorphTarget` (a name and sparse deltas, `SortedDictionary<int, Vector3>`;
  a zero delta is never stored), `MorphTargetResult` (`Target`, `Errors`, `Succeeded`) and `MorphTargets` (the mesh's
  set, never empty, in definition order). `PolygonMesh.Targets` is null for none; `WithTargets(...)` sets it, and
  `WithStyle`, `WithShade`, `WithWeights`, `WithUvs` and `WithVertices` all carry it.
- **API that T-100 to T-104 call.**
  - Making: `MorphTarget.FromPositions(mesh, name, IReadOnlyList<Vector3>)` (full; tombstone slots skipped),
    `MorphTarget.FromPositions(mesh, name, IEnumerable<KeyValuePair<int, Vector3>>)` (sparse, `positions[<id>]`),
    `MorphTarget.FromDeltas(mesh, name, sparse deltas)` (the file reader, T-101; `deltas[<id>]`),
    `MorphTarget.FromMesh(mesh, name, other)` (first difference by `MorphTarget.TopologyDifference(mesh, other)`),
    `MorphTarget.NameProblem(name)`. Every refusal is a `MeshError`, as `PolygonMesh.Create`'s.
  - Reading: `MorphTarget.Name`, `.Deltas` (ascending ids), `.Count`, `.DeltaOf(v)`; `MorphTargets.All`, `.Names`,
    `.Count`, `.Find(name)`, `.ListNames()`; `Model.TargetNames` (the union across meshes in first-definition order,
    for T-104's glTF target list and T-103's refusals).
  - Changing (T-100, T-102): `MorphTarget.Of(name, deltas)` (unchecked, for edits carrying a checked target),
    `MorphTarget.Without(vertices)`, `MorphTargets.Of(targets)` (null for none; throws on a bad or repeated name),
    `MorphTargets.With(target, out replaced)` (replaced in place or added last), `MorphTargets.With(targets?, target)`,
    `MorphTargets.Without(name)` (null when none left), `MorphTargets.WithoutVertices(ids)`,
    `MorphTargets.Select(t => deltas)` (an edit working each target's deltas by its own stencils).
  - Evaluating: `PolygonMesh.Morphed(IReadOnlyDictionary<string, float>)` (a copy to draw: no weights, no targets);
    `MorphTargets.Apply(positions, weights)`; `Skinning.Posed(model, pose, targets)` morphs every mesh with targets,
    even with no skeleton, then skins (`null` targets is the old `Posed(model, pose)`). T-103 calls this.
- **Sum.** Each vertex is summed in double from its base, targets in definition order, and rounded once; a vertex no
  given target moves keeps its base bits, and all weights 0 gives the base exactly.
- **Subdivide.** Each new vertex's delta is its position's own `Stencil` over the
  level's dense deltas (same terms, same order; now `LayerRules.BlendDeltas` on the `Targets` layer). `MeshEdits.Subdivide` drops deltas on tombstones. Exact within 1e-6 of
  the bounding-box diagonal on the arm cage (Bulk) and the hard-rimmed cylinder (both rims hard, one side edge hard, so
  creases and corners), at levels 1 and 2. The 1e-6 tolerance was met; nothing was loosened.
- **`Model.Combined`** offsets each mesh's target ids by its vertex offset and joins a name used on two meshes into one
  target, in first-definition order.
- **Delta-of-the-sum rounding did show.** A target from absolute positions stores `given - base` rounded to float. When
  the given coordinate is within a factor of 2 of the base (same sign) the subtraction is exact (Sterbenz) and weight 1
  gives the position back bit for bit; the arm's Lean (x0.8) and Bulk (x1.35) do. Further away (x3.7, x0.001) weight 1
  came back up to one unit in the last place (of the larger of base and given) off, for example 0.1036 given and
  0.103599995 back. Both are pinned by tests. Storing absolute positions would remove it, but that goes back to the
  person (Notes); I recommend deltas as decided, since glTF stores float deltas and the game rounds the same way.
- **Broken product shown.** With each face point's delta given the face-point stencil of the next face (the wrong
  face), the exactness test failed in all four cases (arm level 1: worst vertex 0.0140 apart against a tolerance of
  6.3e-7; arm level 2: 0.0227; cylinder level 1: 0.0558; cylinder level 2: 0.0871), and so did the tombstone test.
  Restored, all pass.
- **Not checked:** nothing visible (Core only, no frames). The edits other than `subdivide` do not carry targets yet:
  `PolygonMesh.Edit` and `Create` make a mesh without them, as they make one without UVs. That is T-100.
