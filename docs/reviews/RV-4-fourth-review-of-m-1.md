---
description: >-
  Fourth review of M-1
---
# RV-4 — Fourth review of M-1

**Status:** Answered

**Reviews:** M-1

**Round:** 4

**Previous:** RV-3

**Diff:** fbd4e1e93189c606ab1f1156cf8b3dc089c51062 against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-3 fixes (claude-opus-5-5)

### A hole wound the same way as its outline is accepted, and its window is drawn filled in

`src/Carve.Core/Triangulator.cs:177`, doc comment at `:130-133`. Correctness (false success, regression from 3733904). The bridge skip accepts any two edges that are the same two points walked in opposite directions, without checking that the loop it leads into runs opposite to the outer loop. `[[0,0,0],[10,0,0],[10,10,0],[0,10,0],[0,0,0],[3,3,0],[7,3,0],[7,7,0],[3,7,0],[3,3,0]]` with `faces:[[0,1,2,3,4,5,6,7,8,9]]` was refused at 3733904, and at HEAD succeeds: triangles of area 116 (wall 100 plus the window again) against 84 for the correct window, drawing 101,956 px, the same as a solid quad. The same rule accepts small double-covered outlines that are not walls, e.g. `[[1,0],[3,1],[0,3],[1,0],[2,1],[0,3],[1,1],[2,1]]` (z=0); 188 of 1.5M random accepted outlines had winding number 2 somewhere. The description does not say how to wind a hole, and only the opposite-wound window is tested.

**Outcome:** fixed

## Summary

Round 4 of M-1, one reviewer on claude-opus-5-5, checking RV-3's answers and fixes (`git diff 3733904 HEAD`). Every RV-3 answer held, including the normalised view at every far and tiny case tried, margins on thin and flat meshes, and a 718-render race. The one finding is a regression from the bridge rule.
