---
description: >-
  Review of M-34
---
# RV-104 — Review of M-34

**Status:** Answered

**Reviews:** M-34

**Round:** 1

**Previous:** —

**Diff:** `e1b1cb491f617013b7bbc617b8bf78c9cac6632f against 79c045c7`

**Reviewer:** three fresh agents: core (Opus 5.5), server (Opus 5.5), tests (Sonnet 5.5)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: core

### D-8's Rule says newlines may stand between any two tokens; the reader refuses one before '('

docs/design/D-8-scripts-the-call-journal-and-atomic-edits.md, *The statement format* (added line 80); rubric: documents match the code. The decision, a Strictness Rule, reads "Whitespace, newlines included, may stand between any two tokens, so a statement may span lines, but two statements never share one". But ScriptReader.cs:57-59 refuses `f\n(a: 1)` with "a statement may not break before its '('", and ScriptReaderTests pins that refusal. The As built section records the exception, and the Notes' refuse-by-default covers it. D-8 is the text M-35 plans from, and it states the opposite, so a reader of the Rule would expect `f\n(...)` to parse. The same paragraph also says "The person ruled it on 2026-11-26" right after "no trailing comma", but refusing the trailing comma was the implementer's default under the Notes, not a ruling. The fix is to add the break-before-'(' exception to D-8 and say both refusals are the default, open to loosening. Severity: important.

**Outcome:** fixed: D-8's Rule now states the refused newline before the `(`, and says that refusing it and a trailing comma are the implementer's defaults under T-230's Notes, not rulings, and open to loosening; the reader is unchanged.

### A CRLF line moves the column of an error at the end of the line

ScriptReader.cs:56-59 together with Fail (column = characters from the line start + 1); rubric: correctness against Scope ("CRLF and LF both read; the column counts characters"). SkipBlank steps over the '\r' before the '\n' check, and Fail counts that '\r' as a character. So `f\n(a: 1)` gives line 1, column 2, but `f\r\n(a: 1)` gives line 1, column 3. The test comment "CRLF does not move it" is shown only by `f()\r\ng(b 2)`, whose error is on the next line, so no test covers an error at the '\r'. Severity: nit.

