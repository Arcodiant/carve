---
description: >-
  An agent defines a polygon mesh by coordinates through define_mesh and gets counts, bounds and warnings back
---
# T-3 — define_mesh tool

**Status:** Done

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

### As built

Ran on Claude Sonnet 5.5 (`claude-sonnet-5-5`). `define_mesh` is built and tested through the real `carve` process over stdio; nothing is on screen, so no scenario was run.

- `src/Carve.Server/DefineMeshTool.cs`: the tool; `ModelStore.cs`: the store. The tool is **not** found by `WithToolsFromAssembly`: it is built by `DefineMeshTool.Create()` and registered with `mcp.WithTools(...)` in `Program.cs`, because the attribute route cannot put `minItems`/`maxItems: 3` on a vertex in the schema (done through `SchemaCreateOptions.TransformSchemaNode`; the node is matched by shape, since its path is not what one expects). The class takes `ModelStore` in its constructor, resolved from `request.Services` per call: a static method given to `McpServerTool.Create` with `target: null` treats a `ModelStore` parameter as a tool argument. A later tool may still use the attribute route from T-2's notes.
- Return: `{vertices, faces, edges, closed, bounds:{min,max}, warnings:[strings "element: message"]}`. Failure is `isError: true` with a text listing every problem, one per line, ending with "the current mesh is unchanged". The tool binds its two arguments raw (`JsonElement?`) and validates them itself, since the server does not enforce the schema: beyond `PolygonMesh.Create`'s errors it reports, all in the one error and each naming its element, a missing, null or non-array `vertices`/`faces`, a vertex that is not exactly 3 numbers (`vertices[i]`), a non-number coordinate (`vertices[i][k]`), a face that is not a list (`faces[i]`, once), and an index that is not a JSON integer (`faces[i][k]`; `1.0` is accepted as 1, `1.5`, strings, null and values beyond 32 bits are errors). The published `inputSchema` is written out in `DefineMeshTool.Schema()` and set on `ProtocolTool.InputSchema` after the tool is created (the typed `double[][]`/`int[][]` signature it replaces had produced the same schema; a test pins it).
- The description ends with `Worked example (a unit cube): {json}` (`DefineMeshTool.ExampleMarker`, `CubeExample`); a test cuts the example from `tools/list` and calls with it, expecting `closed: true`, no warnings.
- Tests: `tests/Carve.Server.Tests/DefineMeshTests.cs` (cube round trip from the description, bad index naming `faces[2][2]` among several, short vertex, failed call keeps the mesh then an open quad warns, schema and description phrases) and `ModelStoreTests.cs`. The Server tests do not link the product, so the test project compiles `ModelStore.cs` as a linked file and references Core.

**For T-4, `ModelStore`** (singleton, registered in `Program.cs`): `Current` returns a `ModelSnapshot(PolygonMesh? Mesh, long Version)`, read atomically from any thread (mesh and version are one immutable record, never torn). `Mesh` is null and `Version` 0 until the first success; each successful call does `Replace` and raises `Version` by exactly 1, a failed call changes nothing. The frame thread keeps the last `Version` it built and rebuilds when it differs. The mesh is polygons: triangulate with `Triangulator.Triangulate(mesh)`.

Review fix (RV-1): the earlier typed `double[][]`/`int[][]` parameters failed in the SDK's binding before `Run`, returning only `An error occurred invoking 'define_mesh'.` for a fractional index, a null element or a missing argument; that is what the raw binding above replaces.

Not checked: concurrent reads while writing beyond the by-construction argument (single reference swap); a coordinate beyond float range (becomes Infinity and is refused by Core as not finite, not tested here).

Two T-2 tests asserted an empty or single-tool `tools/list` (`StdioTests`, `LoaderTests.An_extensions_tool_reaches_tools_list`); they now assert `define_mesh` is listed (and `test_ping` beside it). That is the count following the new tool, not a looser check.
