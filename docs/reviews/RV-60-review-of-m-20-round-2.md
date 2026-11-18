---
description: >-
  Review of M-20, round 2
---
# RV-60 — Review of M-20, round 2

**Status:** Answered

**Reviews:** M-20

**Round:** 2

**Previous:** RV-59

**Diff:** 47ff3d55c010983bc8eb6392d79e50500c214ad1 against 7e13279

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

### The family test never checks `Refused`, and five calls meant to succeed are refused

- **Where:** `tests/Carve.Server.Tests/ToolFamiliesThroughProcessTests.cs:30,35,36,50-56` (rubric 1, 2)
- **What is wrong:**
  - The loop deconstructs `refused` but never uses it. Nothing asserts `IsError`, so the test still checks only that the process and the harness return the same JSON. That is the exact gap RV-59 reported. RV-59's `**Fix:**` says the test "asserts `IsError` equals the expected value on both sides", but no such assertion exists.
  - I ran it in a scratch copy with `IsError(harness result) == refused` logged. These calls meant to succeed are refused on both sides:
    - cut: `merge_faces {"faces":[7,1]}` ("faces[7] shares no edge … with faces[1]");
    - skeleton, weights and clips: `define_clip` with `CarveFileVersion2Tests.Wave` ("no bone named 'joint'; the bones are 'Hips', 'Head'"). `Chain` has no `joint` bone.
    - save, open and export: `set_weights {"weights":{"Hips":1}}` ("give one of vertices or group");
    - save, open and export: both `export {"path":"out/m.glb"}` calls ("8 vertices have no weight … Nothing was written").
  - Once the first export succeeds, the second one, with no `overwrite`, should be a refusal, but it is not marked `Refused`.
- **Consequence:** the success paths of `merge_faces`, `define_clip` and `export` never run through the process. No `.glb` is ever written, so the new two-way listing and byte comparison cover only `m.carve`. Export is the family most likely to differ between the process and the harness, because of the working folder and `ToolFolder`. A difference there still passes. The edit, bridge, delete-and-select, shading and uvs calls do succeed.
- **Severity:** Major

**Outcome:** fixed

**Fix:** `ToolFamiliesThroughProcessTests` now asserts, for every call and on both sides, that `IsError` equals its `Refused` flag, with a message naming the family, the tool, its arguments, which side, what happened and what was expected (e.g. `cut: merge_faces {"faces":[1,7]} succeeded on the harness; it should have refused`). Arguments were taken from each tool's description. Every call not labelled succeeds on both sides:
  - cut: `loop_cut {"edge":[0,3],"at":[0.5]}` then `merge_faces {"faces":[1,7]}`, `merge_faces`'s own worked example (the two front pieces share the cut edge);
  - skeleton, weights and clips: `define_clip` with the wave clip keyed on `Head`, a bone `Chain` has;
  - save, open and export: `set_weights` on all eight vertices (`Hips`), a `define_clip` on `Head`, then a skinned `export {"path":"out/m.glb"}` and a second `export` with `"overwrite":true` (it replaces the file).
  The two labelled deliberate refusals are the ones that were there: cut's `merge_faces {"faces":[0,1]}` (not connected) and bridge's 4-vs-3 loops. The test also asserts `out/m.glb` is in the harness listing whenever a family exports, and the listings must be equal, so the process must write it too. Checks run here: flipping the succeeding `merge_faces {"faces":[1,7]}` to `Refused: true` failed the test with the message above (reverted); a temporary print of both listings for the export family gave `harness: m.carve, out\m.glb | process: m.carve, out\m.glb` (reverted). No product change was needed. Nothing else in the test was loosened.

### server-regression is still recorded as 228 tests; it is 223

