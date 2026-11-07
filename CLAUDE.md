# Carve

A 3D modelling tool for Claude agents, served over **MCP**. An agent defines and
edits meshes by coordinates or with tools (cut, extrude, move a vertex, edge or
face), looks at the result (screenshots, and render modes such as UVs or
normals), runs queries ("select all unattached vertices", "select all faces
above y=10"), and exports models that games on the **Empyrean** engine import as
content. The concept, decisions and open questions are in **D-1**.

## Planning rules

Every plan, for a milestone or a task, keeps these:

- **Content comes with its mechanic, as a priority.** Content (here: the sample
  models a tool is shown on, its render-mode looks, its tool descriptions and
  the worked examples agents learn from) is built in the same milestone and the
  same task as the mechanic or feature it belongs to, so each milestone's
  mechanics arrive showing something. Placeholder content may stand in where the
  real thing is not ready; absent content may not. Content is never a milestone
  of its own.
- **A thin slice first.** One small, complete slice of the whole is built end to
  end first; wider content and secondary systems come after it. No polish-only
  milestone while the core has a gap.
- **Every milestone is reachable and adds something.** Its result is reachable
  from a plain launch, through the product's own interface (MCP tools, called
  from an ordinary client) and never a debug flag, and adds something that can
  be used or looked at that could not be before.
- **Size by feedback.** A task is one to three commits; a milestone roughly four
  to eight tasks. A point mid-milestone where something could be tried and
  learned from is a milestone boundary.

## Architecture rules

- **`Carve.Core` never references the engine**, `System.Random` or the wall
  clock. It owns meshes, edit operations and queries, so all of it is testable
  with no GPU. `CoreIsEngineFreeTests` enforces the engine half.
- **`Carve.Server` is presentation and transport**: the MCP surface, rendering,
  export. If it is deciding what an edit does to a mesh, that belongs in the
  Core.
- **Stdout belongs to MCP.** On the stdio transport nothing but protocol goes to
  stdout; logs go to stderr or the temp folder.

- **Renders are Empyrean's**, so a screenshot shows the model as the game would.
- **The mesh is polygons**, triangles are a derived view for render and export, never stored. Work persists in a native
  Carve file; `.glb` is what reaches a game.
- **The product has no window.** Every look is an offscreen render.
- **The Carve file is versioned JSON geometry**, read back bit-exact; the `.glb` export has flat normals and is written with SharpGLTF (D-1, M-3).

The rest are decided in D-1 as they settle; add them here when they do.

## Layout

| Path | What |
|---|---|
| `src/Carve.Core` | Engine-free modelling kernel |
| `src/Carve.Extensibility` | The public extension API: `ICarveExtension`, `CarveHostBuilderContext` |
| `src/Carve.Server` | The shipped product, the `carve` MCP server |
| `extensions/Carve.Debug` | The debug extension: in-memory transport, `ScenarioDriver`, fixed clock and seed, run folder. Referenced by nothing in `src/` |
| `scenarios/Carve.Scenario.<Name>` | One project per scenario, run by `run-scenario.ps1`; `Cube` is the first: a cube, an L-block, a long box, both views, and four cube frames by direction and zoom |
| `scenarios/Carve.Scenario.Crate` | The crate: one cube built into a crate with `move` and `extrude` only, five views, undo and error paths; suite `scenario-crate` |
| `scenarios/Carve.Scenario.Export` | The crate saved, reopened and exported into a game content folder in the run folder, then shown by the viewer; suite `scenario-export`, run by `scripts/run-export-scenario.ps1` with the checks in `tests/Carve.ExportViewer.Tests` |
| `spikes/Carve.ModelViewer` | The model viewer, a stand-in for a game: loads a `.glb` through `IModelLoader` and captures fixed views. It is not the scenario harness, and nothing in the product uses it |
| `tests/Carve.Core.Tests` | xunit tests of the kernel, incl. the engine-free guard |
| `tests/Carve.Server.Tests` | xunit tests that start the real `carve`, incl. the no-extension guard |
| `tests/Carve.Server.Tests.Extension` | The extension the loader tests load, on the public API alone |
| `tests/Carve.ModelViewer.Tests` | The viewer on its own fixture cube, through `run-viewer.ps1`; suite `viewer-cube` |
| `tests/Carve.ExportViewer.Tests` | The checks on what the viewer made of the exported crate, the last step of suite `scenario-export`. They read the run folder in `CARVE_EXPORT_RUN` and are skipped when it is unset, so `dotnet test Carve.slnx` passes without it |
| `tests/Carve.Scenario.Tiny` | The scenario the debug extension's tests stack on it |
| `tests.json` | The suites and their tiers, which `cairn <kind> <id> verify` and `cairn test run` work from |
| `scripts/verify.ps1` | The old whole run: build warnings-as-errors + all tests + the cube scenario (needs a Vulkan device). The gate no longer runs it |
| `scripts/run-scenario.ps1` | Builds, then runs one scenario on the real `carve`; returns its exit code and run folder |
| `scripts/run-viewer.ps1` | Builds, then runs the viewer on one model; returns its exit code |
| `scripts/run-export-scenario.ps1` | The export scenario, then the viewer on its content folder, then the checks of `tests/Carve.ExportViewer.Tests` (built first unless `-NoBuild`, each step bounded; a skipped check fails it); the run folder is the last line on stdout |
| `scripts/bounded-run.ps1` | Dot-sourced by the run scripts: starts `dotnet` with quoted arguments (a trailing backslash survives) and stops it after its time limit, exit code 124 |
| `docs/` | cairn tracker (tasks, bugs, milestones, design docs, changelog) |
| `artifacts/` | Ignored: captures, review artefacts and raw content, kept across tasks |

