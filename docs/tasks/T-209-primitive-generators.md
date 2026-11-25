---
description: >-
  primitive makes a box, cylinder, cone or UV sphere with segments and rings, seams marked and UVs unwrapped, under a name as define_mesh does, and answers define_mesh's report
---
# T-209 — Primitive generators

**Status:** Open

**Pri:** P1

**Milestone:** M-10

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit

## Goal

An agent starts a shape from a generator instead of typing every coordinate: `primitive` makes a box, cylinder, cone or
UV sphere at a segment count, with seams marked and UVs laid out, under a name, and answers exactly what `define_mesh`
answers.

## Scope

- **Core, `src/Carve.Core/Primitives.cs`** (new): one static method per kind, each returning the vertex list, the
  face loops and the seam edges, nothing else. Engine-free, no `System.Random`, no clock; trig in `double` from
  `k / segments` turns, rounded to `float` once.
  - **Axis +Y, centred on `centre`** (default the origin) for every kind.
  - **box** `size [x, y, z]` (default `[1, 1, 1]`): vertices and faces in the order of define_mesh's unit cube
    (`DefineMeshTool.CubeExample`), so `box` with size 1 and centre `[0.5, 0.5, 0.5]` equals the worked cube by
    content. Seams: every edge (six islands).
  - **cylinder** `radius` (0.5), `height` (1), `segments` (16, 3 to 256), `rings` (bands along the height, 1, 1 to
    256): ring i from the bottom (`y = -height/2`) up, vertex k of a ring at angle a = 2πk/segments,
    `(r cos a, y, -r sin a)`; faces: the sides ring by ring, `[n·i+k, n·i+(k+1)%n, n·(i+1)+(k+1)%n, n·(i+1)+k]`, then
    the bottom cap `[n-1 … 0]` and the top cap, **each cap one n-gon** (decided at planning). So segments 8 and rings 4
    has the topology of the Uvs scenario's `BarrelSpec` (a test holds that). Seams: the vertical edges at k = 0 and
    both cap rings (three islands, as the barrel).
  - **cone** `radius`, `height`, `segments`, `rings` (as the cylinder, rings narrowing linearly): the rings, then the
    apex as the last vertex; the top band is n triangles `[k, k+1, apex]`, then the base cap as one n-gon. Seams: the
    k = 0 line base to apex, and the base ring (two islands).
  - **sphere** `radius` (0.5), `segments` (16, 3 to 256), `rings` (latitude bands, 8, 2 to 256): the south pole
    (vertex 0), the `rings - 1` latitude rings from south to north, the north pole last; faces: the south fan of
    triangles, the quad bands, the north fan. Seams: the k = 0 meridian pole to pole (one island).
- **UVs:** the seams marked, then Core's `Unwrap` with the defaults the `unwrap` tool uses. A unit test holds the result
  equal by content to `define_mesh` + `mark_edges seam` + `unwrap` through the tools, for each kind.
- **Server, `src/Carve.Server/PrimitiveTool.cs`** (new), tool `primitive`: `kind` (`box | cylinder | cone | sphere`),
  `name`, the kind's own arguments, `centre`, `drop_targets`. It builds the lists in Core and hands them to
  define_mesh's own path, so naming, parts (`PartTexts.DefineMesh`: an empty part gets this geometry and keeps its
  placement; an instance is refused), the morph-target refusal, undo (one step) and the report are define_mesh's by
  construction. Then it adds the seams and UVs in the same undo step. Registered in `CarveServer.AddTools` after
  `define_mesh`.
- **Refusals**, every problem named by argument, the model unchanged: an unknown `kind` (listing the four), an argument
  the kind does not take (`size` on a sphere), a size, radius or height not > 0 or not finite, `segments` or `rings`
  out of range or not an integer, an argument `primitive` does not take.
- **The description**, at most 3,686 characters (M-29's `HelpToolTests` budget). It states the axis, the centre, the id
  order of each kind in one sentence, the seams, and the report. It has **one worked example per kind**, each an
  `Example:` line run as written by a test. If it does not fit, the id-order detail moves to a `help` entry, in the way
  T-207 set up.
- **Tests** (`tests/Carve.Core.Tests/PrimitivesTests.cs`, `tests/Carve.Server.Tests/PrimitiveToolTests.cs`): counts
  per kind at minimum and typical segments and rings, closed and no warnings from `MeshReport`, outward winding
  (signed volume > 0), positions at known angles, the cube and BarrelSpec equalities, a primitive filling an empty part
  and refused on an instance, every refusal, and the worked examples.
- **`README.md`**: `primitive` in the tool list, with one worked call run as written.

## Notes

- **Decided at planning (2026-11-25, by the person):** one tool, four kinds, n-gon caps, seams and UVs included, ids in
  the documented ring order. Recommended by me and taken by the person: no fan-cap option, no tool per kind.
- **Design-heavy:** the id order and the argument names are a public format. T-213, T-215 and T-216 build on them, and
  M-36 may call them from a script. Write the id order into the description and `### As built`.
- New tool: `PartToolsListTests`' digest and the tools/list pins gain an entry. Re-take a digest for an added entry
  only. If an existing tool's entry would change, ask first.
- Defaults (radius 0.5, segments 16, sphere rings 8, the ranges) are mine, not the person's: change one only by asking.
- Ask, don't land: if Core's unwrap gives a UV warning on any primitive at the defaults, bring the figures back.
- Plan against M-29 as landed (the `help` tool, the 3,686 budget, its `define_mesh` text). This milestone starts after
  M-29 finishes.
