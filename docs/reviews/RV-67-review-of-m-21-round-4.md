---
description: >-
  Review of M-21, round 4
---
# RV-67 — Review of M-21, round 4

**Status:** Answered

**Reviews:** M-21

**Round:** 4

**Previous:** RV-66

**Diff:** 4590f1e1f6a3382d85a5d74ff5d387bd0b677a3b against 802e486

**Reviewer:** claude-opus-5-5 (one reviewer, checking RV-66's two answers)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-66's answers (claude-opus-5-5)

### T-131 says an unparseable file today gets "only the path line", but type errors are already told there

- **Where:** `docs/tasks/T-131-render-glb-tells-every-refused-weight-in-render-s-order.md:25` (rubric 7; RV-66 finding "With an unreadable `.glb`, `render_glb` drops a bad weight from its refusal, and with a type error it lists entries out of `render`'s order")
- **What is wrong:** The Scope says that when `loader.Parse` fails, the type, non-finite and out-of-range weight refusals are not told, and "today only the path line is told". `ReadTargets` adds type errors, and the clip and time problems, to `problems` (`src/Carve.Server/RenderGlbTool.cs:91`, `:161` onward), the same list `GlbScene.Prepare` adds the path line to (`src/Carve.Server/GlbScene.cs:66-73`), so those are already told. Only `weightRefusals` (out-of-range and non-finite weights) is dropped. The planned tests would still catch the real defect, and the order half of the Scope is accurate.
- **Consequence:** Whoever picks up T-131 is told a type refusal is missing when it is not.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** T-131's Scope now says only the out-of-range and non-finite weight refusals (`weightRefusals`) are dropped for an unparseable file, and that type, clip and time problems are already told with the path line (commit "RV-67: answered").

### The retrospective's round-3 line says findings 4 to 6 "held under the reviewer's own mutations on the real engine"

- **Where:** `docs/milestones/M-21-morph-targets.md:249` (rubric 7; RV-66 finding "The retrospective says RV-65 was "left Open for a round 3"; RV-65 is Answered")
- **What is wrong:** Only RV-65 findings 1 and 3 were tested by mutation. Findings 4 to 6 were document corrections, checked by reading. The line also says "A round 4 (RV-67) checked those two answers" in the past tense while RV-67 is Open, the same kind of line written ahead of the facts that RV-66's second finding was about.
- **Consequence:** A reader thinks the round-3 checks were stronger than they were, and reads round 4 as finished. Neither changes what ships.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** the round-3 line now says RV-65 findings 1 and 3 held under mutations and 4 to 6 by reading, and the round-4 sentence records RV-67's result rather than anticipating it (`docs/milestones/M-21-morph-targets.md`, commit "RV-67: answered"). By the person's ruling (2026-11-18), no round 5: only documents changed.

## Summary

- **Round 4, one reviewer (claude-opus-5-5),** checking RV-66's two answers. Both hold in substance. T-131 covers both halves of the defect with tests that would catch them, and the retrospective names it. The stale "Open" wording is gone. Each answer left one wording slip (above).
- `git diff 14ff762..HEAD` touches only `docs/`, so no product, test or `tests.json` change follows the last cited verify. `cairn lint` is clean.
- **Count:** 2 findings, both Nits, both in documents only.