**Outcome:** fixed: `Fail` moves an error at a CRLF's `
` back to its `
`, so `f
(a: 1)` is line 1, column 2 as with LF. Row added to ScriptReaderTests; shown failing on the old reader (column 3).

### A lone-surrogate escape reads but does not write, and Write throws an exception its docs do not list

ScriptWriter.cs:76 (`value.GetString()!`); rubric: edge cases, round-trip Goal. `f(s: "\ud800")` reads without error (Utf8JsonReader accepts the escape), but writing it throws InvalidOperationException from GetString. Write's `<exception>` names only ArgumentException. So T-232's save_script would fail on a journalled call carrying such a string instead of refusing it. As built lists this under "Not checked", so it is known, but it is neither handled nor documented on the API. Severity: nit.

**Outcome:** fixed: `ScriptWriter.Write` throws the documented ArgumentException (the smaller fix; the reader still accepts the escape), `<exception>` text extended. Test `A_lone_surrogate_in_a_string_is_refused_as_an_argument_exception` added; shown failing on the old writer (InvalidOperationException).

## Findings: server

### T-231's As built still documents the removed `Change(..., entries, restart, ...)` seam

`docs/tasks/T-231-the-call-journal-in-modelstore-and-clear.md:62`, rubric 7 (As built matches the diff). The "API, for T-232 and T-233" section names `ModelStore.Change(tool, change, IReadOnlyList<JournalEntry> entries, bool restart, out problem)` as the seam for several entries in one step. The refactor pass (60b1e4a9) removed that overload. The seam is now the private `PushLocked(model, tool, restart, entries)`, reached only through `ModelStore.Batch` (`src/Carve.Server/ModelStore.cs:426`, `:466`). M-35 is told to build on this section, so it points at an API that does not exist. Neither T-231 nor T-233's As built records the change. Severity: important.

**Outcome:** fixed: T-231's As built now names `ModelStore.Batch` (the write lock, any number of steps, one push through the private `PushLocked(model, tool, restart, entries)`) as the seam, and says the refactor pass (60b1e4a9) removed the unused `Change(..., entries, ...)` overload. T-233's As built already describes `Batch` and needed no change.

### run_script reports the published model after releasing the lock, not the snapshot its batch pushed

`src/Carve.Server/RunScriptTool.cs:80-86`, rubric 1 (correctness). `store.Batch(...)` returns the `ModelSnapshot` it pushed, but the code throws it away. It builds `meshes` from `store.Current.Model` after `Batch` has released `_write`. Case: another flow's call (a `move` sent alongside) takes the lock in that gap. Then the `run_script` result describes the model with that move applied, not the model the script made. Using the returned snapshot closes the gap. Severity: nit.

**Outcome:** fixed: `RunScriptTool` keeps the snapshot `Batch` returns and reports its meshes. No test: the race needs another flow between `Batch` and the report, so this was not shown failing.

### "Refused before anything runs" has no test that tells it from refusing lazily

`tests/Carve.Server.Tests/RunScriptToolTests.cs:107-124`, rubric 2 (a test that would pass on wrong code). Every refused script puts the refused tool at line 2 after a good `extrude`. It checks only the message and that the version is unchanged. A runner that checked each statement just before invoking it would print the same `line 2: ...` and roll back through `Batch`, so the test still passes. The T-233 Scope rule "refused before anything runs" goes untested. A failing line 1 followed by a refused line 2 tells the two apart: the up-front check names line 2, a lazy one names line 1. Severity: nit.

**Outcome:** fixed: test `A_refused_statement_is_named_even_when_an_earlier_one_would_fail_because_the_check_is_up_front` (line 1 an `extrude` of face 999, line 2 `save`; expects line 2). Shown failing with the up-front loop emptied (it then said line 1).

### The reader column of the every-tool journal test does not check that the call succeeded

`tests/Carve.Server.Tests/JournalToolTests.cs:49`, rubric 2. Each reader is called with `{}` and the result is ignored before the test asserts that the journal still has one entry. For readers whose `{}` is refused (`select` with no query, `render_glb` with no path), the assertion holds whatever the journalling does: a refused call pushes nothing anyway. So the "none for each read-only one" half of T-231's Scope test is weaker than it reads. Severity: nit.

**Outcome:** fixed: the reader column now makes a call each reader accepts (`select` with a query, the skeleton readers with a skeleton set up) and fails if one is refused, and compares the journal count before and after. `render` and `render_glb` need the engine, so they stay refused in this harness and only the count is held. Shown failing: with the old `{}` calls the tightened test listed render, render_glb, select, get_skeleton and get_weights as refused.

### Typo in ScriptsTopic's summary

`src/Carve.Server/ScriptsTopic.cs:3`: "what save_script writes. and how run_script (T-233) runs one." The full stop should be a comma, or the sentence rewritten. Severity: nit.

**Outcome:** fixed: full stop made a comma.

## Findings: tests

### Stage 3 drops 1e308 for 1e38 without the person's answer

scenarios/Carve.Scenario.Replay/NumbersPathsStage.cs:26 (clip duration `1e38`), T-234 Scope outline ("3 numbers: ... 1e308 ...") and Notes ("Ask, don't land ... a check that would be loosened goes back to the person"). The outline's 1e308 case became 1e38; `### As built` records it as a "limit" and says the format-level 1e308 is T-230's test. It names no ruling, though the task required one. The scenario therefore never replays an exponent above the float range. Severity: important (process; the product reason may well be sound).

**Outcome:** no change needed: the person's ruling, 2026-11-28 (option A): every tool stores floats and refuses 1e308, so no session's script can carry it; the format's bit-exactness on 1e308 is held by T-230's Core tests.

### Front-render equality has no positive pixel check

scenarios/Carve.Scenario.Replay/EveryToolStage.cs:~137 (`Same(before.Pixels, after.Pixels)`). Core rubric: a check that passes on wrong behaviour. Two blank or empty frames (camera framing nothing, nothing drawn) are byte-equal, so the pair passes. The Orcward and Acceptance frame checks add a positive check; this one relies on one manual look recorded in `### As built`. Severity: nit (the A == B byte check carries the real invariant; the frame is secondary).

