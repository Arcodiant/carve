---
description: >-
  T-14 re-review
---
# RV-9 — T-14 re-review

**Status:** Answered

**Reviews:** T-14

**Round:** 2

**Previous:** RV-8

**Diff:** 27a277a226c5cebee269509bc380b3bc815acc61 against main

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

### 1. RV-8's outcomes for findings 4 and 5 are outside the vocabulary, so `cairn lint` fails

`docs/reviews/RV-8-t-14-tiers-and-suites.md:56,62`. Both read `fixed: <detail>`, but `reviews.OUTCOME_RE` takes a bare `fixed`; only `skipped:` and `no change needed:` take a reason. `cairn lint` exits 1 with three problems, one for each line and one saying RV-8 is `Answered` with two findings unanswered. Change both lines to `**Outcome:** fixed`; the detail is already in the commit and the diff. Rubric point 7.

**Outcome:** fixed

## Summary

One finding, a format error in RV-8. The substance of every answer holds, and the fixes introduced nothing new.

RV-8's answers:

- **1 (skipped).** The reason is recorded at RV-8:38. `KernelReviewTests.cs:7` is still `unit`, and `CLAUDE.md:87-88` now states the limit, in the words of testing.md. That it was raised with the person is taken from the brief for this review; nothing in the repo records it. Not re-raised.
- **2 (fixed).** `scripts/run-scenario.ps1` is an input of `server-regression` (`tests.json:46`). The fingerprint hashes it, so an edit to the script alone ends the carried pass. The script reads nothing else that the suite does not have: it looks under `scenarios/` and `tests/`, and the tests only ask for `tiny`, which is in the closure, or a name that does not exist.
- **3 (fixed).** `scenarios/**/*.csproj` (`tests.json:31`) matches the cube's project in cairn's `glob_regex` and resolves, so lint is silent on it. A glob is hashed by the files it matches, so a new project changes the key and a scenario's `.cs` does not. That is what RV-8 wanted.
- **4 (fixed).** `tests.json:12,25` use `Tier!=regression`, and `server-regression` keeps `Tier=regression`, so in the server project the two filters split every test with none left over. 69 + 70 + 99 = 238, the set T-14's Scope names, and the queue shows all four suites done on d3b88ff. All 16 test classes are tagged, and `cairn test discover --stdout` lists none untagged. So no untagged class has gone through the filter yet: that one runs rests on how a vstest `!=` treats a missing property, not on a run.
- **5 (fixed).** `docs/tasks/T-12-crate-scenario.md:51` and `CLAUDE.md:88-89` both say a new scenario joins `Carve.slnx`, as the outcome claims. That file is an always-input, so adding the project also moves every fingerprint.

Also checked and fine:

- The `CLAUDE.md` text agrees with tests.json. Nothing else names `Tier=unit`.
- The new inputs do not change any `**Verify:**` line's coverage. T-8 to T-13 name suites that still own what each Scope changes. The script now has two owners, and any change to it onto `main` runs both.
- Nothing outside tracker hygiene changed in d3b88ff beyond what the brief lists.

Left as it stands, and not findings:

- A class tagged `regression` in `Carve.Core.Tests` would run in no suite. That was already so, there is none today, and only finding 1 of RV-8 could bring one.
- T-14 has no `### As built` yet. The task is still In Progress and its Scope puts the counts in the closing report.

The suites were not re-run. `cairn lint` and `cairn test list` were; apart from finding 1, lint is clean, and the list shows the four suites.
