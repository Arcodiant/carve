---
description: >-
  An agent defines a polygon mesh by coordinates through define_mesh and gets counts, bounds and warnings back
---
# T-3 — define_mesh tool

**Status:** Open

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** T-1, T-2

## Goal

An agent defines a mesh by coordinates through `define_mesh`, and the result tells it exactly what it made or exactly what is wrong.

## Scope

In `src/Carve.Server`, a tool class of its own:

- `define_mesh(vertices: number[][], faces: int[][])`, with a JSON schema that says each vertex has three numbers.
- `ModelStore` singleton: holds the one current `PolygonMesh` as an immutable reference plus a version counter. Each successful call replaces it; a failed call leaves it alone.
- The return, as JSON text: `{ vertices, faces, edges, closed, bounds: {min, max}, warnings: [] }`. Every failure from `PolygonMesh.Create` is an MCP tool error (`isError: true`) listing every problem by element, not an exception and not the first problem only.
- **The tool description, as a priority**: it is the interface. It states that coordinates are unitless, Y is up, faces wind counter-clockwise from outside, faces may have any number of sides, and a call replaces the mesh. It carries the full cube as a worked example an agent can copy.

Tests in `tests/Carve.Server.Tests`, through the SDK's client over a pipe where practical: the cube round trip; a bad index comes back as a tool error naming `faces[2][1]`; a failed call keeps the previous mesh; an open quad succeeds with a warning; `tools/list` shows the schema and description. A test parses the worked example out of the description and asserts it is accepted with `closed: true` and no warnings, so the example cannot rot.

No test scenario: nothing is on screen until T-4; T-6 walks this tool end to end.

## Notes

- **Decided by the person:** indexed polygons, one current mesh replaced by each call, counts, bounds and warnings returned, no geometry echoed. Open or inconsistent surfaces are warnings, bad input is an error.
- Do not add a name argument, element ids or partial edits. M-2 decides addressing.
- `ModelStore` is written on the MCP thread and read on the frame thread: it publishes immutable values only.
- Tool text may be sharpened by T-7 after a real agent has used it.
- Writes down for T-4: `ModelStore`'s version contract.