**Outcome:** fixed: stage 1's front frame must show something at its centre unlike the sky at its corner (`before.Pixel(centre) != before.Pixel(0, 0)`). Scenario-side check, needs a GPU run; not shown failing.

### Stage 3 token checks are substring matches

scenarios/Carve.Scenario.Replay/NumbersPathsStage.cs:27-28. `text.Contains("0.1", OrdinalIgnoreCase)` also holds for `0.10000000000000001`, and `"-0"` for `-0.5`, so the "as written" check does not hold the written form by itself; only A == B backs it, and that fails to see a `-0` that both the save and the script lose. Matching on a delimited token (`[` or `,` before it, `,` or `]` after it) would hold it. Severity: nit.

**Outcome:** fixed: stage 3's token check matches a delimited token (not preceded or followed by a word character, `.`, `+` or `-`), so `0.1` no longer matches `0.10000000000000001` nor `-0` `-0.5`. Not shown failing (scenario run).

### Stages 4 and 5 leave small gaps in what they check

scenarios/Carve.Scenario.Replay/AtomicStage.cs:~64 (the refused run_script, save and render cases check the model only, not the journal or a following undo, unlike the failed-run case above them); BreaksStage.cs:~70 (a break counts as "caught" on any `run_script` error, without checking it names a line, so a refusal for the wrong reason passes); BreaksStage.cs:17 (`Find(...).First()` on an empty or failed stage-1 script throws InvalidOperationException and aborts the run, instead of noting a failure; stages 3 and 5 also read stage 1's `inputs/cube.carve` and static `EveryToolStage` fields). Severity: nit.

**Outcome:** fixed: stage 4's three refused-run cases also check the journal is unchanged; stage 5's break counts as caught by a refusal only if it names a line; BreaksStage `Find` notes a missing statement and stops rather than throwing. Not changed: the static fields stages 3 and 5 read from stage 1, which `ReplayStages.cs`'s order already makes safe. Not shown failing (scenario run).

### Journal stage read-only calls are not checked as accepted

scenarios/Carve.Scenario.Replay/JournalStage.cs:~28-33. The six `get_*`/`check` calls, `select` and `render` go through `d.CallAsync` with no result check, so a call refused for bad arguments (for example the `select` shape `{ y_above }`) leaves no line and the "reads leave no line" check passes without having exercised it. Severity: nit.

**Outcome:** fixed: the reads that can succeed (`get_mesh`, `get_parts`, `get_clips`, `check`, `select` with `{kind, where}`, `render`) now go through `Ok`. `get_skeleton` and `get_weights` stay unchecked: the stage has no skeleton and they answer so. Not shown failing (scenario run).

### Path check in the acceptance tests is anchored on the transcript's cwd, not the round folder

tests/Carve.ScriptAcceptance.Tests/ScriptAcceptanceTests.cs:~96-114 (`Every_file_tool_path_is_inside_the_round_folder`). The test name says round folder, but the bound is the first `cwd` found in `transcript.jsonl`; if the session was started elsewhere (the run copy under `%TEMP%`, not `Round`), every path inside that cwd passes. It also skips every `mcp__carve__` call, so `save`, `open` and `save_script` paths are not covered (T-235's Scope speaks of file-tool paths, so this matches the Scope). Severity: nit (rename, or compare the cwd with `Round`).

**Outcome:** fixed: by renaming the test to `Every_file_tool_path_is_inside_the_sessions_folder`, which is what it checks (the transcript's cwd); the `mcp__carve__` paths stay out, as T-235's Scope speaks of file tools. Comparing with `Round` was not done, as a run copy elsewhere is legitimate.

### Statement-kept check aligns by tool name only

tests/Carve.ScriptAcceptance.Tests/ScriptAcceptanceTests.cs:~42-50. A statement of `prop.carvescript` is matched to the next statement of the same tool in the edited script, so a deleted `knife` whose place a later `knife` takes counts as "kept with new values". This is within ruling Q1 A ("as it was or with new values"), but the diff file is the only evidence. Severity: nit.

**Outcome:** no change needed: ruling Q1 A allows each statement to be kept as it was or with new values, and the diff file is written for the person to read the pairing; matching by tool name in order is that rule.

## Summary
