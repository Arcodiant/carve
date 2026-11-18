---
description: >-
  Review of T-109
---
# RV-55 — Review of T-109

**Status:** Answered

**Reviews:** T-109

**Round:** 1

**Previous:** —

**Diff:** ff93586d7a5c28d72ba31e8f2bf92fa6cb0eb4c6 against f6893b1

**Reviewer:** one Opus 5.5 subagent in a fresh context

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### SECURITY.md says Carve's file access is bounded by its start folder; it is not

`.github/SECURITY.md:4-5`: "It reads and writes the files an agent names, relative to the folder it was started in". Rubric point 1 (correctness). `ToolPaths.Resolve` (`src/Carve.Server/ToolPaths.cs:48`) is `Path.GetFullPath(path, baseDirectory ?? Environment.CurrentDirectory)`. A relative path resolves from the start folder, but an absolute path is taken as it is, so an agent can read or write any path the user can. Carve also writes outside the start folder by itself:
- its log goes to `%TEMP%\carve-<pid>.log` (`src/Carve.Server/Program.cs:28`);
- `export`'s limits search walks up from the target folder towards the drive root (`src/Carve.Server/GameLimits.cs:225`) and can write the file it finds.

It also loads, as code, the extension assemblies its configuration lists (`src/Carve.Server/Program.cs:33`). A security policy is the one document where a reader takes "relative to the folder it was started in" as a promise of confinement. It should say that paths are not confined (relative ones resolve from the start folder) and that the extensions it is configured with run as code. "Opens no network port" holds: the transport is stdio.

**Outcome:** fixed: SECURITY.md now says Carve is not a sandbox: relative paths from the start folder, absolute paths as given, a temp-folder log, and the extension assemblies its configuration lists run as code.

### README and CONTRIBUTING say `scripts/verify.ps1` runs the full check; it runs one scenario

`.github/CONTRIBUTING.md:18-19` and `README.md:38-40`, the sentence after "`tests.json` lists every suite". Rubric point 1. `scripts/verify.ps1` builds the solution, runs `dotnet test Carve.slnx`, and runs only the `cube` scenario (`scripts/verify.ps1:14-22`). It does not run the other twelve scenario suites in `tests.json` (`scenario-sky`, `-rig`, `-crate`, `-uvs`, `-shed`, `-export`, `-character`, `-soft`, `-materials`, `-cuts`, `-gatehouse`, `-orcward`). CONTRIBUTING asks contributors to "say in your pull request whether you ran the GPU suites", and they will read "the full check" as all of them. Say what it runs (every test project and the cube scenario), or point at `tests.json`'s `run` lines for the rest.

**Outcome:** fixed: both now say `scripts/verify.ps1` builds, runs every test project and the cube scenario.

### The CI workflow has never run

`.github/workflows/ci.yml`. Rubric point 2 (tests cover the Scope). The only evidence for the Scope bullet "a CI workflow that builds and runs the suites that need no GPU" is reading the file; *As built* says "not yet run on GitHub". `origin` is GitHub and the workflow has a `pull_request` trigger, so pushing the branch and opening a PR against `main` would run it before `finish`. Otherwise its first run on `main` is the test, and *As built* should say so.

From reading it, I expect it to pass:
- The commands match the `run` lines of `tests.json`'s `core-unit` and `server-unit`.
- I checked every class in `tests/Carve.Server.Tests` without `[Trait("Tier", "regression")]` for GPU use (`EngineHost`, `CarveProcess`, Vulkan, Glfw). The only hit is `RenderPumpTests` (tagged `unit`), which uses a fake `IRenderScene` and a fake `IModelGeometryLoader` and no device.
- `Carve.Core.Tests` references no engine package.

**Outcome:** fixed: As built now says the workflow's first run, on the push to `main` at `finish`, is its check.

## Summary

Reviewed by Opus 5.5 (claude-opus-5-5), in a fresh context that wrote none of this. The scope was `git diff main...task/T-109` (5c6603e) and the uncommitted `### As built` in `docs/tasks/T-109-prepare-the-repo-for-public-release.md`. There are three findings, and none of them blocks the Goal: a public clone restores from nuget.org alone and builds.

**Checked and holds:**
- **`EmpyreanVersion` 0.129.0.** Carve references `Arcodiant.Empyrean.Animation`, `.Graphics` and `.Hosting.Glfw`, and nuget.org has 0.129.0 of each. It is the newest version all three share (Graphics also has 0.129.6; the other two stop at 0.129.0), so it matches the Scope. No `.csproj` has an engine literal. The README's link to `Arcodiant.Empyrean` on nuget.org resolves.
- **`NuGet.config` is deleted.** Outside `docs/`, no tracked file still names `localhost:5555`, `empyrean-local` or BaGet. The only remaining mentions are history records (`RV-1`, `T-21`).
- **Private paths.** No tracked file holds a `C:\Users\<name>`, `D:\repos` or `C:\repos` path. I read all 31 rewritten docs files as a word diff. Each rewrite is a prefix swap only (`branches\...`, `~\...`, `%TEMP%\...`, `<cairn>\...`), and no sentence lost its meaning. `C:\tmp\t43break` remains (`docs/milestones/M-18-soft-characters.md:65`); it is not a user or repos folder.
- **CI commands.** The build step and both test steps match `tests.json`. The README and CONTRIBUTING commands (`dotnet test tests/Carve.Core.Tests --filter "Tier!=regression"`, and the same for Server) are equivalent.
- **Scope.** Every change traces to a Scope bullet. The `CLAUDE.md` edit removes the feed URL and `D:\repos\baget`. The `Directory.Build.props` comment drops "the version Orcward's game uses", which is no longer true.
- **Tracker.** Status is `In Progress`, as expected before `finish`. *As built* matches the diff, including its count of 31 docs files. `cairn lint` is clean.

**Seen, not findings:**
- **The user's name in `LICENSE:3`.** It is the copyright holder, so I took it as deliberate. While this review ran, an uncommitted edit appeared in the worktree that puts `Arcodiant/` in front of the name. That edit is not part of the reviewed diff, and I left it uncommitted.
- **The engine moves ten minor versions,** and only `core-unit` and `server-unit` were run. The regression tier and the scenarios, which could show render changes, were not run. The gate onto `main` runs every `unit` and `regression` suite, so `finish` covers that.

**Ran:** `cairn lint`; the nuget.org version lists for the three engine packages; greps over tracked files for private paths and the old feed.

**Not checked:** the build and test suites (not run here; *As built* reports 1030 and 363 passing); any GPU suite; the workflow on a real runner; git history (out of scope).
