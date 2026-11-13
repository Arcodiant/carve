---
description: >-
  Review of T-51, round 2
---
# RV-40 — Review of T-51, round 2

**Status:** Open

**Reviews:** T-51

**Round:** 2

**Previous:** RV-39

**Diff:** `8016de635024c72b80a2b811a9ed0a08243b0e8c against origin/main` (at c2e0d49)

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

### A `limits` argument under an existing file still passes `Find` and fails only after the .glb is written

`LimitsSource.Find` (`src/Carve.Server/GameLimits.cs:178-202`) now refuses a non-.json file that is the argument itself (`:179`) and a folder at the resolved path (`:193`), but not a path whose parent folder is an existing file. RV-39 suggested that check ("reject a write target ... whose parent is an existing file"), and its outcome says the refusals all come before the .glb is written. Two inputs still get through with `write_limits: true`:
- `limits: "<root>/notes.txt/"`, with `notes.txt` an existing file. `File.Exists("notes.txt/")` is false, so the check at `:179` does not fire. `trimmed` is `notes.txt`, which does not end in .json, so the candidate becomes `notes.txt/carve-limits.json`. `Resolve` accepts it, it is not a folder, and it does not exist, so `Find` succeeds.
- `limits: "<root>/notes.txt/game.json"`. The candidate is the argument itself, and it passes the same way.

In both cases `ExportTool.Run` writes the .glb (`ExportTool.cs:118-120`). Then `ToolPaths.Write` fails in `Directory.CreateDirectory` (`ToolPaths.cs:109`), and the result is the tool error "... The .glb was written" (`ExportTool.cs:125`). That breaks the ruling that a bad limits argument is a tool error that writes nothing. It is also the gap RV-39 named as fixed. The fix is a check in `Find`, when `write` is set, that no ancestor of `path` is an existing file. `A_limits_argument_no_file_could_be_at_is_refused_even_with_write_limits` (`GameLimitsTests.cs:124`) has no such case. Rubric 1.

**Outcome:** fixed: `LimitsSource.Find` walks up from the limits path and refuses it when any folder above it is an existing file, before the .glb is written. `GameLimitsTests` covers `notes.txt/` and `notes.txt/game.json`.

### The skinned "no file, no warnings" test cannot fail if Orcward's numbers come back as a default

RV-39's second finding asked for a test that fails if a regression brought `GameLimits.Starting` back as the default for skinned exports with no file. The answer points to `A_skinned_export_warns_only_on_the_games_file_and_clips_mode_reads_and_writes_it` (`tests/Carve.Server.Tests/ExportLimitsToolTests.cs:56-59`). That test exports the two-box chain from `SkinnedExportToolTests.StartChainAsync`: 24 triangles, 2 materials, and bones `base`/`joint`, which are not the Synty set. Every one of those is inside 5000/8, and the height check never runs. So with `Starting` as the default, the export would still give no warnings and the test would still pass. The static side has the same gap: `ExportToolTests.cs:26-27` checks a 12-triangle cube. No test anywhere exports past a starting number with no limits file, so the spec point "with no file there are no limit warnings" has no test that would fail without it (rubric 2). The fix is a no-file case that would trip a starting number, for example a model with 9 or more colours, or a Synty-boned model outside 1.4 to 2.4 m.

**Outcome:** fixed in part: `With_no_file_a_model_past_every_starting_number_gets_no_limit_warning` exports 5202 triangles and ten colours with no file and asserts no limit warning. A skinned model past a starting number is not exported at tool level. Skinned, static and clips all take their limits from the same `LimitsSource.Find`, which `No_file_means_no_limits_and_the_result_says_so` holds to `GameLimits.None`, and `SkinnedExportTests` holds that the warnings come only from the `GameLimits` passed. As built's *Not verified* records the gap.

### As built's Tests paragraph does not list the tests added in answer to RV-39

The *Tests* paragraph in `docs/tasks/T-51-export-limits-per-game-not-orcward-s-constants.md` (the `**Tests:**` line under `### As built`) still lists only the round-1 coverage of `ExportLimitsToolTests`. c2e0d49 updated *Code* for the RV-39 fixes but not *Tests*. It leaves out the skinned and clips-mode tool test, the static `max_submeshes` case through a coloured face, `Write_limits_creates_a_named_json_file_that_is_not_there_yet`, the `NoLimitsFileAboveRoot` guard, and the new unit cases (`A_limits_argument_no_file_could_be_at_is_refused_even_with_write_limits`, `A_comment_is_refused_since_write_limits_would_lose_it`). The *Not verified* note on the upward search also predates the guard, which now makes those tool tests fail instead of writing. Rubric 7 (As built matches the diff).

**Outcome:** fixed: As built lists the tests added in answer to RV-39 and RV-40. *Not verified* says that every test that searches upward now checks first, including the two in `GameLimitsTests` that also lacked the guard.

## Summary

Three findings. Of RV-39's six fixes, four hold: the upward-search guard, the per-bound height assertions, subdivide's wording, and plain JSON with the rewrite documented. Two are incomplete. The `limits` argument can still fail after the .glb is written when its parent is an existing file, and the skinned no-file test cannot catch the regression it was added for. The As built Tests paragraph is also stale. The build is clean, and GameLimitsTests and ExportLimitsToolTests (29) pass, as do SkinnedExport*, SubdivideToolTests, ExportToolTests and ClipLibrary* (49). No issues were found in the warnings logic, the limits-at-the-boundary comparisons, the fill and create paths, clips mode, or the refused-export paths.