- **Where:** `docs/tasks/T-89-tool-behaviour-checks-as-unit-tests.md:83,88`; `docs/tasks/T-90-server-regression-at-landing-only.md:45`; `docs/milestones/M-20-fast-server-tests.md:76` (rubric 7)
- **What is wrong:**
  - T-89 counted 746 unit and 228 regression before its rebase onto T-88.
  - The rebase brought in five of T-88's moves out of classes T-89 keeps `regression`. They now sit in unit classes:
    - `NamedMeshTests` → `NamedMeshDefinitionTests`: `The_worked_example_moves_the_lid_alone_and_leaving_mesh_out_is_refused_with_the_names` and `Every_tool_refuses_an_unknown_argument_and_lists_exactly_the_arguments_its_schema_has`;
    - `RenderTests` → `RenderDefinitionTests`: `The_tool_lists_its_views_and_says_what_missing_and_empty_mean`, `The_description_says_what_the_new_arguments_do` and `The_tool_describes_its_looks`.
  - T-89 updated unit to 751 but left regression at 228. T-90 and the M-20 table copied that figure.
  - The 223 the fix agent reports is right: 751 unit (run here at 3c03805) + 223 = 974.
- **Consequence:** the record gives a regression total five too high, and the before/after sum in T-89 (746 + 228) mixes two trees. No test is lost.
- **Severity:** Nit

**Outcome:** fixed

**Fix:** T-89 (Timings, After the rebase, Not checked), T-90 (Before and after) and the M-20 retrospective table now give server-regression as 223, each saying why: 228 was counted before T-89's rebase onto T-88 moved five more definition tests to unit (two from `NamedMeshTests`, three from `RenderTests`). T-89's sum reads unit 751 + regression 223 = 974, with 746 + 228 named as the pre-rebase pair.

## Summary

Reviewed `git diff 4499dd9 3c03805` (docs/reviews left out), the answered RV-59, and the tree at 3c03805. `server-unit` passes in a scratch copy: 751 tests. `cairn lint` is clean.

RV-59 answers:
- render_glb engine fault first (no change needed): holds. It is recorded in T-87's As built.
- IModelGeometryLoader via the service provider (fixed): holds. `GetService` returns a tool error naming the loader, and the order is commented.
- `Rooted` vs `ToolPaths.Resolve` (fixed): holds. `GetFullPath(relative, baseDirectory)` matches `Resolve`, and a null base returns its input unchanged.
- Harness leak on helper throw (fixed): holds. Every `new ToolHarness` outside a `using` is now wrapped in `try`/`catch { await DisposeAsync(); throw; }`. The catch rethrows and the harness is never returned, so there is no double dispose and no swallowed exception. It is 27 helpers, not 28: the 28th is the `NamedMeshDefinitionTests` copy, which was removed.
- server-unit sample inputs (fixed): holds. `leaky-shed.carve` and `gardener.carve` were added. The other unit classes read only Fixtures, the closure's own sources, csprojs and the out-of-scope `README.md`.
- Rule's list incomplete (fixed): holds. CLAUDE.md names the loader read-back, and T-104's Verify is `server-unit, server-regression`.
- Layout row (fixed): holds.
- T-90 survey missed four items (fixed): holds. 40d3794 is an ancestor of T-90's commit. T-94, T-103, T-104 and T-108 all keep `server-regression`. The retrospective is corrected.
- csproj comment and FileToolTests summary (fixed): holds. The linked-copies bullet matches the csproj (no `ToolPaths.cs`/`GameLimits.cs`).
- Family test compares refusals (fixed): **does not hold.** See the first finding.
- Written files compared one way (fixed): holds as written: both listings, then bytes. Its reach is limited by the first finding, since no `.glb` is ever written.
- Third crate-and-lid copy (fixed, with the FileToolTests near-duplicate skipped): holds. The skip reason is sound.

Count: the fixes add, remove and re-tier no test. `[Fact]`/`[Theory]` methods (556), `InlineData` rows (391) and each class's tier are identical at 4499dd9 and 3c03805. The 5 between 228 and 223 come from T-89's rebase onto T-88, not from the fixes; see the second finding.
