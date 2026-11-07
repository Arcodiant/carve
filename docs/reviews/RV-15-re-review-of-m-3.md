---
description: >-
  Re-review of M-3
---
# RV-15 — Re-review of M-3

**Status:** Open

**Reviews:** M-3

**Round:** 2

**Previous:** RV-14

**Diff:** f9b97b6666f0af9d058a709121f8b4ecdf1f4688 against origin/main

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

### open's description still says undo stops at the open (Minor)

Repeated from RV-14 ("open's description contradicts itself on undo"), answered `fixed`. That finding named two sentences. The fix corrected the first and only reworded the second: "undo cannot go back past the open" became "undo goes back no further than the open" (`src/Carve.Server/OpenTool.cs:19`), which says the same thing.

It is still false within a process, and it still contradicts line 17 of the same text ("within a session undo takes the open back to the mesh before it"). `open` calls `store.Push` (`OpenTool.cs:58`), which adds to the history and clears nothing, and `ModelStore.Undo` then steps past it (`src/Carve.Server/ModelStore.cs:62-71`). `ExportScenario.cs:80-86` and `SaveOpenToolTests.cs:54` both undo an `open` back to the earlier mesh. `UndoTool.cs:16` and D-1 have it right. Rubric 1.

**Outcome:** fixed

Fixed in 9b43de4.

### "A first open in a process cannot be undone" is false after a define_mesh (Minor)

New wording from the fix, in `src/Carve.Server/OpenTool.cs:17` and `README.md:40-41`. What cannot be undone is the first mesh a process has, not its first `open`. A process that calls `define_mesh` and then its first `open` can undo that `open`: `ExportScenario.cs:61-62,80-86` does exactly this, and passes on `undone` being `"open"`. An agent reading the sentence would believe the mesh before the `open` is lost when it is not.

`UndoTool.cs:16` and D-1's *Undo and history* say it correctly ("the first mesh a process has"). The rule is right in the fresh-process case the finding was about, but no test calls `undo` straight after `open` in a new process: `A_later_session_opens_what_an_earlier_process_saved` stops at the `move`, so the newly documented tool error has no check of its own. Rubric 1.

**Outcome:** fixed

Fixed in 9b43de4.

### The fixes left member notes stale (Nit)

The same kind as RV-14's "`### As built` notes went stale in the refactor", this time from the fixes themselves. Rubric 7.

- `docs/tasks/T-18-model-viewer-spike.md:126` names `The_viewer_references_no_Carve_project`, which 5b269a0 removed. It is now `The_viewer_references_no_Carve_code` in `ProductReferencesNoExtensionTests`.
- `docs/tasks/T-17-save-and-open-tools.md:102`, under "Wording T-19 and T-20 rely on", still gives `save: there is no mesh yet; call define_mesh first`. The message now goes on `, or open to read a saved .carve file` (`src/Carve.Server/MeshToolSupport.cs:15`).
- T-15's As built table of every error (from `docs/tasks/T-15-carve-file-in-the-kernel.md:134`) has no row for the new `keys[n]` element. That name is a reading the spec did not settle, and the retrospective's T-15 readings (`docs/milestones/M-3-first-model-in-a-game.md:140`) list neither it nor the `1.0` reading. RV-14's finding named the retrospective as well as As built, and the answer recorded As built only.
- `docs/tasks/T-19-export-scenario.md:99` lists `CrateBuild`'s members without `CheckFront`, and `:119` lists what the scenario asserts without any pixel check on its frames, the fix to a Minor.

**Outcome:** fixed

Fixed in 9b43de4.

### The retrospective blames the refactor pass for a gap T-19 made (Nit)

`docs/milestones/M-3-first-model-in-a-game.md:138` says RV-14 fixed "what the pass left (an unbuilt test step in `run-export-scenario.ps1`)". `d014eae` changed one line of that script, replacing `Write-Err`'s definition with the dot-source of `bounded-run.ps1`. The `--no-build` step came in at `eb9020d` (T-19). Rubric 7.

Also, RV-14's answer to "The guard's summary leaves out `spikes/`" (`docs/reviews/RV-14-review-of-m-3.md:202`) cites 8c9ff6e, which does not touch that file; only 5b269a0 does.

**Outcome:** fixed

Fixed in 9b43de4.

### A failed build of the checks leaves no run folder on stdout (Nit)

New in 5b269a0. `scripts/run-export-scenario.ps1:52` is `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }`, with no `Write-Output $folder` and no message of the script's own. Every other failure path prints the folder, and the script's help (`:8`) and `CLAUDE.md:73` say it is the last thing on stdout. By then the scenario and viewer have run and their output exists. Only a run by hand reaches this line, since the suite passes `-NoBuild`. Rubric 1.

