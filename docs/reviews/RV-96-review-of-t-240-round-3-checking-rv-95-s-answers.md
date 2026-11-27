---
description: >-
  Review of T-240, round 3: checking RV-95's answers
---
# RV-96 — Review of T-240, round 3: checking RV-95's answers

**Status:** Answered

**Reviews:** T-240

**Round:** 3

**Previous:** RV-95

**Diff:** a1e604a26725b96ba19d594c9d165bf01fa33dc9 against origin/main

**Reviewer:** claude-opus-5-5 (fresh agent)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

No new or unresolved findings: every RV-95 answer holds.

## Summary

I read `git show HEAD` (bd35f184) and the code each RV-95 answer touches: `GraphValidator.ReadImage` and the `Files` summary, `GraphSlotBake.Read` and `IsGraphFile`, the new test and the changed expected string.

- **Type list:** `image, ` is in `AnUnknownTypeListsTheTypes`'s expected string, in alphabetical place; the suite is green.
- **Files allocation test:** holds. With `budget.ListOnly ||` gone, `Files` takes the `File.ReadAllBytes` + `PngReader.Decode` path for `big.png` (nothing is cached in a fresh `NodeBudget`), and a 4096x4096 RGBA decode is at least 64 MiB on the calling thread, so the first `InRange(0, 16 MiB)` fails. The decode runs on the calling thread, so `GetAllocatedBytesForCurrentThread` sees it. The second assertion shows the measurement is real.
- **Unreadable PNG wording:** holds. The `Files` summary says an unreadable PNG is listed though validation faults it. `Read` now calls a non-`.texgraph` file an "image" file. The top-level `path` and nested graphs still end in `.texgraph` and keep "graph" (`GraphSlotToolTests:194` still passes). No test pins the new "image" wording. It is a one-word message change, so I don't file it.
- **As built:** `Files` is out of the parenthesis and `Images` is named. `Images` runs a full analysis, so it does decode, and the sentence is correct.

Ran: `dotnet test tests/Patina.Graphs.Tests` (586 passed), `dotnet test tests/Carve.Graphs.Tests` (45 passed), `dotnet test tests/Patina.Server.Tests --filter Tier=unit` (195 passed, 6 skipped), `dotnet test tests/Carve.Server.Tests --filter "FullyQualifiedName~GraphSlot"` (20 passed). No probes were written.