## Build, test and verify

```
dotnet build Carve.slnx
dotnet test Carve.slnx
dotnet run --project src/Carve.Server
cairn <kind> <id> verify
```

A change is verified when `cairn <kind> <id> verify` passes and, for anything
that can be seen, the frames from that run have been looked at. Do not run the
scenario again to get them. A queued run's frames come from `cairn test
artifacts <suite>` (`scenario-cube` for the cube); a run on this machine leaves
them in `artifacts/scenarios/<name>/`. Run a scenario by hand only where no suite
covers what changed, and then add a suite. Say which of the two you did, and what
you did not check.

Every test class carries a `Tier` trait, `unit` or `regression`, by what it does:
one that starts `carve` or needs a device is `regression`, and `unit` means about
1 s or less per test. A class with no trait runs as `unit`. A new scenario joins
`Carve.slnx` and gets a `regression` suite in `tests.json`.

## Test scenarios

Everything that looks at the running product goes through **one scenario
harness**, committed in the first milestone and used for every visual check.
Never write a throwaway capture script or a one-off launcher; extend the harness.

Scenario runs are quiet and repeatable, so a run does not disturb the person at
the machine and its frames compare from run to run:

- **Audio muted**: the master volume at 0, as an override never saved to the
  user's settings. Carve makes no sound today; the rule stands for the day it does.
- **A fixed clock.**
- **A fixed seed** for every random choice.
- **A fixed window size** (the render target size, for offscreen renders).
- **A fresh user-data folder** each run, under the run's own output folder.

## Scenarios are extensions, stacked on a debug extension

Carve is a long-lived process with a host of its own, so it exposes an
**extension API** and carries no debug capability of its own. Three layers, each
depending only on the one below:

1. **The product** ships the extension API and loads extensions. It knows nothing
   of any particular extension.
2. **The debug extension** is the one place debug hooks live: the fixed clock and
   seed, the muted-audio override, the fresh user-data folder, scripted input
   (for Carve, scripted MCP tool calls through the real dispatch), state
   inspection, and headless running and frame capture. It is built on the public
   extension API like any other extension.
3. **Scenarios** are extensions stacked on the debug extension. Each scripts one
   situation and what is captured from it.

The scenario harness is the shipped product itself, started with the debug
extension and one scenario stacked on it. **There is no separate scenarios
executable.**

Scenarios are the API's first and most demanding client, on purpose: they need
freedoms the product's author might not think to give an extension (how the host
is composed, how time advances, what input arrives), and building them pressures
the API into shape. When a scenario needs something the API cannot do, widen the
API or the debug extension. **Never special-case the product or reach into its
internals; no internals access is granted to an extension.**

Neither the debug extension nor any scenario ships with the product. A guard test
committed in the first milestone fails if the product references any extension,
the debug extension and the scenarios among them, checked both in what its project
file declares and in what the built product actually references.

The product's own render tool is a product feature, not a debug hook: an agent
asks for a render through MCP like any other tool. The debug hooks are what makes
that render repeatable and what drives it unattended.

### Writing a scenario

One project per scenario, `scenarios/Carve.Scenario.<Name>/Carve.Scenario.<Name>.csproj`,
with `EnableDynamicLoading` set and project references to `Carve.Debug` and
`Carve.Extensibility` only. It holds a public `ICarveExtension` whose `ConfigureHost` does
`context.Services.AddSingleton<IScenario, MyScenario>()`, and the `IScenario`, which may take
`SeededRandom` or `IConfiguration` in its constructor. Exactly one scenario to a run.

`pwsh scripts/run-scenario.ps1 <name>` runs it: the run folder's path is on stdout and the
exit code is `carve`'s. `<name>` is the last part of the assembly name, lower-cased, and names
the folder, `artifacts/scenarios/<name>/<run number>/`, unless `Carve:Debug:RunFolder` says
otherwise; on a queued run the script sets that to `<name>/` under `CAIRN_ARTIFACTS`. That folder holds `frames/`, a fresh `user-data/`, and `run.json`, the log of every
call and result, in which an image is its size and hash.

`ScenarioDriver` is all a scenario has: `ListToolsAsync`, `CallAsync(tool, args)`,
`CaptureAsync(name, args)`, `AssertBytesEqual(what, expected, actual)`, `Fail(message)`, and `RunFolder`, the run folder's path, for a scenario that keeps files in it. A tool
error is a result from `CallAsync` and a failure from `CaptureAsync`. Compare a
`ScenarioFrame`'s `Pixels`, and sample with `Pixel(x, y)`.

### Headless runs

The product has no headless or capture flag of its own: no `--headless`, no
`--capture`, no `if (debug)`. Running without a window is the engine's own
configuration, found in its packaged docs, and the debug extension turns it on as a
default, set below the command line rather than forced, so a person can watch any
scenario in a window by switching it back there. The extension API lets an
extension add configuration and services before the host is built, since the engine
reads such a switch once, at build.

Carve has no window today (D-1), so this binds the day a window arrives, and the
extension API is built ready for it.

## Working rules

- **Ask, don't land.** A rule the spec does not settle, or a change that would
  loosen a check (a tolerance, a test's target, a baseline), goes back to the
  person rather than landing.
- **Keep the evidence.** Review artefacts and raw content are kept in `artifacts/`
  (ignored), where finishing a task does not delete them. Logs go to the temp
  folder (`$env:TEMP`), never into the repo.
- **Stop only your own processes.** An agent stops only processes it started,
  identified by a command line inside its own worktree, never a test host,
  `dotnet` or a runtime machine-wide.

## Engine docs: read these, not the engine source

The version-matched Empyrean docs ship inside each package. Find the cache with
`dotnet nuget locals global-packages --list`, then start at
`<cache>/arcodiant.empyrean/<version>/docs/AGENTS.md` and each package's own
`docs/AGENTS.md`, `docs/recipes/` and `docs/api/index.md`.

## Engine versions and engine needs

The engine version is pinned once, in `Directory.Build.props` (`EmpyreanVersion`,
plus any per-package patch pin with a comment saying why), never as a literal in a
`.csproj`. The feed is the local BaGet at `http://localhost:5555/v3/index.json`
(docker-compose at `D:\repos\baget`).

