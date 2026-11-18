---
description: >-
  Server tool checks run in-process as unit tests, and server-regression runs only before landing on main
---
# M-20 — Fast server tests

**Status:** Active

**One-liner:** *Server tool checks run in-process as unit tests, and server-regression runs only before landing on main*

## What this milestone delivers

`server-regression` is the costliest suite by far. Over the four weeks to 2026-11-14, `cairn test report` put it at
about 17,300 slot-seconds a week, more than every other suite together. It ran 47 times a week and was carried
forward only 18% of the time. A run passes 524 cases in about 7 minutes (job J-20261114-054304), against the
`est-seconds: 260` in `tests.json`.

Two causes:

- **Every case starts its own `carve`**: a `dotnet exec`, host build, `initialize` and `tools/list`, and the engine
  thread creating a Vulkan device. The cases run one at a time (`DisableTestParallelization`). Yet 20 of the 28
  regression classes never call `render`; they pay for a process and a device to check a tool's JSON.
- **Nearly every item names it in `**Verify:**`.** cairn's tiers mean a regression suite to run before landing on
  `main`, but 56 items name `server-regression`, so it runs at every task verify and again after every review
  fix. Items name it because a tool's behaviour has no `unit` coverage: every test that calls a tool goes
  through the process.

This milestone fixes the second by fixing the first. Tool definitions and tool behaviour that need no device are
checked in-process at `unit` tier, inside `server-unit`. What stays in `server-regression` is what needs the
process or the GPU: stdio, extension loading, the debug extension, and rendering. Then items stop naming it, and
it runs once per landing on `main`.

Content: none user-facing. This is test infrastructure, so the planning rule that a milestone adds something
reachable from a plain launch does not fit it. Its result is a faster gate and a cheaper slot budget, measured by
`cairn test report` and per-test timings before and after.

**Order.** T-87 first. Then T-88 and T-89 side by side. T-90 last, once `server-unit` covers the tools.

