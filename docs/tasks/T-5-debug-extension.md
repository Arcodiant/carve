---
description: >-
  A debug extension drives the product unattended through an in-memory MCP client with a fixed clock, seed and run folder
---
# T-5 — Debug extension

**Status:** Done

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

### As built

Ran on Opus 5.5 (`claude-opus-5-5`). `verify` passes, and `run-scenario.ps1 tiny` was run for real, with its build: exit 0, a run folder, `audio: none` in the log. No frame was looked at here: the only frames are the tiny scenario's quad, checked by bytes and by the PNG's hash. T-6 is the first to look.

**Files.** `extensions/Carve.Debug`: `DebugExtension`, `IScenario`, `ScenarioDriver`, `ScenarioFrame`, `ScenarioFailedException`, `SeededRandom` (public); `RunFolder`, `RunLog`, `PipeClientTransport` (internal). `scripts/run-scenario.ps1`. `tests/Carve.Scenario.Tiny`, and `DebugExtensionTests`. `Carve.Extensibility` was not widened: T-2's hooks carried all of it. Stacking worked as T-2 said, and these tests are its first real case. No engine need.

**For T-6, `ScenarioDriver`.**

- `ListToolsAsync()`: `tools/list`, as the SDK's `Tool`s.
- `CallAsync(tool, args)`: `args` is any object whose JSON is the arguments (`new { view = "back" }`, anonymous arrays included). A tool error comes back as a `CallToolResult` with `IsError` set, not a failure, so T-6's step 5 and 6 use this. A protocol error, such as an unknown tool, throws, failing the run unless caught.
- `CaptureAsync(name, args)`: `render` with those args, saved as `frames/<name>.png`. Returns a `ScenarioFrame`: `Name`, `Path`, `Png`, `Width`, `Height`, `Pixels` (decoded RGBA, rows from the top), `Caption`, and `Pixel(x, y)` returning `(R, G, B, A)`. A tool error, no image, a name used before in the run (ignoring case), or a name with anything but letters, digits, `.`, `-`, `_` fails the run. So the repeat render of step 2 needs a name of its own, e.g. `cube-front-again`.
- `AssertBytesEqual(what, expected, actual)`: pass `frame.Pixels`; the failure names the first differing byte.
- `Fail(message)`: throws `ScenarioFailedException`, and the failure sticks even if a scenario catches it.
- `RunFolder`, `UserDataFolder`: full paths.

**For T-6, the scenario project.** As `CLAUDE.md`'s *Writing a scenario* says; `tests/Carve.Scenario.Tiny/TinyScenario.cs` is the worked example, though its engine package is for its clock test only. The project must set `EnableDynamicLoading`. Sampling positions are T-4's, hard-coded: `ViewFraming` is the product's and a scenario cannot reach it.

**For T-6, `run-scenario.ps1 <name> [-Configuration Debug] [-TimeoutSeconds 300] [-NoBuild] [-CarveArguments a,b]`.** Finds `Carve.Scenario.<name>` under `scenarios/`, then `tests/`. Builds `carve`, `Carve.Debug` and the scenario, with the build's output on stderr. Runs with the worktree root as the working directory, so the default run folder is the worktree's `artifacts/`. **Its stdout is the run folder's path and nothing else**; `$LASTEXITCODE` is `carve`'s. Exit 2 is no such project. In `verify.ps1`, after the tests: `& (Join-Path $PSScriptRoot 'run-scenario.ps1') cube | Out-Null`, then test `$LASTEXITCODE`. Stderr arrives when the run ends, not as it goes, because it is read from a temp file. The two-run comparison can take the two printed folders.

**Readings of the spec.**

- A scenario's name, which `IScenario` as specified does not carry, is the last dotted part of its assembly name, lower-cased, so `run-scenario.ps1 cube` and `artifacts/scenarios/cube/` agree with no second place to say it.
- `<run number>` is one above the highest number in the folder, from 1. The relative default is against the working directory. Two runs at once could pick the same number; nothing here runs two.
- `Carve:Debug:RunFolder` naming a used folder: `frames/` and `user-data/` are cleared, `run.json` replaced, anything else left alone. **Cleared only if the folder holds a `run.json`** (review RV-1); without one, a `frames/` or `user-data/` that exists and is not empty is refused as a fatal `OnStarted` error, and left as it was. Empty ones are used.
- `run.json` is one JSON document, rewritten after each entry so a run that dies keeps what it did. It has `scenario`, `seed`, `audio`, `outcome` (`running`, `passed`, `failed`), `failure` and `calls`. No timestamps, so it compares from run to run. **An image in a result is its byte count and SHA-256, not its base64.** A captured one is in `frames/`; a render made with `CallAsync` is kept only as that hash. Full base64 would be a two-line change if the person prefers the log literal.
- No scenario, or more than one, is a fatal `OnStarted` error, exit 1, with no run folder made.
- **The script's timeout is my addition**, so an unattended `verify` cannot hang: 300 s, then it stops the process it started, tree included, and exits 124. The spec gives no number; it is a parameter.
- Any stdout from `carve` makes the script fail, since the milestone's rule is that stdout belongs to MCP. The script copies what it caught to stderr first; the tiny scenario's `stdout` mode and a test hold it to that.
- **Exit 0 needs a concluded pass** (review RV-1). `OnStarted` sets `Environment.ExitCode = 1`; only the scenario concluding passed sets 0. A host stop first concludes the run as failed (`the host stopped before the scenario concluded`, in `run.json` and on stderr), and the first conclusion stands. Tiny's `stop-host` mode and a test cover it. `IScenario.RunAsync` still takes no token.
- `run-scenario.ps1` matches the project folder without regard to case, so `cube` does not rest on a case-insensitive filesystem.
- The product puts the environment and command line back on top after each extension's `ConfigureHost`, so a later extension reads the person's switch (tiny's `configure-pacing` mode and a test).

**SDK.** `ModelContextProtocol` 2.3.0 has no public stream client transport (the planning probe's code is not on this machine, so how it connected was not seen). `PipeClientTransport` is an `IClientTransport` handing the client the SDK's own `StreamServerTransport` on the other ends of the pipes; that class is plain JSON-RPC lines and takes no side. Every call still takes real JSON, schema binding and dispatch.

**Checks.** The fixed clock test reads `IOptions<TimeOptions>` and the engine's own `ITimeService.DeltaTime` after a render, both 1/60, and asserts no `warn:` at all on stderr. Passing `--Empyrean:Time:PaceToFixedStep=true` by hand did produce the engine's warning, so that assertion bites. The engine types are read in the tiny scenario, not the debug extension, and a test holds `Carve.Debug.dll` to no reference to `carve`, `Carve.Core` or the engine.

**Not checked:** a scenario under `scenarios/` (none yet; the script's search of that folder is unrun); frames compared across processes under the fixed clock; `user-data` being used by anything, since the engine has no such setting and the product keeps no user data, so it is a folder a scenario can write to; a hung scenario's own failure inside `carve`, which has no timeout, only the script's; the frame thread spins unpaced on this clock, which was not measured.