When Carve needs something the engine is missing, record it as an **engine need**:
add a row to D-1's *Engine needs* table (number, what is missing, priority, the
interim), and cite the number at the workaround site
(`// Interim until engine need N: ...`). Carve is not registered as an engine
request client; registering one is the person's call.

## Work tracking

Run `cairn status` at the start of a session and `cairn next` to pick work. Query
items with `cairn list` / `cairn show`; never grep `docs/` to find open work.


<!-- BEGIN cairn -->
This project's work-item tracker (tasks, bugs, batches, milestones, design
docs, changelog) is managed by the **cairn** skill — invoke it for any
tracker action. It is the single source of truth for the generic
methodology (layout, id reservation, merge-back protocol, linting); this
project's own variations on the defaults, if any, are documented below
this marker.

When work is fanned out to subagents, a member is not `Done` because the
subagent said so — it is `Done` when `cairn task <id> verify` exits clean.
Never mark a member `Done`, and never `finish` a container, on a
self-report alone.

Work landing on `main` needs a current, answered review: `finish` refuses
without one. See the skill's `references/review.md`.

Tests: check your work with `cairn <kind> <id> verify` or `cairn test run
<suite>`, which carry a pass forward and take the machine's test slots
(running here when no runner is up; with no `tests.json`, `verify` runs the
whole hook once and carries it). Never take a GPU lock or kill test hosts
by hand. A carried pass is real; use `--fresh` only with a reason you
state. See the skill's `references/testing.md`.
<!-- END cairn -->
