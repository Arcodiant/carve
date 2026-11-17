---
description: >-
  Call the real MCP tools in the test process with no child process and no engine, decided with the person
---
# T-87 — In-process tool harness

**Status:** In Progress

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
