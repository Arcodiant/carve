---
description: >-
  patina, a second MCP server: define_graph, edit_graph, get_graph, save, open, push_snapshot and pop_snapshot
---
# T-113 — The patina server and its graph tools

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-110, T-111

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

`patina`, a second MCP server reachable from a plain launch, on which an agent defines, edits, reads back, saves and
opens one texture graph, and can push and pop snapshots of it to try a change and go back.

## Scope

- **Project `src/Patina.Server`** (exe `patina`, MCP server name `patina`) hosting through T-111's `Carve.Hosting`
  with its own name, log file prefix and tools. It loads extensions as `carve` does. Stdout belongs to MCP.
  `dotnet run --project src/Patina.Server` starts it. The README gains a *patina* section with an `.mcp.json` entry
  beside carve's.
- **One open graph per session**, held in a store, with its path once it has one.
- **Tools.** Each description has a worked example, as Carve's do, and every error names the node and the port or
  parameter (T-110's faults, returned as they stand):
  - `define_graph`: the whole graph as JSON (the `.texgraph` body). Replaces the open graph, clears the snapshot
    stack, and returns the graph's faults (none means it evaluates).
  - `edit_graph`: a list of operations, applied all or nothing: `set_node` (add or replace a node by id),
    `remove_node`, `connect` (`node`, `port`, `from`), `disconnect`, `set_params` (merge), and `set` for `size` and
    `seed`. A list that leaves a new fault changes nothing and returns the faults. It answers with a short summary
    of what changed.
  - `get_graph`: the open graph as `.texgraph` JSON, its path, and its faults.
  - `save`: `path` (`.texgraph`; the extension is added if missing), written as T-110 writes it. Refuses while
    the graph has a fault, listing the faults.
  - `open`: `path`. A file that does not parse is refused with its position, and a missing file lists the
    `.texgraph` files in its folder (as T-81's missing texture does). A graph with reference faults opens and is
    reported fault by fault, so the agent can fix it with `edit_graph`. Clears the snapshot stack.
  - `push_snapshot`: optional `label`; copies the open graph onto a stack and returns the depth.
  - `pop_snapshot`: restores the graph on top of the stack and removes it, returning its label and the new depth.
    On an empty stack it is an error that says the stack is empty. The stack lives in the session only and is never
    saved.
- **Tests.** Tool behaviour on M-20's in-process harness pattern at `unit` tier, in suite `patina-unit`: every tool,
  every fault path, all-or-nothing edits, the snapshot stack (push, edit, pop restores byte-for-byte; pop on empty;
  `open` and `define_graph` clear it). A `regression` class starts the real `patina` over stdio once for
  `tools/list` and one round trip, and the product-reference guard covers `patina`.
- **The scenario harness for patina.** `scripts/run-scenario.ps1` gains `-Server carve|patina` (default `carve`),
  launching `patina.dll` with the same `--Carve:Extensions:N` arguments. New scenario `scenarios/Carve.Scenario.Patina`
  with a stage list in the manner of `CutsStages.cs` (`PatinaStages.cs`; a later task adds its file and one line).
  This task's stage, `GraphStage.cs`: define a small graph, edit it, read it back, push, break it with an edit,
  pop, save, open again and compare by content, then open a file with a planted bad input id and see the fault named.
  Suite `scenario-patina` (regression; `cpu` is enough until T-114 adds frames).
- CLAUDE.md's Layout: rows for `src/Patina.Server`, its tests and the scenario. `Carve.slnx` gains the projects.

## Notes

- Decided at planning (2026-11-17, by the person): the name `patina`; push/pop snapshots, not an undo history; no
  `list_snapshots` or `drop_snapshot`.
- Decided at planning by the orchestrator: a graph with reference faults opens, so the acceptance plant (T-117) can
  be found and fixed through the tools alone; `save` refuses a faulty graph. Stop if either proves wrong in use.
- **Design-heavy: it decides patina's public tool surface**, which agents learn from (descriptions are content).
  Opus. Work every worked example in every description through a test. M-7's `set_material` example promised
  something its own test contradicted.
- Tool names, argument names and the planks example must match T-112's node types and parameters as written in
  T-112. A mismatch is a stop, not a rename.
- Owns `run-scenario.ps1`, `src/Patina.Server/Program.cs` (T-114 and T-115 add a line each to its tool list, and
  their own stage files) and the README's patina section.
- Runs beside T-112. The two touch no common file except `tests.json`, where a conflict only adds.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit and scenario-patina are in `tests.json`, add them to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`) to the call checkpoint, finished by a second agent, also on Claude Opus 5.5
(`claude-opus-5-5`), after T-112 landed.

- **`src/Patina.Server`** (exe `patina`): `Program.cs` is one `EngineHost.RunAsync` call (`Name = "patina"`) and holds the
  tool list, `Program.Tools(registry)`, and `Program.AddTools(services, mcp, baseDirectory)` (store, registry, tool
  folder, tools), which the unit harness calls too. `GraphStore` holds the open graph, its path and the snapshot stack,
  one lock a call. Tools `define_graph`, `edit_graph` (`DefineGraphTool.cs`, `EditGraphTool.cs`), `get_graph`, `save`,
  `open`, `push_snapshot`, `pop_snapshot` (`FileTools.cs`), helpers in `ToolSupport.cs`. define_graph's list of node
  types is written from the registry by `NodeCatalogue` (ports, parameters with `ParamSpec.Describe()` and defaults),
  with one hand-written line per type, checked against T-112's nodes as landed (names, parameters, ranges and
  behaviour all match; no rename was needed).
- **Tests** (`tests/Patina.Server.Tests`, 66): `PatinaHarness` (in-process), `WorkedExampleTests` (every description's
  examples, held to the description's own constants), `GraphToolTests`, `SnapshotTests`, `PatinaProcessTests`
  (regression: the real patina over stdio) and `PatinaReferencesNoExtensionTests` (carve's `ProductReferenceGuard.cs`
  linked). After the rebase onto T-112 all 66 pass; the fault text quoted in the worked examples was already T-110's,
  so no constant changed.
- **Scenario**: `scenarios/Carve.Scenario.Patina` (`PatinaStages.cs`, `GraphStage.cs`), `run-scenario.ps1 -Server
  carve|patina`. Suites `patina-unit` (both test dlls in one `dotnet test`), `patina-regression` and `scenario-patina`.
- **`cpu` checked.** patina opens the engine's headless Vulkan device at start, but no tool awaits `EngineStatus`, and
  `EngineThread` catches a failed device and keeps the process serving. Run here with the Vulkan driver hidden
  (`VK_ICD_FILENAMES` and `VK_DRIVER_FILES` set to a missing file), the log shows `Engine faulted ... is a Vulkan device
  available?` and both `patina-regression` and `scenario-patina` still pass, so both stay `cpu`. T-114, the first to
  render, must move `scenario-patina` (and `patina-regression` if it renders) to `gpu`.
- **edit_graph's rule for a new fault**: a fault counts as new by its place (node, port, parameter), not its message,
  since messages list ids an edit may add; so an edit that only changes the kind of a fault already at that place is
  let through.

**For T-114 and T-115.** A tool is a class with a static `Create()` (given the `NodeRegistry` if it needs one) built on `ToolSupport.Create<T>`,
plus one line in `Program.Tools` in `src/Patina.Server/Program.cs`; its worked examples go in constants on the class
and a test in `WorkedExampleTests` runs each as written. A stage is a class implementing `IPatinaStage` in its own file
under `scenarios/Carve.Scenario.Patina`, plus one line in `PatinaStages.All`.