**Outcome:** fixed

Fixed in 9b43de4.

### The engine need 3 citation is not in the rule's form (Nit)

`spikes/Carve.ModelViewer/ViewerSystem.cs:190` cites the need, as RV-14 asked, but as "The engine's Material has no name to report: engine need 3". `CLAUDE.md:213` gives the form `// Interim until engine need N: ...`, and both other sites use it (`src/Carve.Server/EngineThread.cs:70`, `src/Carve.Server/RenderTool.cs:187`). A search for that form misses this one. Rubric 5.

**Outcome:** fixed

Fixed in 9b43de4.

### The viewer's Carve-code check misses a wider glob (Nit)

`tests/Carve.Server.Tests/ProductReferencesNoExtensionTests.cs:180` matches the literal text `src/Carve.`, `extensions/Carve.` or `scenarios/Carve.`. That catches the link RV-14 described, but `<Compile Include="../../src/**/*.cs" />` holds none of the three, so it would pass while bringing in all of Carve. A `<Reference>` to a built Carve assembly is not looked at either. T-18 says "No Carve code". Rubric 2.

**Outcome:** fixed

Fixed in 9b43de4.

## Summary

Checked all 32 answers of RV-14 against `git diff ff57cdd..HEAD`, which I read whole, and the code around it. 30 hold, both `no change needed` among them, though three of those carry a Nit above (the `1.0` reading's retrospective half, the engine need citation, the Carve-code check). Two do not fully hold, both on `open` and undo: "open's description contradicts itself on undo" (the first finding, a repeat) and "`open` says one undo takes you back", whose fix is right for a fresh process but brought the false sentence of the second finding.

**The two `no change needed` reasons.** T-16's Verify scope: `required()` in cairn's `scripts/cairn/gate.py:184-197` takes the unit suites with a changed input plus the named ones, and `cairn milestone M-3 verify` lists all seven suites. T-15's `1.0`: the refusal list says "an array of integers" (T-15:50), `Write` emits integers, and As built records it (T-15:203). The Scope's "held to exactly what `define_mesh` accepts" (T-15:45) still stands beside it.

**Behaviour and checks.** The `.carve` writer and `GlbExport`'s output code are unchanged; the render colour keeps its value. Tool behaviour moved only where the findings asked: the surrogate refusal, the no-mesh message, dot-files in the missing-file listing, the flush before the move, and the description texts. No check was skipped or loosened. The sample change in `FrontPlusZPanel` is the finding's, `CheckFront` adds checks, and the moved viewer check is stricter than the old one. The one test-side skip, `ExportViewerTests.Run`, is matched by the script failing on any skipped or unpassed check.

**Run by me.**

- From a scratch export of HEAD, built there: `core-unit` filter 183 passed, `server-unit` filter 126 passed.
- `Carve.ExportViewer.Tests` with `CARVE_EXPORT_RUN` unset: exit 0, 4 skipped.
- A ray cast through T-19's saved eye and `crate.carve`: all five `FrontPlusZPanel` samples land on face 1 at z = 0.92, the panel's edge is at about x = 101 and it runs past x = 258, x = 285 was on face 11 as RV-14 said, and the recess-wall, +Z frame, +X and top samples are on the faces their comments give.
- The real `bounded-run.ps1` quoting line on six arguments: each comes out as the Windows command line needs.

Note for whoever runs from this worktree: `dotnet test --no-build` on `tests/Carve.Core.Tests` fails 3 surrogate cases, because that `bin/` still holds a `Carve.Core.dll` built during RV-14's break run (18:37:46, before the source was restored at 18:37:53). It is a stale binary and not a defect; the suites build first.

**Not checked.** No GPU suite was run and no image was opened, so the following rest on RV-14's table and the carried passes in `cairn milestone M-3 verify`: `CheckFront` on real frames, the viewer tests including the default-view and backslash ones, the step-3 timeout and skip detection, the second run into a named folder, and every regression test in `Carve.Server.Tests`. `CheckFront`'s claim that an up-flipped camera fails was never tried, here or in RV-14. T-16's actual verify output is recorded nowhere in the repo, so that clause of the answer is confirmed from the rule in `gate.py` and not from the output, and I did not run `cairn task T-16 verify`. The solution-wide `dotnet test Carve.slnx` and `verify.ps1` were not re-run. The flush before the move has no test and cannot have one short of a power loss; it was read only.
