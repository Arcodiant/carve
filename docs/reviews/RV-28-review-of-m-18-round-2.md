---
description: >-
  Review of M-18, round 2
---
# RV-28 — Review of M-18, round 2

**Status:** Answered

**Reviews:** M-18

**Round:** 2

**Previous:** RV-27

**Diff:** 137a78516eff2bb0f189a77720ebf64de0db45b4 against origin/main

**Reviewer:** claude-opus-5-5 (one reviewer, checking every RV-27 answer and the fix diff `46da626..65302d3`)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings: the RV-27 answers and fixes (claude-opus-5-5)

### `server-unit` and `server-regression` still do not fingerprint the linked `CameraPixels.cs`

- **Where:** `tests.json:23-56`; the link is at `tests/Carve.Server.Tests/Carve.Server.Tests.csproj:50`, used at `NamedMeshTests.cs:248`.
- **What is wrong:** RV-27's fix added hand-listed links to `scenario-soft` and `scenario-character` only. The server suites list only `TestMeshes.cs`, so a change to `CameraPixels.Perspective` carries their old pass. The gap dates from main (`e6d294e`, RV-24), not M-18.

**Outcome:** skipped: outside M-18; the gap predates it on main. It is filed as T-73.

Every other RV-27 outcome holds, as the reviewer checked one by one:
- the B/C order against `MeshBridge`;
- `FlatFaces`, which cannot leak through undo, save or posing;
- the ruling 3, skinned-normal, `MarkEdges`, cap, dart, lone vertex, triangle and budget tests, each of which fails under its mutation;
- unweighted-stays-unweighted at levels 2 and 3;
- the soft fingerprinting and the CLAUDE.md rows;
- the retrospective;
- the not-planar count against `select`.

`core-unit` passed and `cairn lint` was clean. Not checked by this reviewer: `server-*` and the scenarios fresh after 54d15f3 (the `finish` verify runs them), and the changed frames (the person approved them).

## Summary

Round 2 is one reviewer over RV-27's answers and the fix diff. It found one new finding, which predates M-18, was skipped, and is filed as T-73. Nothing in M-18 was fixed in this round, so no third round is needed.
