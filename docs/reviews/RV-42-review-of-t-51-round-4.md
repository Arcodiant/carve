---
description: >-
  Review of T-51, round 4
---
# RV-42 — Review of T-51, round 4

**Status:** Open

**Reviews:** T-51

**Round:** 4

**Previous:** RV-41

**Diff:** 5c8fdbd0bfb343ba0bebb504a9a5744393afa512 against origin/main

**Reviewer:** a fresh general-purpose agent on Opus 5.5 (Claude Code), not the implementer

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Summary

No findings. Scope was commit 9139061 only: the check in `LimitsSource.Find` (`src/Carve.Server/GameLimits.cs:198-204`) and its test cases. Both sides of the comparison are full paths from `ToolPaths.Resolve` (`Path.GetFullPath`), so `/` and `\` are already normalized to the platform separator before the compare. `target` always ends in a file name with `.glb`, so the `TrimEnd` plus one separator gives exactly `<target>\`. That means `big.glb2/x.json` does not match and is not refused. `limits: "big.glb/"` and `limits: "big.glb"` become `big.glb\carve-limits.json` and are refused. The comparison ignores case, which matches how Windows and default macOS file systems behave. The equality branch cannot be hit, because the limits path ends in `.json` and the target ends in `.glb`, but it does no harm. Both test cases would fail if the check were removed. In `GameLimitsTests`, `Find` would return a source, because `thing.glb` does not exist yet and `write` is true. In `ExportLimitsToolTests`, the export would write the .glb and then fail on the limits write, so the refusal text and `Assert.False(File.Exists(glb))` would both fail. The build is clean, and GameLimitsTests and ExportLimitsToolTests pass (30).
