---
description: >-
  Round 2 review of T-109
---
# RV-56 — Re-review of T-109

**Status:** Answered

**Reviews:** T-109

**Round:** 2

**Previous:** RV-55

**Diff:** 81b53638deed9a4107456837658b1b91ae8a0a60 against f6893b1

**Reviewer:** one Opus 5.5 subagent in a fresh context

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings

None. Each of RV-55's three answers holds, and the delta since RV-55 (`git diff 8ccd147..HEAD`) adds nothing wrong.

## Summary

Reviewed by Opus 5.5 (claude-opus-5-5), in a fresh context that wrote neither the change nor RV-55. The scope was RV-55 with its answers, and `git diff 8ccd147..HEAD` (58ef688, b2e124c): `.github/SECURITY.md`, `.github/CONTRIBUTING.md`, `README.md`, `LICENSE`, T-109's *As built* and RV-55's outcomes. `Diff:` covers the whole branch, `f6893b1...HEAD`, without the tracker directories.

**RV-55's answers, checked against the files:**
- **SECURITY.md (fixed, holds).** It now says Carve is not a sandbox and runs with the user's permissions. It says a relative path is taken from the start folder and an absolute path is used as given, which matches `Path.GetFullPath(path, baseDirectory ?? Environment.CurrentDirectory)` (`src/Carve.Server/ToolPaths.cs:48`). It names the log in the temp folder (`Path.GetTempPath()`, `src/Carve.Server/Program.cs:28`) and the extension assemblies loaded from configuration (`ExtensionLoader.Load`, `Program.cs:33`, key `Carve:Extensions`). "Opens no network port" still holds.
- **README and CONTRIBUTING on `scripts/verify.ps1` (fixed, holds).** Both now say it "builds, runs every test project and the cube scenario". The script runs `dotnet build`, then an unfiltered `dotnet test Carve.slnx` (every project, regression tier included), then `run-scenario.ps1 cube` (`scripts/verify.ps1:14-22`). Its own synopsis says the same.
- **CI never run (fixed, holds).** *As built* now says the workflow has not run on GitHub and that its first run, on the push to `main` at `finish`, is its check. `.github/workflows/ci.yml` has `push: branches: [main]`, so that run will happen.

**The delta:**
- **`LICENSE:3`** is now `Copyright (c) 2026 Arcodiant/Andrew Armstrong`. That is the maintainer's choice, and it matches Empyrean's `LICENSE` line for line.
- ***As built*** is now committed. It matches the branch: `EmpyreanVersion` 0.129.0, CI on `windows-latest` running `core-unit` and `server-unit`, the README and CLAUDE.md edits, 31 docs files. The cited comment is at `src/Carve.Server/SkinnedExport.cs:36`, as stated.
- **RV-55** changed only its status and three outcome lines. That is the implementer's answer, and the rest of the file is as written.
- Nothing in the delta goes beyond RV-55's findings and the requested LICENSE change.

**Seen, not a finding:** SECURITY.md does not mention that `export`, when given no limits path, looks for the limits file in every folder from the target's folder up to the drive root (`src/Carve.Server/GameLimits.cs:225`). With `write_limits: true`, it fills that file in. The agent asks for this when it names an export target, and the paragraph does not claim its list of file effects is complete. "Runs with your permissions and reads and writes whatever files the agent names" does not promise confinement.

**Ran:** `cairn lint`; `cairn task T-109 verify`; a read of the sources above.

**Not checked:** the workflow on a real runner; any GPU suite by hand. Verify's own test check covers the suites.
