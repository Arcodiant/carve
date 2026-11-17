---
description: >-
  Call the real MCP tools in the test process with no child process and no engine, decided with the person
---
# T-87 — In-process tool harness

**Status:** Done

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression

## Goal

A unit-tier way to call Carve's real MCP tools, the same `McpServerTool` instances `Program.cs` registers, inside
the test process: no `dotnet exec`, no engine thread, no Vulkan device, and well under a second per test. T-88 and
T-89 move checks onto it.

## Scope

- **Settle the approach with the person before building it.** `Carve.Server.Tests` builds `carve` with
  `ReferenceOutputAssembly="false"` on purpose. Its pure parts link single product files (`ModelStore.cs`,
  `GlbExport.cs`, ...) rather than referencing the product. Candidates:
  - link the tool sources (`*Tool.cs`, `MeshToolSupport.cs`) the same way;
  - reference `carve.dll` and reach the tools through `InternalsVisibleTo`;
  - an MCP client over in-memory streams, as `Carve.Debug`'s `PipeClientTransport` does, against a host built
    with no engine services.

  Weigh what each one tests that ships, what it would let drift, and the product-reference guard
  (`ProductReferenceGuard.cs`).
- Build the harness: a model store, the tool list as `tools/list` returns it, and `CallToolAsync(name, json)`
  giving the same result element `CarveProcess` gives. A test should move to it by changing its start line.
- Tools that need the engine (`render`, looks) refuse clearly on the harness rather than hang.
- Measure: one `CarveProcess` start against one harness start, recorded in this file's notes.

## Notes

### Rulings (decided with the person, 2026-11-17)

1. **Approach C: an MCP client over in-memory streams**, as `extensions/Carve.Debug`'s `PipeClientTransport` does,
   against a host built with no engine services. A small product change is allowed for it: the tool and service
   registration (`ModelStore`, `EngineStatus`, the `mcp.WithTools([...])` list, the `services.AddSingleton(*Tool.Create)`
   lines) moves out of `Program.cs`'s top-level statements into a method `Program.cs` calls; engine wiring, transport and
   extensions stay in `Program.cs`, whose behaviour must not change. Engine tools (`render`, looks) refuse clearly on
   the harness rather than hang: `EngineStatus` is marked faulted with a clear message (InternalsVisibleTo or a small
   public way to build an engine-less status, the implementer's choice). `tests/Carve.Server.Tests` may reference
   `carve.dll`; its "never linked" comment is rewritten to say why, and `ProductReferenceGuard.cs` must still guard the
   product.
2. **Paths:** the harness passes a base directory through `ToolPaths`' existing `baseDirectory` parameter (a small
   product change is allowed so a tool resolves relative paths against a per-host base directory rather than
   `Environment.CurrentDirectory`), so moved tests keep their relative paths. The process path behaves exactly as now.
3. **Tools only:** the harness registers the tools; it runs no extension hooks or configuration. Extension loading stays
   covered in server-regression.
4. The harness gives a model store, the tool list as `tools/list` returns it, and `CallToolAsync(name, json)` returning
   the same result element `CarveProcess` gives, so a test moves over by changing its start line; proven by a few
   `unit` tests on the harness. The existing regression classes are not moved (T-88/T-89). One `CarveProcess` start is
   measured against one harness start and both recorded here.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`).

- **`src/Carve.Server/CarveServer.cs`**: `CarveServer.AddTools(services, mcp, baseDirectory = null)` registers
  `RenderTool.Create`, the 33-tool `WithTools` list, `EngineStatus`, `ModelStore`, `RenderQueue` (render's own
  parameter, so render binds on the harness) and a new `ToolFolder(baseDirectory)`. `Program.cs` calls it with no base
  directory in the same place the lines were; `RenderComposition`, the render pipeline, `RenderSystem` and
  `EngineThread` stay in `Program.cs`.
- **Paths**: `ToolFolder.Directory` is passed to `ToolPaths.Resolve` by save, open, export and define_material (its
  texture paths), and to `LimitsSource.Find` (export's `limits`), which now looks at a relative argument from it too.
  Null, which is all carve itself ever passes, is the working directory at the time of the call, as before; the tool
  descriptions are unchanged.
- **No engine**: chose a small public factory, `EngineStatus.Faulted(message)`, over InternalsVisibleTo (which would
  open every internal type of carve to the tests). The harness replaces the registered status with one faulted with
  `ToolHarness.NoEngine` ("render: no engine in the unit harness; ..."), so both of render's paths (the looks and
  `mode: "uvs"`) return that tool error at once after their argument checks.
- **`tests/Carve.Server.Tests/ToolHarness.cs`**: builds an empty host with `AddMcpServer`, the stream transport over two
  `Pipe`s and `CarveServer.AddTools`, and talks to it with `McpClient` over a private copy of the debug extension's
  three-line `PipeClientTransport` (that one is internal to Carve.Debug). The same members as `CarveProcess`:
  `new ToolHarness(workingDirectory: Root)`, `InitializeAsync()`, `ListToolsAsync()`, `CallToolAsync(name, json)`, plus
  `Store`. Requests go as raw `JsonRpcRequest`s so the result is the JSON the server sent. The client asks for protocol
  `2025-06-18`, the version `CarveProcess` asks for: left to negotiate, the SDK picks a later one whose results carry
  `resultType`, and they would no longer equal the process's.
- **Reference**: carve is referenced with `Aliases="carve"`, because the test project still links `ModelStore.cs`,
  `ToolPaths.cs` and the other product files and the two copies would otherwise clash; the harness names carve's types
  through `extern alias carve`. The csproj comment says why the reference is there. `ProductReferenceGuard` is
  unaffected: it checks `src/Carve.Server`'s project closure, `carve.dll`, its `.deps.json` and its output folder, none
  of which this reference changes, and its tests pass in server-unit/server-regression.
- **Tests** (`ToolHarnessTests.cs`): `unit` — 34 tools listed with closed schemas; define_mesh then get_mesh round
  trip, also seen through `Store`; an unknown argument is a tool error; render refuses with `NoEngine` in under 5 s
  (shaded, wireframe, uvs; each ran 20-30 ms); a relative save/open lands under the harness's folder and not the test
  process's. `regression` (`ToolHarnessMatchesProcessTests`, it starts carve) — `tools/list` byte-equal between the
  process and the harness for the same build, and define_mesh, move, get_mesh, check and undo results byte-equal.
- **Timings** (this machine, Debug, from that regression test's output): one `CarveProcess` start to an answered
  `initialize` 370-413 ms over three runs (2.2 s on the first, disk-cold run); one harness start 14-21 ms once the
  test process has JIT-compiled the host, about 280 ms for the very first harness in a fresh test process. The unit
  harness tests ran 20-90 ms each.

Not checked: the harness under Release; any tool's result compared with the process's beyond the five in the
regression test and `tools/list`; extension hooks (by ruling 3, not on the harness). The process path was checked by
the whole server-regression suite passing on this branch, not by a test of its own for `ToolFolder(null)`.
