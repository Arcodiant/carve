---
description: >-
  Review of T-51, round 3
---
# RV-41 — Review of T-51, round 3

**Status:** Open

**Reviews:** T-51

**Round:** 3

**Previous:** RV-40

**Diff:** —

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

### A `limits` path under the .glb being exported still passes `Find` and fails only after the .glb is written

The new ancestor check in `LimitsSource.Find` (`src/Carve.Server/GameLimits.cs:198-205`) looks at the disk as it is before the export, but `ExportTool.Run` calls `Find` before writing the .glb (`src/Carve.Server/ExportTool.cs:115-120`). So the one ancestor `Find` cannot see is the file the export itself is about to create. `export {"path":"big.glb","limits":"big.glb","write_limits":true}`, with no `big.glb` yet, gives the candidate `big.glb/carve-limits.json`. That resolves, is not a folder, and has no existing file above it, so `Find` succeeds. The .glb is then written, and `ToolPaths.Write` fails in `Directory.CreateDirectory` (`src/Carve.Server/ToolPaths.cs:109`) because `big.glb` is now a file. The result is the tool error "... The .glb was written" (`ExportTool.cs:125`). `limits: "big.glb/x.json"` goes the same way. With `overwrite: true` and an existing `big.glb`, the new check does catch it. This is the rule RV-40's first finding was about: a `limits` argument that no file could be written at must be refused before the .glb is written. The input is contrived, but the check is one comparison: when `write` is set, also refuse a limits path that is the export target or lies under it (`path` starts with `target` plus a separator, or any `above` equals `target`). No test covers it. Rubric 1.

**Outcome:** fixed (third round on the limits path, so taken to the person, who ruled 2026-11-13 to refuse it up front with no fourth round): `LimitsSource.Find` refuses a limits path equal to or under the export target, before the .glb is written. Covered by two cases in `GameLimitsTests` and one in `ExportLimitsToolTests` (`limits` = the .glb, with `write_limits`).

### As built's *Code* paragraph does not list the new refusal

The T-51 task file (`docs/tasks/T-51-export-limits-per-game-not-orcward-s-constants.md`, the `**Code:**` line under `### As built`) still lists the refusals as "a blank argument, a non-`.json` file or a folder where the file should be is refused before the `.glb` is written (RV-39)". 42d3ab2 updated *Tests* and *Not verified* to mention a path under a file, but *Code* does not mention the refusal it added to `Find`, so As built no longer matches the code. Rubric 7.

**Outcome:** fixed: As built's *Code* paragraph lists every refusal of the `limits` argument with the round that added it, and *Tests* lists the new cases.

## Summary

Two findings. The RV-40 fixes otherwise hold. `Find` now refuses `notes.txt/` and `notes.txt/game.json` with `write_limits` before anything is written, and `GameLimitsTests` covers both. The ancestor walk ends cleanly at a drive or share root, and with `write_limits` false a path under a file now gets the more accurate message. `With_no_file_a_model_past_every_starting_number_gets_no_limit_warning` exports 5202 triangles and ten colours past 5000 and 8 with no file, and would fail if `Starting` came back as the default for a static export. The skinned gap is recorded under *Not verified*. `NoLimitsFileAboveRoot` now guards the two `GameLimitsTests` that search upward. The one gap left is a limits path under the .glb the export is about to create. The build is clean, and GameLimitsTests and ExportLimitsToolTests pass (30).
