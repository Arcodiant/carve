---
description: >-
  Third review of M-3
---
# RV-16 — Third review of M-3

**Status:** Open

**Reviews:** M-3

**Round:** 3

**Previous:** RV-15

**Diff:** 36bf71f78ecd697517fa637def82ef37a207d6e1 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### `undo`'s description keeps the sentence RV-15 had removed from `open` (Minor)

The same fault as RV-15's "'A first open in a process cannot be undone' is false after a define_mesh", in the one text its fix did not touch. `src/Carve.Server/UndoTool.cs:16` ends "so undo straight after a first open in a new process is a tool error". A process that calls `define_mesh` and then its first `open` is a new process making its first open, and undo straight after it works: `scenarios/Carve.Scenario.Export/ExportScenario.cs:62,80-86` does exactly this and passes on `undone` being `"open"`, and `ModelStore.Undo` allows it (`src/Carve.Server/ModelStore.cs:66`). It also disagrees with `OpenTool.cs:17` ("a process that had a mesh before the open can") and `README.md:40`, which now draw the line correctly.

RV-15 cited this sentence as having it right. The sentence before it does state the rule, and "so" ties the case to it, which makes this less misleading than the sentence in `open`, but read alone it is the claim RV-15 rated Minor. The condition wanted is that the open is the process's first mesh, which is how `OpenTool.cs:17` puts it. Rubric 1.

**Outcome:** —

### "The one exception" leaves out the 100-step limit (Nit)

`src/Carve.Server/OpenTool.cs:17` says "The one exception is the first mesh a process has" and "a process that had a mesh before the open can"; `README.md:40` says "only the first mesh a process has cannot be undone". `ModelStore.PushLocked` also drops the oldest entry past 101 (`src/Carve.Server/ModelStore.cs:78`), and `Undo` stops at whatever is then oldest (`:66`). So after `define_mesh`, `open`, then 100 edits, 100 undos land on the opened mesh and the next is "nothing to undo", in a process that had a mesh before the open. Traced from the code, not run.

`UndoTool.cs:12` ("The last 100 changes can be undone") and D-1:155 are right and do not say "only", so the two statements that claim to be exhaustive are the ones that are not. It takes more than 100 steps after the open to reach it. Rubric 1.

**Outcome:** —

### The viewer's Carve-code check still misses two common forms (Nit)

Follows RV-15's "The viewer's Carve-code check misses a wider glob". The forms that finding named are caught, but the fix tests the path's shape, and two ordinary forms get through (`tests/Carve.Server.Tests/ProductReferencesNoExtensionTests.cs`):

- `:206-208` looks at the first segment after any leading `..`. An MSBuild property in front makes that the property itself, so `$(MSBuildThisFileDirectory)../../src/**/*.cs` and `$(MSBuildProjectDirectory)/../../src/**/*.cs` pass the check.
- `:193-195` takes the whole `Include` as an assembly name and looks at a file name only in `HintPath`. `<Reference Include="../../src/Carve.Core/bin/Debug/net10.0/Carve.Core.dll" />` passes the check, and MSBuild resolves that form through `{RawFileName}` in `AssemblySearchPaths`. RV-15 asked about "a `<Reference>` to a built Carve assembly".

Each was added as a case to the existing theories in a scratch copy and failed with `Assert.NotEmpty() Failure: Collection was empty`. The real `Carve.ModelViewer.csproj` has none of these; it holds one `PackageReference`. Rubric 2.

**Outcome:** —

## Summary

Checked the seven answers of RV-15 against `git diff 0fc1aa7..HEAD`, read whole, and the code around it. Six hold outright. The seventh, the viewer check, holds for the forms it names and has the gaps in the third finding. The two `open` findings hold for what they named: both false sentences are gone from `OpenTool.cs` and `README.md`. The first finding above is the same fault, left in `UndoTool.cs`.

