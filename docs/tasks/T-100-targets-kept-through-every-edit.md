---
description: >-
  move, extrude, delete, merge_faces, bridge, plane_cut, loop_cut and knife keep a mesh's targets, new vertices taking their deltas from the vertices they come from
---
# T-100 — Targets kept through every edit

**Status:** Done

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-99

**Verify:** core-unit

## Goal

No edit that keeps vertex identity loses a target. An agent can give the cage its dials first and go on
modelling.

## Scope

All in `src/Carve.Core`. The rules the person agreed (2026-11-16):

| Edit | Targets |
|---|---|
| `move` | deltas unchanged: the target moves with the base |
| `extrude` (and its inset) | each new vertex copies the delta of the vertex it was made from |
| `delete` | a deleted vertex's delta goes with it; the rest are kept |
| `merge_faces`, `bridge` | kept; a vertex these remove loses its delta |
| `plane_cut`, `loop_cut`, `knife` | each new vertex blends deltas by the same `Stencil` (first written `Subdivision.Stencil`; the type is now top-level) that blends its weights (the `Targets` layer's `LayerRules.BlendDeltas`) |
| style-only tools (colour, groups, material, shading, hard edges, UVs, weights) | untouched |

- `EditResult` carries the targets through like `Weights`. Find every edit through `MeshEdits`, `FaceSplit`,
  `FaceMerge`, `Bridge`, `PlaneCut`, `LoopCut` and `Knife`, and leave none out: list them in As built.
- **Tests** (unit), one per edit: a target defined, the edit made, and the morphed mesh at weight 1 compared with
  the same edit applied to the morphed mesh. Where that is exact (move, delete, extrude of a flat face, cuts along
  an edge), compare within 1e-6 of the diagonal. Where it is not (a knife point inside a face), assert the stencil
  rule directly. Show each failing on a broken product, such as a new vertex given a zero delta, and record the
  table.

## Notes

- Runs beside T-101 and T-102, which do not touch these files.
- **Goes back to the person, stop and ask:** an edit that cannot keep targets by the rules above. The plan says
  none exists. If one does, it must be refused before it runs with a text naming the targets, not silently
  dropped.
- Mechanical once T-99 is in: Sonnet.

### As built

> 2026-11-18: the rebase onto T-71 moved the carriage into the attribute-layer registry. `EditResult.CarriedRest`,
> `FaceSplit.CarryRest` and `Stencil.Apply(Func<int, Vector3>)` are gone: `StandardLayers.Targets` (in
> `LayerRegistry.Standard`) does the carrying, and `Stencil.Apply(IReadOnlyList<Vector3>)` works the stencil. Each
> name below that no longer exists says what replaced it (corrected for RV-64 by Opus 5.5). The tests are unchanged but
> for one added for RV-64, and the broken-product table is redone against `StandardLayers.Targets`.

Built by Claude Sonnet 5.5, as the Notes ask.

- **Every edit keeps its targets; none had to be refused.** The edits, all found through `EditResult` (`MeshEdits`) or
  `FaceSplit`:
  - `EditResult.CarriedRest` (now `LayerRegistry.Standard.Carry`, in `EditResult.From`) carries `source.Targets` onto the edited mesh (as `Weights`), then drops the deltas of
    tombstoned vertices: this covers `move`, `delete`, `merge_faces` (`FaceMerge`), `bridge` (`Bridge`, no vertex made
    or removed) and the deletion half of `plane_cut` (`PlaneCut`).
  - `EditResult.From(..., extruded, ...)` gives each extrude ring vertex the delta of the corner it stands over, in every
    target (first written as `WithExtrudedDeltas`, now `LayerRules.BlendDeltas`' `Offset` branch; hole corners too).
    Inset needs nothing more.
  - `FaceSplit.CarryRest` (now the layer's `LayerRules.BlendDeltas`) blends each new vertex's delta by the `Stencil`
    (first written `Subdivision.Stencil`) that blends its weights (`Stencil.Apply` over the deltas as a list, not the
    removed `Apply(Func<int, Vector3>)`; summed in double, rounded once). `FaceSplit`'s stencils now reference source
    vertices only (`Stencil.Lerp` on an edge, `Barycentric` over the original face for a point inside it), so the
    resolving of a stencil on an earlier new vertex down to source vertices, which `CarryRest` did, is gone with it.
    `FaceSplit.Split` is the one path for `plane_cut`, `loop_cut` and `knife`.
  - `MeshEdits.Subdivide` was T-99's. Style-only tools do not touch geometry.
- **Tests**, `tests/Carve.Core.Tests/TargetsKeptThroughEditsTests.cs` (12, unit): the morphed edited mesh against the
  edit on the morphed mesh within 1e-6 of the diagonal, for move, extrude (flat face, inset 0 and 0.1, plus a holed
  face's deltas), delete, merge_faces (the removed centre vertex loses its delta), bridge, plane_cut (both sides, and
  keeping one side), loop_cut, knife edge to edge, and a knife point inside a face asserted against the stencil rule.
  The cut targets are affine in position and leave the plane's coordinate alone, so even the interior point is exact.
  Two targets, weights 1 and 0.5, so each target is worked by itself. An affine target cannot tell one blend that
  reproduces the point from another, so RV-64 added a twelfth: a target that moves only two corners of the top face
  (not affine there), whose interior knife point must take the barycentric blend of the triangle it lies in.
- **Broken products**, redone for RV-64 (Opus 5.5) against the shipped `StandardLayers.Targets` and
  `LayerRules.BlendDeltas` / `ReblendDeltas`: each mutation made in a scratch copy of the tree at 2026-11-18's HEAD,
  `Carve.Core.Tests` run whole (1131 tests), and restored. The first were measured against the removed per-edit code.

  | Broken | Failed in core-unit | Of them, in `TargetsKeptThroughEditsTests` |
  |---|---|---|
  | a blended vertex (split or subdivide) given a zero delta | 16 | 6: both plane_cut tests, loop_cut, the three knife tests |
  | an extrude ring given a zero delta (the `Offset` branch) | 2 | 2: both extrude tests |
  | the `Targets` layer left out of `LayerRegistry.Standard` | 25 | 12: all |
  | a deleted vertex keeping its delta (the layer's write keeps the source's) | 3 | 3: delete, merge, plane_cut with a side kept |
  | a re-made vertex (subdivide) keeping its own delta (`ReblendDeltas`) | 5 | 0 (T-99's subdivide tests catch it) |
  | `FaceSplit.Barycentric` taking the worst triangle, not the best | 1 | 1: the non-affine knife point (no test failed before RV-64) |