**Ask, don't land.** T-87 must settle how the in-process harness reaches the tools. The test project now
deliberately builds `carve` as a process and never links it (`ReferenceOutputAssembly="false"`: "the tests see
what ships"). Changing that is the person's call. Moving a check down a tier must not loosen it: the same
assertions, on the same inputs.

## Required tasks

- [ ] [T-87](../tasks/T-87-in-process-tool-harness.md) — Call the real MCP tools in the test process with no child process and no engine, decided with the person
- [ ] [T-88](../tasks/T-88-tool-definition-checks-as-unit-tests.md) — Description, worked-example and report-shape checks run on the harness as unit tests
- [ ] [T-89](../tasks/T-89-tool-behaviour-checks-as-unit-tests.md) — Non-rendering tool behaviour checks run on the harness as unit tests; server-regression keeps process, extension and render checks
- [ ] [T-90](../tasks/T-90-server-regression-at-landing-only.md) — A planning rule leaves server-regression out of Verify lines, open items follow it, and the suite estimates are measured again

## Retrospective

**As planned.**

- **Tool checks run in-process.**
  - **T-87** built `ToolHarness`: an MCP client over in-memory streams against a host built by the same `CarveServer.AddTools` that `Program.cs` calls, with no engine.
    - A test moves to it by changing its start line.
    - `render` and `render_glb` refuse at once (20–30 ms) through `EngineStatus.Faulted`.
    - Relative paths resolve under each harness's own `ToolFolder`.
  - **T-88** moved 46 tool-definition cases (descriptions, schemas, worked examples, report shapes) to `unit` classes, unchanged apart from the start line.
  - **T-89** moved the non-rendering tool-behaviour classes, including M-19's `NotPlanarFoldToolTests` and `PieceExportToolTests` as the person ruled. It also moved five UV and material classes the plan had not listed.
  - No assertion, input or tolerance changed.
- **The wiring stays covered through the real process.** `ToolHarnessMatchesProcessTests` (T-87) and `ToolFamiliesThroughProcessTests` (T-89) send the same calls to `carve` and to the harness, one case per tool family, and require identical JSON and byte-equal files.
- **The rule (T-90).** CLAUDE.md's *Build, test and verify* now says that `**Verify:**` names `server-regression` only for stdio, extension loading, the debug extension or rendering.
- **The person's rulings, asked before dispatch** (2026-11-17), on research by an Opus 5.5 agent comparing the three candidates:
  - the in-memory MCP host (option C), with registration moved into a method and the test project referencing `carve.dll`;
  - a base directory for each harness through `ToolPaths`;
  - tools only, with no extension hooks on the harness;
  - T-87 first, and T-88 and T-89 held until M-19 landed, so their moves took in M-19's classes.

  T-88, T-89 and T-90 then ran with no stop.
- **Before and after.**

  | | Before | After |
  |---|---|---|
  | `server-regression` cases | 524 at planning (603 counted by T-89 after M-19) | 223 (first recorded as 228, a count from before T-89's rebase onto T-88; RV-60) |
  | `server-regression` per run | about 7 min (J-20261114-054304) | 338 s in T-90's measurement; 368–493 s in later runs on a loaded runner |
  | `server-regression` `est-seconds` | 260 | 338 |
  | `server-unit` cases | 370 | about 751 |
  | `server-unit` per run | 4 s of test time | 24 s; no case at or over 1 s |
  | `server-unit` `est-seconds` | 12 | 24 |
  | One server start | `carve` process: 370–410 ms to `initialize`, 2.2 s cold | harness: 14–21 ms warm, about 280 ms for the first in a process |

  `est-seconds` went up because the old 260 was already below the real time (the plan's 7 min against 260), and the measured run is now the honest figure.
- **`cairn milestone M-20 verify` (2026-11-17, at 891135f)** passed every check but `review`, which comes after the person's go:
  - 4/4 members done and merged;
  - all 17 suites pass, 15 ran and 2 carried;
  - `server-regression` ran 493 s on Xellotath.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-87 (with research) | Opus 5.5 | 2 (a read-only research agent, then the implementation) |
  | Rebase onto M-19 | Sonnet 5.5 | 1 |
  | T-88 | Sonnet 5.5 | 1 |
  | T-89 | Sonnet 5.5 | 1 |
  | T-90 | Sonnet 5.5 | 1 |

  No rework was traced to the cheaper tier.

**Not as planned.**

- **M-19 changed the same files.** It was Active, waiting on the person's go, with its own changes to the tool registration in `Program.cs` and to a dozen server test classes. M-20 started anyway. Per the person's ruling, T-87 went ahead and T-88/T-89 were held until M-19 landed. A Sonnet agent then rebased the milestone onto `main`:
  - It moved M-19's `render_glb` and `get_clips` into `CarveServer.AddTools`.
  - It gave `render_glb` the `ToolFolder` and the faulted-status refusal.
  - It changed `render_glb` to resolve `IModelGeometryLoader` from `IServiceProvider` at call time rather than as a parameter, since the harness has no engine to provide one.
- **Product changes beyond moving registration**, within the rulings:
  - `EngineStatus.Faulted(message)`, which is public: T-87 chose it over InternalsVisibleTo, which would have opened every internal type to the tests;
  - `ToolFolder`, threaded through save, open, export, `define_material`, `render_glb` and export's `limits`.
- **The harness pins MCP protocol 2025-06-18**, the version `CarveProcess` asks for. A later version adds `resultType` to every result, so the two would no longer match. A client upgrade can change this.
- **The test project still compiles its own copies of product files** (`ModelStore.cs` and others; the refactor pass dropped the linked `ToolPaths.cs` and `GameLimits.cs`) beside its new reference to `carve.dll`, which it takes under `Aliases="carve"` to avoid the clash.
- **T-90 found nothing to change in the items it listed, and missed four.** The 56 items that named `server-regression` at planning were all Done or in progress, and the open ones it listed (T-71, T-73, T-86, B-4, B-5) name no suite in `**Verify:**`. It did not list M-21's T-94, T-103, T-104 and T-108, which were Open and named `server-regression` (RV-59, corrected in T-90's As built, 2026-11-17): T-94, T-103 and T-108 keep it, and T-104 keeps it under the amended rule. The rule binds from the next plan on.
- **T-89's rebase onto T-88 conflicted in seven files**, where T-88 had cut definition tests out of classes T-89 had converted. T-89 resolved them by taking T-88's side and re-applying its conversion in a commit of its own. Splitting ownership by test kind inside shared files cost that rebase, but no stop.
- **`finish` again could not remove its worktree folder** in any member. `branches/task/T-87` to `T-90` are unregistered from git and left on disk. Also left:
  - `C:\Users\andre\AppData\Local\Temp\t87-finish.log`;
  - T-88's move script, `C:\Users\andre\artifacts\carve\M-20\T-88\tools\mv.py`.
- **`verify` warns that `CLAUDE.md` is an input of no suite** (T-87, T-90). This is expected for a docs change.

**Never verified.**

- **The weekly saving.** `cairn test report --since 2d` (the milestone's span, which also holds M-19's landing) still has `server-regression` at about 72,000 slot-seconds a week over 133 runs, 27% carried. The change shows only as later items stop naming it. The next milestone's retrospective should compare.
- **Coverage gaps:**
  - The harness under a Release build.
  - `ToolFolder(null)`, the process's own path, has no test of its own. It is covered by `server-regression` passing.
  - T-88's moves were made by script and read through only where the build or a run flagged something. The moved cases were compared with the originals only in that both pass.
  - `PbrReleaseTests` and `UvExportTests` are `regression` and start no process. T-89 did not examine them for a move.
- **Per-run times were measured on a shared, loaded runner** (Xellotath and Mantorok), so the per-run figures above vary by up to half between runs.
- **No frames were taken or opened.** None were needed.

**Known and left (for the refactor pass).**

- T-88 gave the classes that derived from `FileToolTests` private copies of `StartAsync`, `Ok` and `Refused`, leaving `FileToolTests` to T-89. They can fold back now.
- The linked product files in `Carve.Server.Tests` that the `carve.dll` reference makes redundant.
- **T-86** (`server-regression` times out on a loaded machine) stays open. A smaller suite helps it, but does not settle it.

**After the person's go (2026-11-17 to 2026-11-18).**

- **Rebase (Sonnet 5.5):** `main` had moved to 0.11.3 (CI, README, newly planned M-21 items). The rebase was clean. Under T-90's rule, T-104 dropped `server-regression`; T-94, T-103 and T-108 kept it. RV-59 later restored it to T-104 when the rule was widened.
- **Refactor pass (Sonnet 5.5):** T-88's copies of `StartAsync`, `Ok` and `Refused` folded back into `FileToolTests` in four classes. `ToolPaths.cs` and `GameLimits.cs` are no longer linked copies; the tests take them from carve.dll, where they were already public. The other linked files depend on each other and stay.
- **Review round 1, RV-59:** three Opus 5.5 reviewers (the product code, the test moves, the whole diff) gave 12 findings: 1 Major, 7 Minor, 4 Nits. The test-moves reviewer compared all 549 test methods with `main` mechanically, and none was lost, loosened or wrongly tiered. The Major: two samples the moved unit tests read were inputs of `server-regression` only, so an edit to one could land on a stale carried pass. A Sonnet 5.5 agent answered all 12. Two of them:
  - the CLAUDE.md rule now also names the exported `.glb` read back through the engine's loader;
  - `render_glb` now reports an engine fault before a bad path. That was kept, since it matches `render` and the harness needs it, and it is recorded in T-87.
- **Review round 2, RV-60 (Opus 5.5):** the family end-to-end test's answer did not hold. The fix claimed an `IsError` assertion that was never written, and five calls meant to succeed were refused on both sides, so no `.glb` was ever written. The person chose an Opus 5.5 fix and one more round. That agent made every family succeed, the export write a `.glb` on both sides, and the assertion real, shown failing when an expectation is flipped. It also corrected 228 to 223 (T-89 had counted before its rebase onto T-88).
- **Review round 3, RV-61 (Opus 5.5):** both answers held. One Minor finding was answered with no change: the labelled 4-vs-3 bridge refusal has three reasons, but the loop-count check is asserted by message at unit tier.
- **`cairn milestone M-20 verify` (2026-11-18, at d54ce31):** VERIFY PASS, with `review` current at RV-61. All 17 suites passed, every one carried from an earlier run on the same inputs.
- **Next time:**
  - A fix agent's `fixed` answer needs checking against the code, not its own words. RV-59's family-test answer described an assertion that did not exist.
  - Asking for test counts sent one fix agent into a raw `dotnet test` pass over both suites, beside cairn's own runs. Ask for counts only where cairn reports them.

**Test report** (`cairn test report --since 2d`):

- `server-regression` is still most of the slot time, as expected while the milestone and M-19 ran it at every verify.
- `scenario-orcward` (M-19's) shows 4 fails and 1 job that never ran, outside M-20's work.
- There were no flakes. Nothing new to file beyond T-86, which already exists.

**Next time.**

- **Check a container that edits the same files is not still Active before starting.** Here the clash was seen at the opening table, and holding two members cost nothing. An earlier look would have planned for it.
- **When two members split one set of files by kind of test, run them one after the other.** Side by side, one of them pays a rebase across every shared file.
- **Hand an architecture question to a research agent first.** It made T-87's ruling one question with a recommendation, and the implementation then ran in one agent.
