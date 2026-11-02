---
description: >-
  T-14 tiers and suites
---
# RV-8 — T-14 tiers and suites

**Status:** Answered

**Reviews:** T-14

**Round:** 1

**Previous:** —

**Diff:** 5e07d32da624e74e55cadab22bcd49bcfc2b1f07 against main

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

### 1. Three tests in `unit` are well over its limit, and the rule in CLAUDE.md does not mention one

`tests/Carve.Core.Tests/KernelReviewTests.cs:7` tags the whole class `unit`. Timed here: `NoAcceptedRandomOutlineWindsRoundAnyPointTwice` (line 129) 14 s, `ManySidedConvexFaceKeepsEveryVertex(n: 4096)` (line 271) 3 s, `RandomSimpleConcaveOutlinesAreAllAccepted` (line 159) 1 s to 2 s. These three are most of `core-unit`'s time, and it runs on every kernel change. Testing.md's `unit` is about 1 s or less per test, so the 14 s and 3 s tests do not fit; the third is marginal. Rubric point 6. T-14's Notes send a test that does not fit its tier back to the person, and that has not happened.

A method-level `Tier` does not fix it. xunit merges class and method traits, so the test would match both `Tier=unit` and `Tier=regression`. The slow tests need a class of their own and a suite that selects them, such as a `core-regression`; no regression suite reads `tests/Carve.Core.Tests` today. If they move, T-8 and T-10 should name that suite, because these tests guard what `Create` accepts. `heavy` would not do: every item says `**Perf:** skip`, so they would stop running.

`CLAUDE.md:86-87` says what makes a test `regression` (starts `carve` or needs a device) and leaves out the time limit, which is how a test like these stays `unit`.

**Outcome:** skipped: left in `unit` for this task and raised with the person, as T-14's Notes require. CLAUDE.md's tier rule now states the time limit.

### 2. `server-regression` does not declare `scripts/run-scenario.ps1`, so a change to it carries a stale pass

`tests.json:40`. `DebugExtensionTests.ScriptAsync` (`tests/Carve.Server.Tests/DebugExtensionTests.cs:315-317`) runs the script for real, in the `The_script_*` tests (lines 200-295): exit codes, the stdout refusal, the timeout, case-blind lookup. The script is in no project directory, so `dotnet-closure` does not reach it; only `scenario-cube` lists it. An edit to the script alone, for instance one that breaks the exit code of 124 on a timeout, leaves `server-regression`'s fingerprint unchanged. Its old pass carries while `scenario-cube`, which only takes the success path, passes. The coverage check passes too, because `scenario-cube` owns the file. The old hook ran these on every verify. Add `scripts/run-scenario.ps1` to the suite's `inputs`. Rubric point 1.

**Outcome:** fixed

### 3. `server-unit` reads `scenarios/**/*.csproj` but does not declare it

`tests.json:26`. `ProductReferencesNoExtensionTests` (lines 23, 33, 41) calls `ProductReferenceGuard.ExtensionProjects` (`tests/Carve.Server.Tests/ProductReferenceGuard.cs:129-134`), which takes the forbidden names from every `.csproj` under `extensions/` and `scenarios/`. `extensions/Carve.Debug` is in the closure; `scenarios/` is not. A new scenario whose assembly name matches something the product already carries, with a name outside the `Carve.` prefix the guard refuses anyway, would fail that guard, but the pass carries. It needs an unusual name, so this is narrow, but the guard is an invariant test and the fix is one glob: `"scenarios/**/*.csproj"`. That glob leaves a `.cs` change in a scenario out of `server-unit`. Rubric point 1.

**Outcome:** fixed

### 4. A test class with no `Tier` trait now passes the gate without running

`tests.json:12,25,39` select by `--filter Tier=...`, so a class with no trait is in no suite. The old hook ran it. Nothing catches this: `cairn lint` warns only of a project no suite runs, and only `cairn test discover` reports untraited tests. All 238 are tagged today, so nothing has been dropped yet, but M-2 adds classes straight away: T-8 a new `MeshTopologyTests` (`docs/tasks/T-8-edge-table-and-stable-ids.md`), T-10 one per operation, T-11 the tool tests. None of those Scopes mention the trait, and only `CLAUDE.md:86` carries the rule. A member who forgets it gets a green verify with its new tests never run, and the reviewer's count does not show it. Either make the unit filters catch the untagged (for example `Tier!=regression`), or add the trait to those Scopes and make the check part of the review. Rubric point 1.

**Outcome:** fixed

### 5. T-12's plan does not add the crate project to `Carve.slnx`

`docs/tasks/T-12-crate-scenario.md:51` says `scenario-crate` follows `scenario-cube`, which is `build: solution` and `run-scenario.ps1 ... -NoBuild`. So only projects in `Carve.slnx` get built. The old `verify.ps1` line this replaced went through the script's own build, which needed no solution entry. Neither T-12's Scope nor `CLAUDE.md`'s *Writing a scenario* (line 142) says to add the project, so the suite fails on a missing DLL until someone does. The failure is loud, so this is only a gap in the plan: add it to T-12's Scope or to *Writing a scenario*. Rubric point 1.

**Outcome:** fixed

## Summary

Five findings. 1 needs the person's decision, as T-14's Notes require. 2 and 3 are one-line `inputs` fixes. 4 is the gap the tier filter opens for M-2. 5 is a plan edit.

Checked and fine:

- The other test classes are in the right tier. `ProductReferencesNoExtensionTests` reads built files and starts nothing. `RenderPumpTests` uses a fake scene, with no threads or delays.
- `dotnet-closure` follows `ReferenceOutputAssembly=false` references. It reaches `tests/Carve.Scenario.Tiny`, `tests/Carve.Server.Tests.Extension`, `extensions/Carve.Debug` and `src/Carve.Server`, which holds the `Compile Include` links. `TestMeshes.cs` is declared by hand.
- `Directory.Build.props` and `NuGet.config` are always-inputs.
- `TreatWarningsAsErrors` still applies, from `Directory.Build.props`, to the same solution build.
- Every test project and the cube scenario are still run. `verify.ps1`'s `dotnet test` of the solution reached the same two test projects.
- The `CLAUDE.md` text matches cairn, apart from the rule in finding 1.
- T-8 to T-13's `**Verify:**` lines all pass `coverage` for the files each Scope changes: `src/Carve.Core`, `src/Carve.Server`, their tests and the cube scenario are each an input of a required suite. T-12 and T-13's `full` needs no ownership by name.
- `cairn lint` is clean.

The test suites were not re-run. Only `KernelReviewTests` was timed, for finding 1.
