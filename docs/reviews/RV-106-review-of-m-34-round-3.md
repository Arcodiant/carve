---
description: >-
  Review of M-34, round 3
---
# RV-106 — Review of M-34, round 3

**Status:** Open

**Reviews:** M-34

**Round:** 3

**Previous:** RV-105

**Diff:** 9a8af46571cb3677405c1ea6a3e81259b04344b2 against 79c045c7

**Reviewer:** one fresh agent (Opus 5.5)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### The negatives' copies are not named after the round, so the path check fails on all five and the new tie is not shown

`scripts/run-script-acceptance-negatives.ps1:31-35` (`New-Copy`) puts each broken copy at `$work\<name>` (`byte`, `swapped`, `measure`, `cwd`, `missing`). The path check now requires the session's folder name (`round-1`) to equal the round's (`ScriptAcceptanceTests.cs:99`), so it fails on every copy. Run on a copy of round 1 (at `%TEMP%\rv106\round-1`), `acceptance/negatives.tsv` lists `Every_file_tool_path_is_under_the_sessions_folder_and_a_file_of_the_round` as failed for all five breaks. The effects:

- The existing three negatives no longer break one thing each. T-235's negatives table (the `Fails` column) and the RV-105 answer ("both caught by this check alone") do not match the run.
- Negative 5 (`prop-elsewhere.carvescript`) is caught by the folder-name assert, not the `File.Exists` assert it is meant to show. The same break in a copy named `round-1` does fail on `File.Exists`, so the code holds, but the script does not show it.
- Negative 4 (`cwd` = `C:\`) is caught by the old `outside` bound, not the new name tie. A root `cwd` plus the separator is `C:\\`, so no path starts with it. The name tie (a `cwd` such as `C:\Users`) is never shown failing either.

Fix: make each copy `$work\<name>\<round leaf>`. For negative 4, use a `cwd` that still holds the paths but has another name (the round's parent folder, for instance). Then correct the table. The real round passes all five checks. Comparing Write content with the round's files was left out, and I judge that an acceptable residual: to pass, a session would have to fake the round's folder name and files. Severity: important.

**Outcome:** —

## Summary

The new path check holds the ruling on the real round. It fails a session at `C:\` or at an unrelated folder, and it fails a written file the round does not hold (checked by hand in a copy named `round-1`). The corrected RV-104 `How:` lines for findings 11 and 13 match `EveryToolStage.cs` 134-139 and `AtomicStage.cs` 55-60. `e166caaa` changes nothing beyond the findings. One important finding: the negatives script names its copies so that the path check fails on every one, so its table and the RV-105 answer misstate what each break shows.
