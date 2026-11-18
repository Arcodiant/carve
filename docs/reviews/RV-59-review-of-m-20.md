---
description: >-
  Review of M-20
---
# RV-59 — Review of M-20

**Status:** Answered

**Reviews:** M-20

**Round:** 1

**Previous:** —

**Diff:** e4697c23e78f2251934c75637533d89c619db598 against 7e13279

**Reviewer:** claude-opus-5-5 (product code), claude-opus-5-5 (test moves), claude-opus-5-5 (whole diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the product code (claude-opus-5-5)

I checked the following and found nothing wrong with them. Program.cs registers the same services and tools as before, and at the same point: after the extension hooks, the engine and the transport check. So last-wins and first-wins substitution behave as they did. An extension still cannot see or replace `ModelStore`/`EngineStatus`/`RenderQueue` from `ConfigureHost`, exactly as before. The new `ToolFolder` is registered after the hooks too, so its registration wins over any an extension makes. With `ToolFolder(null)`, every path call resolves the same way as before: `ToolPaths.Resolve` falls back to `Environment.CurrentDirectory` when the tool is called, and `LimitsSource.Find`'s `Rooted` returns its input unchanged. So the shipped carve's limits lookup is unchanged. Nothing in the product calls `EngineStatus.Faulted`. `EngineStatus` is re-registered as a plain singleton after the hooks, so a faulted status cannot reach the shipped path. In the harness, test parallelization is off (`CarveProcess.cs:5`), each test builds its own harness, and nothing touches `Environment.CurrentDirectory`. The pinned protocol version, 2025-06-18, matches what `CarveProcess` sends (`CarveProcess.cs:120`), so it is consistent.

### render_glb now reports an engine fault ahead of path and file errors

**Where:** `src/Carve.Server/RenderGlbTool.cs:85-86`

**What:** Before, render_glb resolved the path, read the file and ran `GlbScene.Prepare`, and only looked at the engine when it posted to the queue (`WhenAny(rendering, WhenStopped)`). Now it waits on `WhenSettled` first and returns any fault before that.

**Failure scenario:** On a machine whose engine faults, for example one with no usable GPU, `render_glb(path:"missing.glb")` used to answer `render_glb: no file at …`. A wrong clip name used to list the file's clips. Both now answer with the engine fault. Before the engine is ready, even a bad path now waits for startup before it is reported. This matches `render` (`RenderTool.cs:359`), so it is defensible, but it is a visible change to the shipped carve that T-87 does not record.

**Severity:** Minor

**Outcome:** no change needed: it matches `render` (`RenderTool.cs:359`), and on the harness the fault must come first, since the loader is not there. The behaviour change is recorded in T-87's `### As built`.

### IModelGeometryLoader is fetched from the service provider at call time

**Where:** `src/Carve.Server/RenderGlbTool.cs:58,100`

**What:** The loader is no longer a parameter. `services.GetRequiredService<IModelGeometryLoader>()` fetches it from `IServiceProvider` inside the tool, so the dependency no longer shows in the method signature.

**Failure scenario:** If an engine substitution, or a future host, leaves the loader unregistered, the tool fails only when it reaches that line. The SDK then returns a generic `InvalidOperationException` tool error rather than failing at startup. In the harness this line is unreachable only because the fault check comes first, so the two lines silently depend on their order. The loader's singleton lifetime is unaffected. A comment, or `GetService` with an explicit error message, would make the dependency visible.

**Severity:** Nit

**Outcome:** fixed

**Fix:** `RenderGlbTool.cs` uses `GetService<IModelGeometryLoader>()` and returns a tool error naming the missing loader, with a comment that the engine-fault check above must come first on an engine-less host.

### `Rooted` and `ToolPaths.Resolve` can disagree on rooted-but-relative limits paths

**Where:** `src/Carve.Server/GameLimits.cs:172,185,190`

**What:** With a base directory set, `Path.Combine(base, @"\foo")` returns `\foo`, and `Path.Combine(base, "C:foo")` returns `C:foo`. `File.Exists` and `Directory.Exists` then resolve those against the process's current drive and directory. `ToolPaths.Resolve` (`ToolPaths.cs:48`) instead uses `GetFullPath(path, base)`, which takes the base directory's drive.

**Failure scenario:** When the base directory is on another drive, the ".json or folder?" checks look at one file and the read resolves another. This only happens with a base directory set, which is only the harness, so the shipped carve is unaffected. Using `Path.GetFullPath(relative, baseDirectory)` in `Rooted` would make the two agree.

**Severity:** Nit

**Outcome:** fixed

**Fix:** `Rooted` in `GameLimits.cs` uses `Path.GetFullPath(relative, baseDirectory)` when a base directory is set; a null base is unchanged.

### Harness leaks a started host when a test helper throws

**Where:** `tests/Carve.Server.Tests/ToolHarness.cs:57-62`, together with helpers such as `CheckToolTests.cs:12-14` and `FileToolTests.cs:50-53`

**What:** The helpers create the harness, then call `InitializeAsync`, `ListToolsAsync` and sometimes `define_mesh` before handing it to the test's `using`.

**Failure scenario:** If any of those calls throws (a timeout, or a failed assert in `FileToolTests`), nothing disposes the harness. Its host is left started, along with its pipes and server session, until the process ends. The cost is noise and leaked resources when a test fails, not wrong results. Sync `Dispose` blocking on `DisposeAsync` is safe under xunit, which has no synchronization context.

**Severity:** Minor

**Outcome:** fixed

**Fix:** every helper that starts a `ToolHarness` and then calls into it (28 `StartAsync`-style helpers, e.g. `CheckToolTests`, `FileToolTests.StartAsync`, `NamedMeshTests`, `SkinnedExportToolTests.StartChainAsync`) now disposes the harness on an exception and rethrows.

## Findings: the whole diff (claude-opus-5-5)

Answers to the brief's questions: a change to a product tool file does trigger `server-unit`, because `dotnet-closure` follows every ProjectReference and the test csproj references `src/Carve.Server`. The `coverage` check has never required `server-regression` for stdio, extension or render changes, because the same closure is also an input of `server-unit`. So the new rule rests on the planner's judgement plus the landing run; no check enforces it. That is acceptable, but the record should say so. `ProductReferenceGuard` and the no-extension guard still check carve's own output (`CarveProcess.CarveDll`), so the new reference does not weaken them. The changed Verify lines are correct: T-104 drops `server-regression`, and T-94, T-103 and T-108 keep it because they change rendering. Est-seconds 24 and 338 match T-90's measurement.

### server-unit does not list samples its moved tests now read

- **Where:** tests.json:34-55 (`server-unit` inputs); tests/Carve.Server.Tests/CheckToolTests.cs:59 and SubdivideToolTests.cs:125,172.
- **Problem:** T-89 moved `CheckToolTests` (reads `samples/leaky-shed.carve`) and `SubdivideToolTests` (reads `samples/gardener.carve`) from `regression` to `unit`. Those two samples are still inputs of `server-regression` only.
- **Consequence:**
  - Editing either sample leaves `server-unit`'s fingerprint unchanged, so the suite carries a stale pass. Carried passes count at every gate, including landing on main, so a sample edit that breaks these tests can land without them running.
  - An edit to `gardener.carve` also makes `coverage` demand `server-regression` in Verify, which contradicts the new rule.
- **Fix:** add both samples to `server-unit`'s inputs.
- **Severity:** Major

**Outcome:** fixed

**Fix:** `tests.json` adds `samples/leaky-shed.carve` and `samples/gardener.carve` to `server-unit`'s inputs. The other unit-tier classes read only `Fixtures/*.png` (already an input), the closure's own files and `README.md`/csprojs, so no other sample needed adding.

### The rule's list of what only server-regression covers is incomplete

- **Where:** CLAUDE.md:133-134; docs/tasks/T-104-morph-targets-in-the-glb.md:17.
- **Problem:** The rule says `server-regression` alone covers "the stdio transport, extension loading, the debug extension or rendering". It also alone holds `UvExportTests`, regression tests with no process that read the exported GLB back through `IModelGeometryLoader` (the retrospective's line 126 says this was not examined), and `PbrReleaseTests`.
- **Consequence:** T-104 changes `GlbExport.cs` vertex output and writes loader read-back tests. `UvExportTests` is the test of exactly that, and the precedent T-104 would likely copy, which makes its new tests `regression` too. With `**Verify:** server-unit`, none of them runs until landing.
- **Fix:** either name "export read back through the engine loader" in the rule, or move `UvExportTests` and `PbrReleaseTests` to `unit`.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** CLAUDE.md's rule also names "the exported `.glb` read back through the engine's loader" (`UvExportTests`, `PbrReleaseTests`), and T-104's `**Verify:**` is `server-unit, server-regression` again. Neither class moved.

### Layout row misdescribes the regression tier

- **Where:** CLAUDE.md:83
- **Problem:** The row says the `regression` tests "start the real `carve`" and cover rendering "(incl. the no-extension guard)". Both parts are wrong:
  - `ProductReferencesNoExtensionTests` is `[Trait("Tier","unit")]` (line 9) and was `unit` on main too.
  - `UvExportTests` and `PbrReleaseTests` are `regression` and start no process.
- **Consequence:** A reader looking for the guard's suite, or deciding Verify from this row, is misled.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** CLAUDE.md's `tests/Carve.Server.Tests` row now says what is unit (tool behaviour and definitions on `ToolHarness`, the no-extension guard) and what is regression (stdio, extension loading, the debug extension, rendering, the loader read-back of exports, the process-vs-harness tests).

### T-90's survey of open items missed four items; the record still says it found none

- **Where:** docs/tasks/T-90-server-regression-at-landing-only.md:43; docs/milestones/M-20-fast-server-tests.md:112
- **Problem:** At T-90's commit (50e430c), M-21's plan (40d3794) was already in history. T-94, T-103, T-104 and T-108 were Open and named `server-regression`. T-90 listed only T-71, T-73, T-86, B-4 and B-5, and the retrospective says "found nothing to change … the rule binds from the next plan on". Commit 3282eab later fixed T-104, but neither document was updated.
- **Consequence:** The as-built record and the retrospective are false. They also hide that the open-items query T-90 used misses milestone members, which matters for the next sweep like this.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** T-90's `### As built` has a dated (2026-11-17) correction naming T-94, T-103, T-104 and T-108, and the retrospective's T-90 bullet and its linked-copies bullet (`ToolPaths.cs`, `GameLimits.cs` removed) are corrected. The cause could not be told from the record: `cairn list task --status Open` returns all four today.

### csproj comment and FileToolTests summary no longer true after the refactor

- **Where:** tests/Carve.Server.Tests/Carve.Server.Tests.csproj:22-27; FileToolTests.cs:44
- **Problem:**
  - The csproj comment says carve is referenced "for one reason: the unit harness" and "Everything else still starts carve as a real process". The refactor pass now has `GameLimitsTests`, `ToolPathsTests`, `MaterialExportTests`, `PieceExportTests` and `SkinnedExportTests` use `carve::` types directly.
  - The retrospective (line ~108) still lists `ToolPaths.cs` among the linked copies, though the refactor removed it.
  - `FileToolTests` says it "calls a file tool through the real server", but `StartAsync` now uses `ToolHarness`.
- **Consequence:** Misleading comments in the exact place a future change to the "tests see what ships" policy would start.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** the csproj comment and the `FileToolTests` summaries now say the harness is the unit path, a few unit classes use carve's types directly, and the regression tests start the process.

No problems found in how `render_glb` or `FileToolTests` changed from both sides. Regression subclasses use `StartProcessAsync`, and `render_glb` is registered in `AddTools`.

## Findings: the test moves (claude-opus-5-5)

Diff: `git diff origin/main...HEAD -- tests/Carve.Server.Tests` (the merge base is origin/main, 7e13279).

**Checked mechanically.** Every `[Fact]`/`[Theory]` method was extracted from both trees, with its attribute block and its brace-matched body.

- **Counts:** origin/main has 549 methods and 877 InlineData-expanded cases. HEAD has 556 methods and 886 cases.
- **Lost or renamed:** none.
- **New:** 7 methods (9 cases), all of them harness and end-to-end tests: the 5 in `ToolHarnessTests`, `ListsTheSameToolsAndAnswersTheSame` and `Every_tool_family_...`.
- **Attributes:** with `Tier` traits removed, every attribute block is identical. That covers every `[InlineData]` row, `Skip` and Fact/Theory.
- **Bodies:** after collapsing whitespace and mapping `CarveProcess([], workingDirectory: Root)`→`ToolHarness(...)` and `StartProcessAsync`→`StartAsync`, 536 of 549 are identical. The other 13 differ only in their start line (`new CarveProcess([])`→`new ToolHarness()`, or `StartWithCrateAndLidAsync`→`...OnHarnessAsync`). No assertion, input, tolerance or timeout changed.
- **Methods that changed class:** 53. Of these, 44 methods (46 cases) went to `*DefinitionTests`, which matches T-88. The other 9 went to the three split classes. T-89's 973→974 matches the one added end-to-end test.
- **Tier traits:** every test class's `Tier` at HEAD was set against whether it starts a process, calls render/render_glb/a look, or uses the cwd.
  - No `unit` class starts a process.
  - The only "render" calls in unit classes are an unknown-argument refusal and description reads. Render checks unknown arguments before the engine status, so that row asserts the same thing on both.
  - No `regression` class runs on the harness: each still renders, loads extensions or uses stdio.
  - No regression subclass of `FileToolTests` calls the harness `StartAsync`.
- **Weaker on the harness:** every harness test made without a folder (`new ToolHarness()`) touches no files. Folder tests assert paths under `Root`, which is the same directory the process used as its cwd.
  - The server's only mutable static is the `ToolPaths._writes` counter, and every harness builds its own host and singletons, so tests that used to get a fresh process don't now share state.
  - A test that relied on a process-only member would not compile on `ToolHarness`.
- **`IToolCaller`:** minimal (only `CallToolAsync`) and used correctly by `BuildChainAsync` and `LookAsync`.
- **Registration drift:** `ToolHarnessMatchesProcessTests` compares the raw `tools/list` output of the process and the harness. That would catch a tool that `Program.cs` registers outside `AddTools`. A tool missing from `AddTools` is missing from both, so only the hard `Assert.Equal(36, ...)` in `ToolHarnessTests` catches that, which is adequate.

### The tool-family end-to-end test mostly compares refusals for edit, cut and select

- **Where:** `ToolFamiliesThroughProcessTests.cs:15-22`, `Every_tool_family_answers_through_the_process_as_it_does_on_the_harness`.
- **What is wrong:** the test checks only that the process and the harness return the same JSON. It never checks that a call succeeded, and several of its calls are refused on both sides:
  - `extrude {"by":0.25}`: `extrude` takes `distance`, so `by` is an unknown argument (`ExtrudeTool.cs:62`).
  - `select {"kind":"faces","faces":[3]}`: `select` takes only `kind`, `where` and `mesh` (`SelectTool.cs:101`).
  - `bridge {"loops":[[0,1,2,3],[4,5,6]]}`: the exact case `MergeBridgeToolDefinitionTests:119` asserts is refused ("4 and 3").
  - `merge_faces {"faces":[0,1]}`: the same call `MergeBridgeToolDefinitionTests:87` asserts is refused ("must be connected").
- **Consequence:** for these tools the "one end-to-end case per family" only shows that argument validation runs the same through the process. Their real edit paths never run through `Program.cs`, so a process-only difference there would not be caught. Asserting `!IsError` on the calls meant to succeed would expose this.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** in round 1, `ToolFamiliesThroughProcessTests` gave the edit, bridge and select families valid arguments (`extrude` `distance`, `select` `kind`/`where`, a bridge between two equal loops) and marked two refusal cases `Refused: true`. It did not assert `IsError`: the `Refused` flag was read and never used, and `merge_faces {"faces":[7,1]}`, `define_clip` (a clip on a bone the skeleton lacks), `set_weights` (neither vertices nor group) and both `export` calls were still refused on both sides. Round 2 (RV-60) completed it: the test now asserts `IsError` equals `Refused` on both sides for every call, and those calls succeed (see RV-60's first finding).

### Written files are compared one way only

- **Where:** `ToolFamiliesThroughProcessTests.cs:44-45`.
- **What is wrong:** the loop lists only the files under `harnessRoot`.
- **Consequence:** a file that only the process writes, for example one that a cwd-relative path sends somewhere else, is never noticed. Comparing both directory listings would close this.
- **Severity:** Minor

**Outcome:** fixed

**Fix:** the test lists both directories (the same set of relative paths) and then compares bytes.

### A third copy of the crate-and-lid start, reusing the process helper's name

- **Where:** `NamedMeshDefinitionTests.cs:39`, private `StartWithCrateAndLidAsync`.
- **What is wrong:** it returns a `ToolHarness`, which makes it a copy of `NamedMeshTests.StartWithCrateAndLidOnHarnessAsync`. It has the same name as `NamedMeshTests.StartWithCrateAndLidAsync`, which starts the process.
- **Consequence:** the moved test bodies read as unchanged only because the name now resolves to a different helper. A reader cannot tell from the call site which server the test uses. `FileToolTests.StartAsync`/`StartProcessAsync` are also near-duplicates, because `IToolCaller` lacks `InitializeAsync`/`ListToolsAsync`.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** `NamedMeshDefinitionTests` drops its private copy and uses `NamedMeshTests.StartWithCrateAndLidOnHarnessAsync`. The `FileToolTests.StartAsync`/`StartProcessAsync` near-duplication stays as it is (skipped), because `IToolCaller` has no `InitializeAsync`/`ListToolsAsync` to share a body through, and merging them would widen it for two helpers.

Apart from these, no lost, duplicated, renamed, loosened or wrongly tiered test was found. The moves are faithful.

## Summary

Three reviewers in one round, each on Opus 5.5, all stronger than or equal to the implementers (Opus 5.5 for T-87, Sonnet 5.5 for the rest). The diff (about 75 files) is mostly test moves with a small product change, so it was split by concern rather than by member: the product code in `src/` with the harness that drives it; the faithfulness of the moved tests, checked mechanically method by method against origin/main; and one pass over the whole diff (tests.json, CLAUDE.md, the csproj and guards, item Verify lines, interfaces changed from two sides).

Twelve findings: one Major (two samples the moved unit tests read are not `server-unit` inputs, so a sample edit can land on a stale carried pass), five Minor, six Nits. The test-moves reviewer found no test lost, renamed, loosened or wrongly tiered: 549 methods on main, all present at HEAD with identical attributes and bodies but for the start line.
