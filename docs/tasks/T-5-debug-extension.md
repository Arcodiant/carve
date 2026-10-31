---
description: >-
  A debug extension drives the product unattended through an in-memory MCP client with a fixed clock, seed and run folder
---
# T-5 — Debug extension

**Status:** Open

**Pri:** P1

**Milestone:** M-1

**Batch:** — (standalone)

**Depends on:** T-2, T-4

## Goal

The shipped product can be run unattended and repeatably with the debug extension stacked on it, driving tools through a real MCP client.

## Scope

**`extensions/Carve.Debug`** (new, in `Carve.slnx`, referenced by nothing in `src/`), an `ICarveExtension` on the public API alone:

- Transport: replaces stdio with `WithStreamServerTransport` over in-memory pipes. Its `ScenarioDriver` holds the SDK's `McpClient` on the other end, so every scripted call goes through real JSON, schema binding and dispatch.
- Fixed clock: `Empyrean:Time:FrameDeltaTime = 1/60` as a default below the command line. **Also turns `PaceToFixedStep` off**: the engine ignores it on the fixed clock and logs a warning.
- Fixed seed: `Carve:Debug:Seed`, default `1`, exposed as a seeded service for the day something random arrives.
- A run folder: `Carve:Debug:RunFolder`, default `artifacts/scenarios/<scenario>/<run number>/`, holding frames, a `user-data` folder that is fresh each run, and a JSON log of every call and result.
- Audio: Carve has none; the run log records `audio: none`.
- `IScenario` (`Task RunAsync(ScenarioDriver)`), registered by a scenario extension. The driver has `CallAsync`, `CaptureAsync(name, args)` which saves a `render` result's PNG in the run folder, `AssertBytesEqual`, and `Fail`. When the scenario ends the host stops, exiting 0, or 1 on failure with the failure on stderr.
- State inspection: `tools/list` and the run log. Nothing reaches into the product's internals; if a scenario needs more, widen `Carve.Extensibility`.

**`scripts/run-scenario.ps1 <name>`**: builds, then starts the real `carve` with `Carve:Extensions` listing the debug extension and that scenario, and returns its exit code and run folder. It stops only the process it started.

Tests: `tests/Carve.Server.Tests` starts `carve` with the debug extension and a tiny test scenario calling `tools/list`, and asserts exit 0, a run folder, and 0 bytes on stdout. A test asserts the fixed clock option reached the engine. The guard test from T-2 still passes.

Add `extensions/` to `CLAUDE.md`'s *Layout* table.

## Notes

- **Decided by the person:** real MCP client in process over a swapped transport; explicit extension list; no scenarios executable, the harness is `carve` itself. Probed in `artifacts/m1-probe2/`: `McpClient` to `WithStreamServerTransport`, render round trip, 0 bytes on stdout.
- No flag in the product. If the debug extension cannot do something through the API, widen the API in `Carve.Extensibility`, never special-case `Carve.Server`.
- **Decides something others build on**: `ScenarioDriver` is what every later scenario is written against. Design-heavy.
- Writes down for T-6: how a scenario project is laid out and registered, in `CLAUDE.md`'s scenarios section.
