---
description: >-
  Review of M-3
---
# RV-14 — Review of M-3

**Status:** Answered

**Reviews:** M-3

**Round:** 1

**Previous:** —

**Diff:** f9b97b6666f0af9d058a709121f8b4ecdf1f4688 against origin/main

**Reviewer:** three reviewers, each claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the kernel, file format and tools (claude-opus-5-5)

### A lone surrogate escape in a key escapes CarveFile.Read as an exception (Minor)

`src/Carve.Core/CarveFile.cs:126-128` reads `property.Name` with only `JsonException` guarded, and that guard (lines 100-111) covers the parse alone. The parser accepts a `\uD800` escape in a key, and decoding the name then throws. Rubric 1, T-15 Scope "Refusals, each naming where it is in the file", T-17 "every `CarveFile.Read` error, with its element name".

Input: a `.carve` file holding `{"\ud800": 1}`, or a valid file with an extra key `"\udc00x"`. I ran both against the source in a scratch project outside the worktree: `System.InvalidOperationException: Cannot read incomplete UTF-16 JSON text as string with missing low surrogate.` and `...Invalid surrogate value: '0xDC00'.`

`OpenTool.Run` (`src/Carve.Server/OpenTool.cs:51`) does not catch it, so it leaves the tool. ModelContextProtocol.Core 2.3.0 contains the generic text "An error occurred invoking", so the agent should get that, with no path or element. I did not run this through a live `carve`. The mesh stays unchanged, since `Push` is not reached. No test covers this input.

**Outcome:** fixed

Fixed in 8c9ff6e.

### open's description contradicts itself on undo, and undo's does not know open (Minor)

`src/Carve.Server/OpenTool.cs:17` says "one undo takes you back to the mesh before". Line 19 says "undo cannot go back past the open". In one process the second is false: `SaveOpenToolTests` asserts `"undone":"open"` and the earlier mesh back. In a fresh process, the milestone's "later session", the first is false: `ModelStore.Undo` needs two history entries, so `undo` straight after the first `open` answers "nothing to undo". T-17's As built records that case, but the description an agent reads does not.

