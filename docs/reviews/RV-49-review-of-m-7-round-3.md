---
description: >-
  Review of M-7, round 3
---
# RV-49 — Review of M-7, round 3

**Status:** Answered

**Reviews:** M-7

**Round:** 3

**Previous:** RV-48

**Diff:** 54c3facfe59592a095406fa6291a5d3d59d16604 against 5bc4559

**Reviewer:** claude-opus-5-5 (one reviewer: RV-48's answers and the round-2 fix diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-48's answers and the round-2 fix diff (claude-opus-5-5)

### The record cites rebased-away commits, and calls one of them "the head"

**Where:** `docs/milestones/M-7-materials-and-textures.md:270`, `docs/reviews/RV-48-review-of-m-7-round-2.md:52`, `:66`, `:106`, `:118`, `:130`
**Rubric:** 7 Tracker hygiene
**Severity:** Nit

The Retrospective says the guard "was taken again at the head (`3f9ba34`)". RV-48's Fix lines cite `bbd20d9`, `aeaecd3` and `3f9ba34`. None of these three commits is in milestone/M-7's history: `git merge-base --is-ancestor` fails for each. They were rebased into `343eb56`, `2bf6e40` and `3893cf6`, so once `finish` lands the milestone they point at nothing. The substance holds: `git diff 3f9ba34 HEAD -- src scenarios tests` is empty, so the tree the evidence was taken from is the head's code. The evidence README has the same issue but sits outside the repo.

**Outcome:** fixed

**Fix:** RV-48's Fix lines now cite the landed `343eb56`, `2bf6e40` and `3893cf6`. The guard's line in RV-48 and in the Retrospective names `3f9ba34` on `fix/RV-48` as the tree the evidence was taken from, and says its code is identical to the landed `3893cf6`. The Drive README is left as written, since it names the commit the tables were taken at (orchestrator)

### The Retrospective's round-2 entry does not say what was run on the RV-48 fixes

**Where:** `docs/milestones/M-7-materials-and-textures.md:273-277` (compare `:271`)
**Rubric:** 6 Tests are placed / 7 Tracker hygiene
**Severity:** Nit

For RV-47, *Before `finish`* lists the suites each fixer ran. The RV-48 bullet lists the findings, the fixers and the test's breaks, but not what ran. That includes whether server-regression, which now holds `RenderReleaseTests`, ran after the fix. The evidence README records it: a `Tier=regression` run of 584 tests passing at 3f9ba34, and the eleven suites queued there. The milestone record should carry it too, since it is the only round-2 run of the new regression test.

**Outcome:** fixed

**Fix:** the RV-48 bullet now says what ran at `3f9ba34`: the 12 suites through the queue, and server-unit and server-regression (584, with the new release test) carried from runs on that same commit. It also gives server-regression's base run at `307d692` and the round-3 reviewer's re-run of the two release tests (orchestrator)

## Answers checked

All 7 of RV-48's answers hold:

- **The release test:** it fails when the `Keep` call is deleted, given `_drawn` alone, or moved above `_drawn = _nextDrawn`. The `_materials`-alone mutant really is unreachable: `ShowMaterials`, `Make` and `Install` run synchronously, and `_nextDrawn` is always taken from `_materials`. The test is honest about what it compares. The Tiny package reference is within CLAUDE.md's rule, which binds `scenarios/Carve.Scenario.<Name>`, not this test project under `tests/`, and it reads a public engine service, not Carve internals.
- **The frame guard:** `head.sha256` and `base.sha256` have 229 lines each, and `diff` is empty. `server-base.sha256` and `server-head.sha256` have 120 lines each, and `diff` is empty. `68f810e` is an ancestor of `3f9ba34`, whose code equals the head's.
- **The other five:** T-80's frames record, the 16/6/3 count, T-78's Plan and As built, nine frames, and save's sentence all hold as answered.

## Summary

Round 3 of M-7's review. One reviewer, claude-opus-5-5, read RV-48 and the whole of `git diff d04aebe..HEAD`, and read `RenderSystem`, `PbrMaterials`, `LookHolder` and `RenderPump` to check the release sequencing. It also read CLAUDE.md's extension rules and the engine docs on residency and holder release, and diffed the evidence tables. It built `Carve.slnx` (0 warnings) and ran `RenderReleaseTests` and `PbrReleaseTests` (3 of 3 passing). All seven answers hold, and the diff adds no code defect. Its harness had refused an earlier reviewer's findings file, so it returned its findings in its report and the orchestrator copied them in. 2 Nits, both record-keeping, both fixed in documents only, so the code diff is unchanged.
