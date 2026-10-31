---
description: >-
  carve starts as a generic-host MCP server over stdio, runs the engine on its own thread and loads extensions from an explicit list
---
# T-2 — Server host and extension API

**Status:** Open

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

`carve` starts as a real MCP server over stdio, with the engine alive on its own thread and extensions loaded from an explicit list, and the guard test proves the product references none of them.

## Scope

**`src/Carve.Extensibility`** (new, public, references only `Microsoft.Extensions.*` abstractions and the MCP SDK types an extension needs): `ICarveExtension` with a pre-build hook taking a `CarveHostBuilderContext` (an `IServiceCollection`, the `IConfigurationManager`, the `IMcpServerBuilder`) and a started hook taking the built `IServiceProvider`.

**`src/Carve.Server`**:

- `Program.cs`: `Host.CreateEmptyApplicationBuilder`; configuration from the command line and environment variables; logging to stderr only (console provider with `LogToStandardErrorThreshold = Trace`) and a file in `$env:TEMP`.
- `ExtensionLoader`: reads `Carve:Extensions` (ordered DLL paths; default empty), loads each with its dependencies resolved beside it (`AssemblyDependencyResolver`), instantiates each `ICarveExtension` in order. A path that does not load, or holds no extension, is a fatal stderr error naming it and a non-zero exit. No scanning.
- Composition order: extension pre-build hooks first, then `AddEmpyreanCore`, `AddEmpyreanGraphicsHeadlessCore`, `AddEmpyreanConfiguration` and `AddMcpServer`, so first-wins engine substitution works. The default transport is `WithStdioServerTransport`, registered only if no extension supplied a transport. Tools come from `WithToolsFromAssembly`, so each later tool is a file of its own and no task edits a shared list.
- `EngineThread : IHostedService`: a dedicated thread named `frame` runs `IGameLoop.Initialise` then `Run`. `StartAsync` does not wait for the engine, so `initialize` answers at once. An `EngineStatus` service exposes ready, faulted (with the message) and a task to await. `StopAsync` calls `IGameLifecycle.RequestStop` and joins. A Vulkan failure is held in `EngineStatus`: the process and MCP stay up.
- Pacing defaults for the product: `Empyrean:Time:FixedDeltaTime = 0.05`, `PaceToFixedStep = true`, set as in-memory configuration below the command line.
- Packages: `ModelContextProtocol` 2.3.0, `Microsoft.Extensions.Hosting` 10.0.13, `Arcodiant.Empyrean.Graphics` at `$(EmpyreanVersion)`. Not `Hosting`, not `Hosting.Glfw`. Add the two new versions as properties beside `EmpyreanVersion` in `Directory.Build.props`.

**`tests/Carve.Server.Tests`** (new, in `Carve.slnx`):

- The guard test: parses `src/Carve.Server/Carve.Server.csproj` for any reference to `Carve.Debug`, `Carve.Scenario*` or anything under `extensions/` or `scenarios/`, and reads the built `carve.dll`'s assembly references for the same. A small fixture proves the check fails on a bad reference, so it cannot pass vacuously.
- Stdout test: starts the real `carve` process, does `initialize` and `tools/list`, and asserts every stdout line is a JSON-RPC message; closing stdin exits 0 within 5 s.
- Loader tests: an empty list loads nothing; a bad path exits non-zero naming it; a test extension's pre-build and started hooks run in order.
- No-device test: with `VK_LOADER_DRIVERS_SELECT=nonexistent` in the child's environment, `initialize` still answers.

Scenarios: none here, the harness arrives in T-5. Update `CLAUDE.md`'s *Layout* table with the new projects, and in D-1 answer *Where the renderer lives*: in process, generic host, engine on its own thread, with the child process as the rejected alternative.

## Notes

- **Decided by the person:** generic host plus our own `IHostedService`, one container, not `EmpyreanHost` (which refuses `IHostedService`). Explicit extension list only. Official SDK. The probe at `artifacts/m1-probe2/` on the planning machine ran this composition: pixels identical to the `EmpyreanHost` probe, about 1 s to first image, clean exit on stdin EOF, 0 bytes on stdout under the in-memory transport.
- That probe's engine failure was fatal because its `StartAsync` awaited the engine; not awaiting it is this task's work and is not yet proven.
- No tool is registered here; `tools/list` is empty. T-4 is the first to read `EngineStatus`.
- Files owned: `Carve.Server`, `Carve.Extensibility`, `Carve.slnx`, `Directory.Build.props`, `CLAUDE.md` *Layout* rows, D-1. T-1 touches none of these, so they run side by side.
- **Design-heavy and decides what others build on**: the extension API is a public API. Scenarios are meant to pressure it, so T-5 may widen it; no internals access is ever granted.
- If the frame thread leaving the main thread breaks anything in the engine, that is an engine need: record it in D-1's table, do not work around it silently.
- Writes down for T-3, T-4 and T-5: how a tool is declared, `EngineStatus`, the extension hook names, in `CLAUDE.md` or the task's closing report.