`src/Carve.Server/UndoTool.cs:10` still says "the last define_mesh, move or extrude is taken back", though `open` is now a step and `undone` can be `"open"`. Rubric 1; the description is the interface (T-17 Scope, D-1's content).

**Outcome:** fixed

Fixed in 8c9ff6e, 8d1f433.

### T-16's Verify scope leaves out the suites holding its proof (Minor)

`docs/tasks/T-16-export-to-glb.md:17` declares `server-regression, scenario-cube, scenario-crate`. T-16's central tests are `Tier=unit`: `GlbExportTests` (the bit-for-bit engine parse, the material, byte-identical writes), `ToolPathsTests` and `FlatShadingTests`. `tests.json` runs these in `server-unit` and `core-unit`, and `server-regression` filters to `Tier=regression`.

So `cairn task T-16 verify` passes without running them. T-16's own notes say its unit checks were run by hand. `src/Carve.Core/FlatShading.cs` and the path rules have no declared suite that tests them directly. T-17 (`docs/tasks/T-17-save-and-open-tools.md:17`, `server-regression` only) has the same gap for its `ToolPaths.Resolve` change. Rubric 4.

**Outcome:** no change needed: a member's verify requires the unit suites whose inputs it changed, whatever its Verify line names; T-16's verify output shows server-unit and core-unit, and the milestone's verify runs every suite.

### save's cube text is a hand copy that nothing ties to the writer (Minor)

`src/Carve.Server/SaveTool.cs:20-42` holds the cube's file as a literal, and line 49 tells agents it is what the cube "is saved as". The only check is `tests/Carve.Server.Tests/SaveOpenToolTests.cs:189`, `Assert.Contains("\"carve\": 1,", save)`, which would pass with any vertex or face in that text wrong. `CarveFileTests.CubeText` is a second, separate copy. T-17 Scope requires this text, and a format change would leave the description wrong with every test green. Rubric 2.

The literal also does not match the writer as built. It is a raw string, so it takes the checkout's line ends. The index is LF, the working tree is CRLF under `core.autocrlf=true`, and the built `carve.dll` holds `"carve": 1,\r\n`, where `CarveFile.Write` always gives `\n`. The `tools/list` text therefore differs by checkout, which also qualifies the "byte-identical" claim in commit d014eae's message.

**Outcome:** fixed

Fixed in 8c9ff6e.

### open's "a file that cannot be read" branch has no test (Minor)

`src/Carve.Server/OpenTool.cs:45-49` is a listed Scope error (T-17 "a file that cannot be read", repeated in the description at line 27). `A_file_that_cannot_be_opened_changes_nothing` covers bad JSON, a version, a face and a folder, but no locked or denied file. A file held with `FileShare.None`, as `ToolPathsTests` already does for writes, would reach it. Removing the `catch` fails no test. Rubric 2.

**Outcome:** fixed

Fixed in 8c9ff6e.

### A face index of 1.0 is refused in a file but accepted by define_mesh (Nit)

`src/Carve.Core/CarveFile.cs:241` uses `TryGetInt32`. `MeshToolSupport.TryIndex` (`src/Carve.Server/MeshToolSupport.cs:249-254`) accepts `1.0` and `1e0` for `define_mesh`. A file with `"faces": [[0, 1.0, 2]]` gives `faces[0][1]: index is 1.0, not an integer vertex id` (run in the scratch project). T-15 says a file is "held to exactly what `define_mesh` accepts". The refusal only touches hand-edited files, since `Write` never emits `1.0`, but the difference is not recorded among the readings in T-15's As built or the retrospective. Rubric 1.

**Outcome:** no change needed: T-15's refusal list says a face is an array of integers, and Write never emits 1.0. Recorded as a reading in T-15's As built.

### The missing-file listing hides .carve files that start with a dot (Nit)

`src/Carve.Server/OpenTool.cs:76` filters `!n.StartsWith('.')`. The filter is not needed for temp files, which end in `.tmp` and are already excluded by the extension test. `save` accepts such names: `ToolPaths.Resolve` passes `.carve` and `.x.carve` (`.glb` resolved with an empty stem in the scratch run, and `.carve` takes the same path). In a folder holding only `.x.carve`, a missing-file error says "there are no .carve files". T-17 Scope says to list the folder's `.carve` files, and this rule is in neither the spec nor the description. Rubric 3.

**Outcome:** fixed

Fixed in 8c9ff6e.

### The replace does not flush the temp file before the move (Nit)

`src/Carve.Server/ToolPaths.cs:102-104` disposes the stream and renames with no `Flush(flushToDisk: true)`. The doc comment (lines 81-82) says a failure "leaves the old file, or none, and never a partial one". That holds for the I/O failures T-16 names. It is not guaranteed for a power loss or system crash after the rename, when the old file is already gone and the new data may not be on disk. From reading the code; not tested. Rubric 1.

**Outcome:** fixed

Fixed in 8c9ff6e.

### Two tests claim more than they assert (Nit)

`tests/Carve.Server.Tests/SaveOpenToolTests.cs:82-83` comments "the same move lands on the same face", then asserts only that `move` on face 3 succeeded, which any mesh with four faces would pass. The ids are in fact proved by the `get_mesh` equality at line 81, so this check cannot fail on its own.

`Crate` at line 21 is one extrude and one move, not the crate T-17's test bullet names. `TestMeshes.Crate()` exists but is not used here, so the through-the-tool "bit for bit" check never meets the real crate's accumulated floats. Rubric 2.

**Outcome:** fixed

Fixed in 8c9ff6e.

### T-15 and T-16 As built are stale after the refactor commit (Nit)

Commit d014eae removed `GlbExport.Write(PolygonMesh, ...)` and moved the crate to `TestMeshes.Crate`, and updated T-17's file only. `docs/tasks/T-16-export-to-glb.md:93` still says `GlbExport.Write(mesh | triangles, name)`. `docs/tasks/T-15-carve-file-in-the-kernel.md:201` still says `CrateTests` exposes `internal static PolygonMesh Build()`. Rubric 7.

**Outcome:** fixed

Fixed in 8d1f433.

### The other tools still call coordinates unitless (Nit)

`src/Carve.Server/ExportTool.cs:21` says 1 unit = 1 m and the result prints `m`. `src/Carve.Server/DefineMeshTool.cs:23` and `:35`, `GetMeshTool.cs:15` and `MoveTool.cs:17` still say "unitless", against the convention decided in M-3 and D-1. An agent building to a game's scale reads the wrong one first. Rubric 5, Rule.

**Outcome:** fixed

Fixed in 8c9ff6e, 658bc38.

## Findings: the viewer, scenarios and checks (claude-opus-5-5)

### `dotnet test Carve.slnx` now fails on a clean tree (Major)

`tests/Carve.ExportViewer.Tests/ExportViewerTests.cs:188-189`. `Run` is a static field whose initialiser throws when `CARVE_EXPORT_RUN` is unset, and the project is listed in `Carve.slnx`. Only `scripts/run-export-scenario.ps1:46` sets that variable. So all four tests fail with a `TypeInitializationException` under any whole-solution run, whatever the product does.

That run is the `dotnet test Carve.slnx` that `CLAUDE.md` gives under *Build, test and verify*, and `scripts/verify.ps1:17`, which `.cairn.yml` still names as `verify:`. `verify.ps1` stops at that line and never reaches the cube scenario. The per-suite gate is not affected, because no suite in `tests.json` runs the solution. Rubric 1 (what it breaks elsewhere). Read from the code; not run.

**Outcome:** fixed

Fixed in 5b269a0.

### The five native frames in the export scenario have no positive pixel check (Minor)

`scenarios/Carve.Scenario.Export/ExportScenario.cs:39,62,92-95,115-116`. `front` is captured and then only compared with later captures of itself: `front-reopened` and the three `front-after-*` frames. `CrateBuild.CheckCorners` is never called here, and nothing samples a pixel. `CaptureAsync` fails only on a tool error or a missing image (`extensions/Carve.Debug/ScenarioDriver.cs`), so a blank or mirrored render on all five frames passes.

The milestone carries "a positive pixel check on every frame, and one that a mirrored or flipped camera would fail" into every task. Exposure is small: `scenario-crate` checks the same render of the same crate, and `get_mesh` equality covers the geometry here. Rubric 2.

**Outcome:** fixed

Fixed in 5b269a0.

### `run-export-scenario.ps1` runs the checks with `--no-build` even when it is asked to build (Minor)

`scripts/run-export-scenario.ps1:48`. Without `-NoBuild`, steps 1 and 2 build carve, the debug extension, the scenario and the viewer, but nothing builds `tests/Carve.ExportViewer.Tests`, and `dotnet test` is always `--no-build`. After an edit to `ExportViewerTests.cs` or `FramePixels.cs`, a plain `pwsh scripts/run-export-scenario.ps1` runs the old checks and can report a pass for them. On a fresh tree it fails at step 3, after the scenario and viewer have run. The suite itself passes `-NoBuild` after a solution build, so only a run by hand is affected. Rubric 1.

**Outcome:** fixed

Fixed in 5b269a0.

### The third step of the export suite has no time limit (Minor)

`scripts/run-export-scenario.ps1:48`. The scenario is bounded at 300 s and the viewer at 120 s, both through `Invoke-BoundedDotnet`, but `dotnet test` runs bare. `scenario-export` in `tests.json` declares no `timeout-seconds`, and `cairn/testcmd.py:317` passes that value straight through. A hung test host therefore holds the GPU slot with nothing to stop it, and never gives the 124 the other two steps do. Rubric 1.

**Outcome:** fixed

Fixed in 5b269a0.

### A second run into a named run folder fails at `save` (Minor)

`scenarios/Carve.Scenario.Export/ExportScenario.cs:44`, with `extensions/Carve.Debug/RunFolder.cs:37`. `RunFolder.Create` clears only `frames/` and `user-data/`. A named folder is meant to be reusable, but `work/`, `game/`, `viewer/` and `export-result.txt` stay. On a second `pwsh scripts/run-export-scenario.ps1 -CarveArguments '--Carve:Debug:RunFolder=artifacts/x'`, `work/crate.carve` already exists, `save` says `replaced`, and `ExpectNot` fails the run on a sound product. The same holds for a queued run if `CAIRN_ARTIFACTS` were ever reused; I did not establish whether it is fresh per job. Rubric 1.

**Outcome:** fixed

Fixed in 5b269a0.

### Nothing tests the default view (Minor)

`spikes/Carve.ModelViewer/ViewerOptions.cs:16,73`. T-18's Scope says that with no `--view` the default is `front=1.6,1.2,2.2`. Every successful run in `tests/Carve.ModelViewer.Tests/ViewerCubeTests.cs:26-28` passes a view explicitly. The only runs with none (`:217`, `:230`) fail at load, before any framing. Changing `DefaultView` to another direction or name fails no test. Rubric 2.

**Outcome:** fixed

Fixed in 5b269a0.

### A trailing backslash on a folder argument breaks the viewer's command line (Nit)

`scripts/bounded-run.ps1:14`. Each argument is wrapped as `"<value>"`, so a value ending in `\` escapes its closing quote. `run-viewer.ps1 game\Content\ models/crate.glb out\` is what PowerShell tab completion gives. The same quoting through `Start-Process` gave `['--content', 'game\\Content" --model m.glb --out out"', '--view', 'front=1,1,1']`. The viewer then stops with a usage error that does not point at the cause. It fails loudly, so nothing passes wrongly. Rubric 1.

**Outcome:** fixed

Fixed in 5b269a0.

### One of the five "+Z panel" samples is on the frame (Nit)

`tests/Carve.ExportViewer.Tests/ExportViewerTests.cs:73-74`. The comment says "five points across the +Z panel, which is one flat face". Casting a ray through (285, 300) from T-19's saved eye and `crate.carve` lands on face 11, the +Z frame's right band (x 0.88 to 1, z = 1). The other four land on panel face 1 (z = 0.92). The two share a shade, so the check still passes, but `FrontPlusZPanel` and `FlatPlusZPanel` do not sample what they say. Rubric 1.

**Outcome:** fixed

Fixed in 5b269a0.

### The guard's summary leaves out `spikes/` (Nit)

`tests/Carve.Server.Tests/ProductReferenceGuard.cs:128`. The summary still says "extensions/ and scenarios/", and the code on line 130 now includes `spikes`. T-18 updated the parameter comment on `FindInAssembly` but not this one. Rubric 7.

**Outcome:** fixed

Fixed in 8c9ff6e, 5b269a0.

### `The_viewer_references_no_Carve_project` looks only at `ProjectReference` (Nit)

`tests/Carve.ModelViewer.Tests/ViewerCubeTests.cs:259`. T-18 says "No Carve code". A linked `<Compile Include="../../src/Carve.Core/..." />`, the idiom `tests/Carve.Server.Tests/Carve.Server.Tests.csproj` already uses, would put Carve code in the viewer and pass this test. Rubric 2.

**Outcome:** fixed

Fixed in 5b269a0.

### An unused local in `CrateScenario` after the move (Nit)

`scenarios/Carve.Scenario.Crate/CrateScenario.cs:68`. `var crate = await CrateBuild.BuildAsync(driver);` is never read, since its closed, warnings and bounds checks moved into `CrateBuild`. Rubric 3.

**Outcome:** fixed

Fixed in 5b269a0.

## Findings: the whole diff (claude-opus-5-5)

### `dotnet test Carve.slnx` can no longer pass (Major)

Rubric 6 (tests placed), and 1 (breaks something elsewhere). T-19 added `tests/Carve.ExportViewer.Tests` to the solution (`Carve.slnx:11`) as an ordinary xunit project. `ExportViewerTests.Run` (`tests/Carve.ExportViewer.Tests/ExportViewerTests.cs:188-189`) is a static field that throws `InvalidOperationException` when `CARVE_EXPORT_RUN` is unset, and only `scripts/run-export-scenario.ps1:46` sets that variable. So on a healthy tree the project's tests fail with a type-initialisation error under the solution-wide run: `dotnet test Carve.slnx`, the command in `CLAUDE.md:79`, and `scripts/verify.ps1:17`, which `CLAUDE.md`'s Layout describes as "all tests". The `cairn` gate is unaffected, because `scenario-export` sets the variable and no suite runs the solution. The tests should skip when the variable is unset, or the project should stay out of what the solution-wide run picks up. This comes from reading the code; I did not run it.

**Outcome:** fixed

Fixed in 5b269a0.

### `open` says one undo takes you back, but not in the worked example's own session (Minor)

Rubric 1. `src/Carve.Server/OpenTool.cs:17` says "one undo takes you back to the mesh before". `README.md:39-40`, on step 3 "In a later session, `open`", says "one `undo` takes it back". The first mesh of a process cannot be undone: `ModelStore.Undo` returns null when `_history.Count < 2` (`src/Carve.Server/ModelStore.cs:66`), and T-17's As built says so (`docs/tasks/T-17-save-and-open-tools.md:111`). The later-session `open` is exactly that case, so following the README and calling `undo` gets `undo: nothing to undo`. The description and the README state the opposite of what As built records for the sequence they teach.

**Outcome:** fixed

Fixed in 8c9ff6e, 8d1f433.

### The tools T-17 did not touch still describe a world without `open` (Minor)

Rubric 1, and the planning rule that tool descriptions come with their mechanic. `src/Carve.Server/UndoTool.cs:10` lists what undo takes back as "the last define_mesh, move or extrude", but `open` is a step and `undone` can be `"open"`. D-1's *Undo and history* (`docs/design/D-1-carve-concept.md:155`) names `define_mesh` as the step and not `open`. With no mesh, every tool points only at `define_mesh`: `NoMesh` (`src/Carve.Server/MeshToolSupport.cs:15`), `MoveTool.cs:26`, `ExtrudeTool.cs:26`, `GetMeshTool.cs:16`, `RenderTool.cs:113`, and the no-mesh clauses at `SaveTool.cs:53` and `ExportTool.cs:25`. An agent starting a later session and calling `get_mesh` or `render` first is told to define a mesh, not to open its file, on the path this milestone adds.

**Outcome:** fixed

Fixed in 8c9ff6e, 8d1f433.

### `run-export-scenario.ps1` never builds its test step (Minor)

Rubric 1. The script has a `-NoBuild` switch, and without it `run-scenario.ps1` and `run-viewer.ps1` build. Step 3 always runs `dotnet test $tests -c $Configuration --no-build` (`scripts/run-export-scenario.ps1:48`), and nothing has built `Carve.ExportViewer.Tests`. The suite is safe only because it passes `-NoBuild` after a solution build. Run by hand on a fresh tree or another `-Configuration`, the last step fails with nothing to run. After an edit it runs the old checks against the new frames.

**Outcome:** fixed

Fixed in 5b269a0.

### `### As built` notes went stale in the refactor, which no tracker item records (Minor)

Rubric 7. `d014eae` fixed names in T-17 and T-22 only.

- `docs/tasks/T-15-carve-file-in-the-kernel.md:201` still says `CrateTests` exposes `internal static PolygonMesh Build()`; that is now `TestMeshes.Crate()`.
- `docs/tasks/T-16-export-to-glb.md:93` still gives `GlbExport.Write(mesh | triangles, name)`; the mesh overload is gone.
- `docs/tasks/T-18-model-viewer-spike.md:94` lists the test files without `FramePixels.cs`, which now holds what was in `ViewerFrame.cs` and is linked into `Carve.ExportViewer.Tests`.
- `docs/tasks/T-17-save-and-open-tools.md:97` does not name `FileToolTests.cs`, where its test helpers now live.

`scripts/bounded-run.ps1`, and the pass itself, appear in no As built and not in the milestone's retrospective, which was written before it (`7a2060f`). The commit message is the only record, and its "No behaviour changes" claim is backed only by a `tools/list` comparison. I read the diff of the tools, `CarveFile`, the scripts and the tests and found no behaviour change; the removed `Assert.Equal(2, level.Length)` moved to `TestMeshes.cs:76`.

**Outcome:** fixed

Fixed in 8d1f433.

### `CLAUDE.md` Layout leaves out what M-3 added (Minor)

Rubric 7. The table gives each script and test project its own row, but `scripts/bounded-run.ps1`, `scripts/run-export-scenario.ps1` and `tests/Carve.ExportViewer.Tests` have none. The last two appear only inside the `scenarios/Carve.Scenario.Export` row, and `bounded-run.ps1` appears nowhere in `CLAUDE.md`. `CLAUDE.md:163` also says "`ScenarioDriver` is all a scenario has" and lists its members without `RunFolder`, which `ExportScenario` depends on for all its paths (`scenarios/Carve.Scenario.Export/ExportScenario.cs:31-32`).

**Outcome:** fixed

Fixed in 8d1f433.

### The missing material name is not recorded as an engine need (Minor)

Rubric 5, and `CLAUDE.md`'s *Engine versions and engine needs*. The agreed viewer sketch has `view.json` report "material 'carve'" (`docs/milestones/M-3-first-model-in-a-game.md:79`). It cannot, because the engine's `Material` has no name to read (`docs/tasks/T-18-model-viewer-spike.md`, As built; milestone line 143). `spikes/Carve.ModelViewer/ViewerSystem.cs:191-201` writes the material without one. That is something Carve needed and the engine lacks, so the rule asks for a row in D-1's table (`docs/design/D-1-carve-concept.md:171`, still two rows) and a citation at the site. Neither exists. The gap is mentioned, but not where the rule puts it.

**Outcome:** fixed

Fixed in 8d1f433.

### Comments the later members left behind (Nit)

Rubric 7. `tests/Carve.Server.Tests/ProductReferenceGuard.cs:128` still says "under extensions/ and scenarios/" above a list that now includes `spikes`; T-18 updated the other comment at line 32 and missed this one. `Directory.Build.props:16` says SharpGLTF is for "Carve.Server and its tests only", and `docs/tasks/T-16-export-to-glb.md:97` says the same of `Carve.Server` and `Carve.Server.Tests`. T-18, on its spec, also references it from `tests/Carve.ModelViewer.Tests`.

**Outcome:** fixed

Fixed in 5b269a0, 8d1f433.

### Two literals that claim a source no test ties them to (Nit)

Rubric 2. `SaveTool.CubeFile` (`src/Carve.Server/SaveTool.cs:19-20`) is documented as "What save writes for the unit cube" and is a copy of `CarveFileTests.CubeText`, but the only check on it is `Assert.Contains("\"carve\": 1,", save)` (`tests/Carve.Server.Tests/SaveOpenToolTests.cs:189`). `GlbExport.BaseColor` (`src/Carve.Server/GlbExport.cs:22`) is documented as "RenderSystem's" colour but repeats the private `RenderSystem.Surface` (`src/Carve.Server/RenderSystem.cs:25`). Either copy can drift from its source with every test still passing.

**Outcome:** fixed

Fixed in 8c9ff6e.

### A project-file check sits in a GPU suite (Nit)

Rubric 6. `The_viewer_references_no_Carve_project` (`tests/Carve.ModelViewer.Tests/ViewerCubeTests.cs:255-260`) only reads a `.csproj`, but its class is `regression`, so it runs only in `viewer-cube`, which needs Vulkan. It fits beside the guard tests in `server-unit`, which already takes `spikes/**/*.csproj` as an input.

**Outcome:** fixed

Fixed in 5b269a0.

## Summary

Three reviewers, in parallel, split by risk: the kernel, file format and tools; the viewer, scenarios and checks; and one pass over the whole diff for what falls between them. The diff is about 50 files, after a rebase onto origin/main and a refactor pass (d014eae). Nobody ran a GPU suite for this review or opened an image. There are two reports of one Major (a solution-wide test run fails); the rest are Minor or Nit.


### The fixes proved (after 78d73ed)

On a clean tree, `dotnet test Carve.slnx` with `CARVE_EXPORT_RUN` unset passed (Core 183, ModelViewer 15, Server 302, ExportViewer 4 skipped, 0 failed), and `pwsh scripts/verify.ps1` exited 0 (the same totals, 0 warnings, the cube scenario run). Each check below was then made to fail on a real break, one at a time, and the break reverted; the tree ended clean. No check had to be changed.

| Check | The break | Result |
|---|---|---|
| `CrateBuild.CheckFront` on the export frames | the product's front camera mirrored in X (`ViewFraming`) | the export scenario failed: `front: +Z (53) should be at least 10 brighter than +X (187)` |
| `FlatPlusZPanel` samples | sample x = 110 moved to x = 90, on the recess wall | `the +Z panel is not one flat shade: 170, ...` |
| the surrogate refusal | the try/catch in `CarveFile` replaced by `property.Name` | 3 Core theory cases and the `open` server test failed |
| `open`'s locked-file test | the `catch` around `File.ReadAllText` removed | `A_file_that_cannot_be_opened_changes_nothing` failed (`An error occurred invoking 'open'`) |
| the dot-file listing test | `&& !n.StartsWith('.')` put back in the listing | `A_missing_file_names_the_path_and_lists_the_carve_files_beside_it` failed |
| the cube-text test | one digit of `SaveTool.CubeFile` changed | `The_descriptions_worked_example_runs_as_written_...` failed |
| the default-view test | `DefaultView` set to the minus-x direction | `With_no_view_given_the_default_is_the_front_view` failed |
| the backslash viewer test | the backslash doubling removed from `bounded-run.ps1` | `A_folder_argument_ending_in_a_backslash_reaches_the_viewer_whole` failed |
| the second-run clearing | the clearing loop removed from `ExportScenario`; two runs into one named folder | the first run passed, the second failed: `did not expect 'replaced'`; with the clearing both pass |
| the step-3 timeout | `-ChecksTimeoutSeconds 1` | exit 124, `the checks are still running after 1 s; stopped` |
| skipped-check detection | `$env:CARVE_EXPORT_RUN = $folder` left out of step 3 | exit 1, `no check passed; they may have been skipped` (all four skipped); with one test marked skipped, exit 1, `a check was skipped` |
| the project-file check | a `Compile Include` of `src/Carve.Core/Mesh.cs` linked into `spikes/Carve.ModelViewer` | `The_viewer_references_no_Carve_code` failed |

The five `FrontPlusZPanel` samples (x = 110, 150, 190, 230, 250 at y = 300) were cast as rays from the saved view's eye through the pixel: each passes the +Z frame's opening (x 0.26 to 0.84, y 0.35 to 0.53 at z = 1) and meets the panel at z = 0.92 (x 0.17 to 0.79), so all five are on the panel. Reading the row, the panel starts at x = 101, and the frame faces beyond x = 260 are not sampled. Not checked: the bar-top, recess-wall and minus-x samples by ray, and an up-flipped camera (only the mirrored one).