**`open` and `undo` wording.** Read `OpenTool.Text` whole, `UndoTool.Text`, `README.md:35-43` and D-1's *Undo and history* against `ModelStore.cs`, and grepped every other tool text and `docs/` for undo wording. `open` is a step (`OpenTool.cs:58`), `define_mesh` and `open` are the only ways a first mesh arrives (`Push` and `Replace` have no other callers), a failed `open` pushes nothing, and the file holds no history. Every sentence is true and none contradicts another, except the two findings above. D-1:155, `SaveTool.cs:49` and T-17:111 are all true as they stand. "Session" in `UndoTool.cs:16` and D-1 is loose beside "process", but the next clause in each settles it.

**The other answers.**

- T-18 names the test that exists. T-17's line matches `MeshToolSupport.NoMesh`. T-15's `keys[n]` row matches `CarveFile.cs:140-141`, and `n` is the position among the root object's keys. T-19's `CheckFront` list matches `CrateBuild.cs:185-191`, and every native frame goes through it (`ExportScenario.cs:52,76,130`).
- `run-export-scenario.ps1:52-57` reads `$LASTEXITCODE` before anything can change it, and `$folder` is set by then. Read only, as in RV-15's table.
- The engine need 3 comment is in the rule's form, and all three sites now match a search for it.
- RV-14's citation is right: 8c9ff6e does not touch `ProductReferenceGuard.cs` and 5b269a0 does.
- The new test `Undo_straight_after_the_first_open_of_a_fresh_process_is_a_tool_error_and_the_mesh_stays` is in a `regression` class and checks both the error and the unchanged mesh.

**Behaviour and checks.** The only product change is the text of one description constant, and the viewer change is one comment. The `.carve` writer and reader, `GlbExport`, the tools' code and every tolerance are untouched. No test reads the changed sentences. No check was weakened: the new Compile rule catches everything the old literal match did, the theories only gained cases, and `The_viewer_check_passes_a_file_of_its_own` was widened as a guard against false positives. The script change adds output on one failure path and keeps its exit code.

**Retrospective.** Its git claims hold: `d014eae` changed one line of the script, and `eb9020d` (T-19) brought the unbuilt test step. RV-14's count is 2 Major, 16 Minor and 14 Nit, and the two Majors are one bug. RV-15's is 7 with one repeat. The queue agrees on Mantorok: `server-unit` failed there twice at 1d28607 and passed on Xellotath, though a retry is all the queue shows, not its cause. The stale "not yet ruled" text is gone. The person's rulings on T-20's two run conditions and on Mantorok, and the choice of a third round, are recorded nowhere else in the repo, so those rest on the retrospective alone. Those edits were not asked for by a finding, but they are the retrospective's own record and the commit message says so.

**Run by me.** In a scratch export of HEAD, built there, with three probe cases added: `server-unit` filter, 132 passed and 3 failed, the failures being exactly the three probes. So all 132 tests of HEAD pass. The worktree's own `bin/` was not used. The patch-id above comes from `reviews.diff_patch_id` against `origin/main` at HEAD 24b3f76. `cairn milestone M-3 verify` was not run, since the source changed and it could have run GPU suites. The same pipeline on 0fc1aa7 gives RV-15's f9b97b6, so it reproduces the recorded value. Scratch is in `C:\Users\andre\AppData\Local\Temp\m3-rv16\`.

**Read from the queue, not run by me.** At 1d28607, whose non-review content is HEAD's, `server-regression` and `scenario-export` passed on Xellotath (J-20261107-003635-f55764, J-20261107-003633-350eff). That is where the new undo test ran. `scenario-cube`, `scenario-crate` and `viewer-cube` show as done there, and I did not read their results.

**Not checked.** No GPU suite was run and no image was opened. The new undo test was not run by me. RV-15's table of breaks was not repeated, so that each check fails on its break rests on that table, apart from the three probes above. The `run-export-scenario.ps1` failed-build path was read, not run, and only a hand run reaches it. `core-unit`, `dotnet test Carve.slnx` and `verify.ps1` were not run. The 100-step sequence in the second finding was traced, not run. The person's rulings cannot be checked from the repo.
