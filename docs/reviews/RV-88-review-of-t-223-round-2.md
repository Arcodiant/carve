---
description: >-
  Review of T-223, round 2
---
# RV-88 — Review of T-223, round 2

**Status:** Answered

**Reviews:** T-223

**Round:** 2

**Previous:** RV-85

**Diff:** 22aef69971985f6e105f95832c18d15a7e700b46 against origin/main

**Reviewer:** a fresh Opus 5.5 subagent

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### A vertex whose face normals cancel is reported as "on no face"

`src/Carve.Core/Displace.cs:133-136` and `:157`. The new check treats any vertex with `normals[v] == Vector3.Zero`
as one on no face, but a zero sum also comes from faces whose area-weighted normals cancel, which a valid mesh can
have: a double-sided card (a quad and the same quad reversed; `PolygonMesh.Create` accepts it, and `check` only
reports it as `duplicate-face`). Failing input, run against Carve.Core from a scratch console: vertices
`(0,0,0) (1,0,0) (1,1,0) (0,1,0)`, faces `[0,1,2,3]` and `[3,2,1,0]`, then
`Displace(mesh, null, new NoiseSource(7, 0.5, 4), 1, 0, null, ...)`. This is the whole-mesh call, which picks only
vertices on live faces, and it is refused with `selection: matches no vertex with a normal (they are on no face)`.
Add a third quad `[1,4,5,2]` next to it and the call succeeds with the warning `vertices 0, 3 are on no face, so they
have no normal and did not move`, though both are on two faces. Leaving the vertices out is right; the text is wrong.
It should say the vertex has no normal (it is on no face, or its faces' normals cancel), or work out which case it is
from `mesh.LiveFaces`.

Fix: the warning, the refusal and the description now say the vertex has no normal, on no face or with its faces facing opposite ways.

**Outcome:** fixed

## Summary

Round 1's three fixes hold. One new finding, the wording for a vertex whose faces' normals cancel, fixed.
