---
description: >-
  Review of M-34, round 2
---
# RV-105 — Review of M-34, round 2

**Status:** Answered

**Reviews:** M-34

**Round:** 2

**Previous:** RV-104

**Diff:** b08d4b9802265f52ae3dcbe77284411a6fac2820 against 79c045c7

**Reviewer:** one fresh agent (Opus 5.5)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings

### RV-104 finding 14: the rename leaves the ruled check unenforced

`tests/Carve.ScriptAcceptance.Tests/ScriptAcceptanceTests.cs:73-94`. The test still bounds paths by the first `cwd` the transcript reports. Nothing ties that `cwd` to `Round` (line 19) or to anything else, so a session started at `C:\` would pass every file-tool path. The rename makes the name honest, but it does not answer the finding. The check is not optional either. T-235's ruling 1 (run condition A, line 48) allowed the file tools on the condition of "a check that every file-tool path stays in the round folder". The As built's "inside the session's folder" (line 82) records a weaker check than the one ruled, and nobody asked the person about it. The answer's reason (the run copy under `%TEMP%` is legitimate) argues for tying the cwd to the round, not for leaving it untied. Two ways to do that: require the cwd to end in the round's folder name, or require the cwd to hold the round's files (`prop.carvescript`, `transcript.jsonl`). Otherwise take the loosening back to the person. Severity: important.

**Outcome:** fixed

How: `Every_file_tool_path_is_under_the_sessions_folder_and_a_file_of_the_round` (the name says what it checks) now requires every file-tool path in the transcript (Read, Write, Edit) to lie under the session's `cwd`, the `cwd`'s folder name to equal the round folder's, and each path to exist in `Round` at the same relative path from the session folder. Passes on the real round; `scripts/run-script-acceptance-negatives.ps1` has two more planted copies (`cwd` set to `C:\`; the written file renamed to one the round does not hold), both caught by this check alone. T-235's `### As built` and CLAUDE.md say so. Not checked: a session at another path that has the round's folder name and holds the same files.

### RV-104 findings 11 and 13: two How lines do not describe the code

`docs/reviews/RV-104-review-of-m-34.md:113` says stage 1's front frame must show something at its centre unlike the sky at its corner (`before.Pixel(centre) != before.Pixel(0, 0)`). The code (`scenarios/Carve.Scenario.Replay/EveryToolStage.cs:135-140`) does something else. It counts pixels that differ by over 40 from their own row's left edge and notes a failure under 1 % of the frame. That is a sound check, but the record names a different one. Line 129 answers finding 13's journal half but says nothing of its "or a following undo" half for the refused runs (`AtomicStage.cs:57-61`). The "Not changed" clause lists only the static fields. Either add the undo check or say why it is not needed (the refusals are up front, so nothing ran to undo). Severity: nit.

**Outcome:** fixed

How: RV-104 findings 11 and 13 `How:` lines corrected to what `EveryToolStage.cs` 135-140 (a row-edge difference count, over 1 % of the frame) and `AtomicStage.cs` 57-61 do; the undo half of finding 13 is listed under what was not changed, with why.

## Summary

I checked all 15 RV-104 answers against the fix commits (1571060c, 4dcd2e5d, b4799394, d023dcad) and the code they touch. I judged by reasoning on the old code that the new or tightened tests would fail there: the CRLF column row, the lone-surrogate test, the line-2-first test, the reader column and the delimited token regex. The fixes for findings 5 (`Batch`'s returned snapshot) and 10 to 13 hold. The replay project builds. Findings 9 and 15 rest on the person's rulings. The one answer that does not hold is finding 14's rename. No fix commit changes a tool description, argument or result beyond its finding.
