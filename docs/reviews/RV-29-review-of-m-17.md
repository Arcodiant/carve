---
description: >-
  Review of M-17
---
# RV-29 — Review of M-17

**Status:** Open

**Reviews:** M-17

**Round:** 1

**Previous:** —

**Diff:** af34d07159e0a50d93864024c56e5651fca3f3c6 against origin/main

**Reviewer:** claude-opus-5-5 ×3 (the weights side; the render side; the whole diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the weights side, T-37, T-38 and T-41's scenario (claude-opus-5-5)

### `get_weights`' worked example is tested on a different model state from the one its description gives

- **Where:** `src/Carve.Server/GetWeightsTool.cs:22-23`, `tests/Carve.Server.Tests/GetWeightsToolTests.cs:35-49`.
- **What is wrong:** the description runs the example "after set_weights binds vertices 0 and 1 of a cube wholly to a bone Head"; the test never makes that call and asserts the unweighted state (8 unweighted; no vertex follows 'Hips', 'Head', 'Tail'). T-38's "regression for the worked examples as written" is not met, and the description shows no expected output. The same defect as the whole-diff reviewer's third finding.
- **Failure:** a regression in the by-vertex read of a weighted vertex, or in the idle-bone note, passes this test.
- **Suggested fix:** do the `set_weights` first; assert vertex 0 reads `{Head: 1}`, vertex 7 `{}`, "6 vertices have no weight: 2, 3, 4, 5, 6, 7", and the note names 'Hips', 'Tail'. Or change the description's premise.

**Outcome:** fixed

Fixed in 3a15bc4. The test runs `set_weights {"vertices":[0,1],"weights":{"Head":1}}` first and compares the whole result; the description names the bones and shows the `Result:` line.

### `weigh`'s `bones` argument is never run successfully through the tool, and two of its documented errors are never tested

- **Where:** `src/Carve.Server/ModelStore.cs` (`Weigh`, names to indices), `src/Carve.Server/WeighTool.cs:49-62`; tests `NamedMeshTests.cs:82`, `ChainPose.cs:181-182`.
- **What is wrong:** the `bones` ruling is unit-tested in Core only, with indices. Every server `weigh` call leaves `bones` out or names an unknown bone, so the name-to-index mapping is never run. "No skeleton yet" and "an empty bones list" are tested nowhere.
- **Failure:** replace `indices` with `null`, or add `i + 1`, in `ModelStore.Weigh`, and every test still passes; drop the no-skeleton check and `AutoWeights.Segments` throws on null, a crash rather than a tool error.
- **Suggested fix:** a server test on the Hips/Head/Tail chain: `weigh {"bones":["Head"]}`, assert the counts, read back with `get_weights` that only the vertices nearest Head are weighted, wholly to Head. Add `weigh` with no skeleton and with `"bones":[]`, asserting a tool error and no new undo step.

**Outcome:** fixed

Fixed in f4cec67. `GetWeightsToolTests` runs `weigh {"bones":["Head"]}` on the Hips/Head/Tail chain (4 weighed, 0 kept, 4 unweighted; `get_weights` reads 2, 3, 6, 7 wholly Head, the rest empty), and `weigh` with no skeleton and with `"bones":[]` is a tool error with no new undo step.

### No test checks that a leaf's segment is half its parent's length

- **Where:** `src/Carve.Core/AutoWeights.cs:40`, `tests/Carve.Core.Tests/AutoWeightsTests.cs`.
- **What is wrong:** every leaf check passes for any extension of at least 0.4; elsewhere the nearest point on the leaf is its head. The tie-to-lower-index rule in `Nearest` (strict `<`) under `bones` is untested too.
- **Failure:** change `* 0.5` to `* 1.0` or `* 2.0`, or `d < bestD` to `d <= bestD`, and all nine `AutoWeightsTests` pass.
- **Suggested fix:** a point at `(0, 1.8, 0)` on `TwoBones`, distance 0.3 to the leaf, weights per `(0.3/0.8)^4`; and a `bones` case with a vertex equidistant from a named and an unnamed bone (the joint at `(0, 1, 0)` with `bones: [1]`), which must be out of scope.

**Outcome:** fixed

Fixed in 5a377ae. (0, 1.8, 0) on `TwoBones` weighs the half-length leaf, and the joint (0, 1, 0) with `bones: [1]` is out of scope; `* 1.0` and `<=` each fail a test (mutations reverted).

### `Math.Pow` makes "bit for bit" determinism depend on the platform

- **Where:** `src/Carve.Core/AutoWeights.cs:73`.
- **What is wrong:** D-6 promises the same weights bit for bit. Everything else in the path is IEEE-exact, but `Math.Pow` calls the C runtime's `pow`, which can differ in the last ulp between Windows' UCRT and glibc. The test compares two runs in one process.
- **Failure:** the same mesh weighed on Windows and on a Linux host can differ in the last bit, so a saved `.carve` or `.glb` differs across machines. Latent today.
- **Suggested fix:** `var r = nearest / d[k]; var r2 = r * r; w = r2 * r2;`.

**Outcome:** fixed

Fixed in 8e89b67. `FourthPower(r)` is `r2 = r * r; r2 * r2`, IEEE-exact. fixed: 8e89b67, restored by 859ff2a on the person's ruling (2026-11-11); every frame and .glb byte-identical, gardener-weighed.carve changes in the last bit of 507 numbers

### `scenario-rig`'s inputs leave out a file its project links

- **Where:** `tests.json` (suite `scenario-rig`), `scenarios/Carve.Scenario.Rig/Carve.Scenario.Rig.csproj`.
- **What is wrong:** the csproj links `../Carve.Scenario.Character/GardenerPixels.cs`, which the suite's inputs leave out. The same defect as the whole-diff reviewer's second finding.
- **Failure:** an edit to `GardenerPixels.cs` that breaks the Rig build can carry `scenario-rig`'s earlier pass.
- **Suggested fix:** add it to the inputs, or drop the unused link.

**Outcome:** fixed

Fixed in 3bc670e. The unused `GardenerPixels.cs` link is dropped from the Rig csproj, so the suite's inputs cover every file it compiles.

Covered: `src/Carve.Core/AutoWeights.cs`, `VertexWeights.cs`, `PolygonMesh.cs` and `Model.cs` (weights parts); `src/Carve.Server/WeighTool.cs`, `GetWeightsTool.cs`, `ModelStore.cs` (diff, `DefineSkeleton`, `PushLocked`), `Program.cs`; tests `AutoWeightsTests`, `GetWeightsToolTests`, `NamedMeshTests`, `EditToolTests` (diff); `RigScenario.cs` and its csproj; the weigh parts of `ChainPose.cs` and `CharacterScenario.cs`; `tests.json`; T-37, T-38, T-41; D-6's Weights diff; the milestone retrospective. Found sound: normalisation and the cap of four with ties to the lower index; distance-0 sharing; several-children segments; the `bones` scope and `replace`/keep follow the rulings; `weigh` is one undo step; `get_weights`' summary, warnings and errors; an empty `weigh` result does not mark a mesh as weighted.

## Findings: the render side, T-39 and T-40 (claude-opus-5-5)

### After a `weights` render, a model that lacks that bone makes every later render fail

- **Where:** `src/Carve.Server/RenderPump.cs:126` (`WeightHeat.Of` in `ShowMesh`), with `RenderPump.cs:66` (the `_meshFault` check) placed before `RenderPump.cs:100` (the `Weights` check).
- **What is wrong:** `_shownWeights` stays set after a weights render is served. On every later model version the pump calls `ShowMesh(current)` with it, and `WeightHeat.Of` throws if that model has no skeleton or no bone of that name. The catch sets `_meshFault`, which fails every queued request before the step that would clear `_shownWeights`. A stale pose has no such problem (`Skinning.Posed` ignores unknown bones). It can also happen with no edit, if a weights call races an edit: the bone is checked against `current` in RenderTool, then the pump shows a newer `store.Current`. The same defect as the whole-diff reviewer's first finding.
- **Failure:** `render {weights:"Elbow_L"}`, then `open` of a file without Elbow_L, `undo` past `define_skeleton`, or a renaming `define_skeleton`; then `render {}` and every render after it fail with "weights: no bone named 'Elbow_L'…".
- **Suggested fix:** in `ShowMesh`, compute the heat only when the snapshot's skeleton has that bone, else show the mesh shaded; fail only the weights request itself, re-checked at the head of the queue as target and highlight are. Add a `RenderPumpTests` case: weights request, then a model with no skeleton, then a plain request that must be served.

**Outcome:** fixed

Fixed in fdf17f2. `ShowMesh` shows the mesh shaded when the weights bone is gone, and the pump fails only a weights request whose bone is missing, re-checked at the head of the queue; `RenderPumpTests` cover `undo` past `define_skeleton` and a renaming `define_skeleton`, then a plain render served.

### Several named "mirror" and "flip" checks don't test the transform they name

- **Where:** `scenarios/Carve.Scenario.Crate/ChainBones.cs:61`, `scenarios/Carve.Scenario.Crate/ChainWeights.cs:65-66`.
- **What is wrong:** the chain is framed on rest bounds y 0..2 and the quads on x −0.9..1, so a y-flip maps y to `2 − y` and an x-mirror is about x = 0.05. ChainBones:61 tests `1 − d.Y·0.5` (y ≈ 0.65), which has no magenta under the flip either, so it passes on a flipped image. ChainWeights:65 and :66 mirror and flip the corner inside the quad, not where a camera mirror or flip would put it. Camera mirror and flip are still caught elsewhere (ChainBones' positive bent-frame `Line` checks; ChainWeights' middle-quad green and magenta checks), so the spec point is met, but the labels and the evidence table misname which check guards what.
- **Failure:** with the overlay flipped in y, ChainBones:61 still passes; if the positive bent-frame checks were removed, nothing would test the y-flip there.
- **Suggested fix:** ChainBones:61 uses `(d.X·0.5, 2 − d.Y·0.5)` or derives it from `RestMin.Y + RestMax.Y`; relabel ChainWeights:65-66 as corner-order checks and name the middle-quad and magenta checks as the camera guards.

**Outcome:** fixed

Fixed in ec42db3. ChainBones tests the y-flip at `RestMin.Y + RestMax.Y - d.Y * 0.5`, shown failing on an overlay flipped in y with the other bones checks taken out (the old point passed there); ChainWeights' red corner checks are relabelled as corner order and the middle-quad green and corner magenta named as the camera guards; T-39's and T-40's As built updated.

Covered: T-39 and T-40 task files, D-6's leaf paragraph; `src/Carve.Core/BoneLines.cs`, `Skinning.cs`; `src/Carve.Server/BoneOverlay.cs`, `LabelLayout.cs`, `LookMeshes.cs`, `RenderPump.cs` (whole), `RenderQueue.cs`, `RenderSystem.cs`, `RenderTool.cs`, `WeightHeat.cs`, and for context `LookHolder.cs`, `MeshBridge.cs`, `SkeletonTools.cs`, `OpenTool.cs`; tests `BoneLinesTests`, `BoneOverlayTests`, `BoneRenderToolTests`, `WeightsLookTests`, `EditToolTests`; scenario diffs of `ChainBones.cs`, `ChainWeights.cs`, `ChainPose.cs`, `CharacterScenario.cs`. Found sound: posed heads match `SkinTransforms`; `bones` and `weights` take the clip's sampled pose; the `mode` refusal; heat stops, clamping and corner order (A, C, B); frames without the new arguments take the old path; the weights → plain fallback.

## Findings: the whole diff (claude-opus-5-5)

### A `weights` render leaves the pump holding a bone name, and once that bone (or the skeleton) is gone, every later render fails

- **Where:** `src/Carve.Server/RenderPump.cs:126` (`WeightHeat.Of(snapshot.Model, _shownWeights)` in `ShowMesh`), with the order of checks at `RenderPump.cs:66` and `:100`. The throw is in `src/Carve.Server/WeightHeat.cs` (`Of`).
- **What is wrong:** `_shownWeights` is the bone from the last `weights` render. Every model change runs `ShowMesh(current)`, which works out the heat from that stale name against the new model. If the new model has no skeleton, or no bone of that name, `WeightHeat.Of` throws. The catch then sets `_meshFault`. The next request is failed by the `_meshFault` check at line 66, which comes before the check at line 100. That check is the only place `_shownWeights` is reset. So the stale name is never cleared, and every later edit throws again. The pose path has a guard (`snapshot.Model.Skeleton is not null`). The weights path has none.
- **Failure:** `define_mesh`, `define_skeleton`, then `render {"weights":"Root"}`. Then `undo` (the skeleton is gone), or `open samples/leaky-shed.carve`, or `define_skeleton` with that bone renamed. Then a plain `render {}` fails with "The current mesh could not be put in the scene: weights: no bone named 'Root'…". Every render after that fails the same way, even after further edits.
- **Suggested fix:** in `ShowMesh`, work out the heat only when the snapshot's skeleton has `_shownWeights`; otherwise pass no heat. Or clear `_shownWeights` before the `_meshFault` check. Add a `RenderPumpTests` case: weights render, then the skeleton removed, then a plain render succeeds.

**Outcome:** fixed

Fixed in fdf17f2, the same as the render side's first finding.

### `scenario-rig` inputs miss `GardenerPixels.cs`, which the Rig project compiles

- **Where:** `tests.json:80` (suite `scenario-rig`), against `scenarios/Carve.Scenario.Rig/Carve.Scenario.Rig.csproj:20`.
- **What is wrong:** the Rig csproj links four files from other scenario folders (`CrateBuild.cs`, `CameraPixels.cs`, `GardenerSpec.cs`, `GardenerPixels.cs`); the suite lists only the first three. `dotnet-closure` does not follow linked files. `RigScenario.cs` never uses `GardenerPixels`, but it is compiled into the Rig assembly. The CLAUDE.md Rig row and T-41's As built name three linked files.
- **Failure:** a change to `scenarios/Carve.Scenario.Character/GardenerPixels.cs` leaves the `scenario-rig` fingerprint unchanged, so a stale pass carries, including a change that breaks the Rig build.
- **Suggested fix:** drop the unused `GardenerPixels.cs` link from the Rig csproj, or add it to the suite's inputs and the CLAUDE.md row.

**Outcome:** fixed

Fixed in 3bc670e, the same as the weights side's fifth finding.

### `get_weights`'s worked example states a setup that neither the test nor the description's own reading runs

- **Where:** `src/Carve.Server/GetWeightsTool.cs:22`, against `tests/Carve.Server.Tests/GetWeightsToolTests.cs:35-49`.
- **What is wrong:** the description introduces `{"vertices":[0,7]}` as run "after set_weights binds vertices 0 and 1 of a cube wholly to a bone Head". The test meant to run the worked example "as written" (T-38 Scope) runs it on an unweighted cube and asserts vertex 0 reads `{}` and 8 vertices are unweighted.
- **Failure:** an agent reading the description expects `{"vertex":0,"weights":{"Head":1}}`. The only check of the example asserts the opposite state, so the claimed setup is unverified.
- **Suggested fix:** call `set_weights {"vertices":[0,1],"weights":{"Head":1}}` before the example in the test and assert vertex 0 is `{"Head":1}`, vertex 7 is `{}`, 6 unweighted. Or reword the description.

**Outcome:** fixed

Fixed in 3a15bc4, the same as the weights side's first finding.

### `render`'s list of tools that work without Vulkan still leaves out `weigh`

- **Where:** `src/Carve.Server/RenderTool.cs:234`; also lines 229 and 51.
- **What is wrong:** b72eb37 added `get_weights` to "…still work there", but `weigh` (pure CPU) was left out. The text also says a pose is "skinned by the weights set_weights gave", though `weigh` now gives them too.
- **Failure:** an agent on a machine with no Vulkan device concludes `weigh` is unavailable, though it works.
- **Suggested fix:** add `weigh` to the list; say "the weights set_weights or weigh gave" in both places.

**Outcome:** fixed

Fixed in 3a8f45e. `weigh` is in the "still work there" list, and both places say "the weights set_weights or weigh gave".

Covered: the whole diff, with full reads of `src/Carve.Core/{AutoWeights,BoneLines,Skinning}.cs`, `src/Carve.Server/{WeighTool,GetWeightsTool,ModelStore,Program,RenderTool,RenderPump,RenderQueue,RenderSystem,BoneOverlay,WeightHeat,LookMeshes,LabelLayout}.cs`, `tests.json`, `Carve.slnx`, CLAUDE.md, the changelog, D-6's diff, every csproj's `Link=` items, `GetWeightsToolTests`, the milestone doc and the As built of T-37 to T-41. Checked with no finding: `AutoWeights` and `BoneLines` agree on segments; `get_weights`, the `weights` look and `weigh` read the same `VertexWeights`; the changelog and D-6 match what was built.

## Summary

The diff (41 files, about 2,100 lines, 33 commits over `origin/main`, after the refactor pass at b72eb37) was split three ways, each read by an Opus 5.5 reviewer who wrote none of it:

- **The weights side:** T-37, T-38 and T-41's scenario.
- **The render side:** T-39 and T-40. The render queue and pump are the risk surface there.
- **The whole diff:** for what falls between members.

Eleven findings, of which three were found twice, by different reviewers:

- **The stuck-render bug:** whole #1 and render #1.
- **The `scenario-rig` linked input:** whole #2 and weights #5.
- **The worked example:** whole #3 and weights #1.

The most serious is the stuck render. After a `weights` render, any model without that bone fails every later render.
