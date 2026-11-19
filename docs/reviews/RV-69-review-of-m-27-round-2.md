---
description: >-
  Review of M-27, round 2
---
# RV-69 — Review of M-27, round 2

**Status:** Open

**Reviews:** M-27

**Round:** 2

**Previous:** RV-68

**Diff:** d5b85090847f22c5548010056d3ac4dc5c503c92 against a494e1f

**Reviewer:** claude-opus-5-5 (RV-68 answers on Carve.Hosting, Patina.Graphs and Patina.Server), claude-opus-5-5 (RV-68 answers on the scenarios and whole diff, and the changes since round 1)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-68's answers on Carve.Hosting, Patina.Graphs and Patina.Server (claude-opus-5-5)

I read each fix commit's diff and the tree at `defc7c9` and checked that every named test exists and asserts what its Fix line says. I built `Carve.slnx` (0 errors, so the narrowed `Carve.Hosting` surface holds against `Patina.Server` and every scenario) and ran `Patina.Graphs.Tests` (97 pass) and `Patina.Server.Tests` with `Tier!=regression` (100 pass). I re-ran the graph probes (unknown type, `albedo` role, `role: 5`, the `"éé"` column, a reference fault and a cycle behind a kind mismatch) against the built `Patina.Graphs.dll` from `probe-a/graphs`. For tests that should fail without their fix, I reasoned from the pre-fix code.

- FrameLog writes the same frame file from two threads at once: holds
- Three product commits after T-128 finished: does not hold (the As built cites pre-rebase hashes)
- More of Carve.Hosting is public than any second server reads: holds
- Test-local `EngineHost` hides `Carve.Hosting.EngineHost`: holds
- Validation throws when a node's input is a node of unknown type: holds
- An unknown output role adds a false "two outputs" fault: holds
- No built-in node is tested banded against whole: holds
- Write's order follows the caller's comparer: holds
- Load-error columns count UTF-8 bytes: holds
- export holds the graph lock and ignores cancellation: holds (copied under `Gate`, `SemaphoreSlim` turn taken with the token outside the `try`, token checked per band and before the encode)
- edit_graph lets a hidden fault through: does not hold (cycle case)
- patina-regression's inputs omit the sample graph: holds
- An export folder with an invalid character gives a bare SDK error: holds
- swatch takes an angle that overflows to infinity: holds
- Stale test name and As built line about the tool lists: holds

### The ruled kind check skips every input of a node on a cycle, so edit_graph still blames the fix for a hidden fault

**Where:** `src/Patina.Graphs/GraphValidator.cs:174`

**What:** `Kinds` returns before any check when the node is itself on a cycle (`if (a.Cyclic.Contains(id)) return;`). So a node whose input sits on a cycle never has its other inputs kind-checked, even when their sources exist and have a known type. The ruling says such inputs are kind-checked when another input of their node is at fault. The Fix line and T-110's As built say only that the input *whose source is on a cycle* is tried both ways. The early return goes further than either.

**Failure scenario:** Probed: take `c` = colour, `base` = blend {a: `c`, b: `lv`, mask: `c`}, `lv` = levels {in: `base`} and an output on `base`. This gives one fault, `base/b` "is on a cycle". The wrong colour `mask` gets no fault. Without the cycle (b from a colour `n`), the same graph gives `base/mask: takes greyscale, but 'c' gives colour`. With `b` naming a missing node instead, the mask fault shows beside the `b` fault, as ruled. So if an edit makes the cycle and a colour mask, edit_graph answers OK with "1 fault". The later `connect base b from n` that breaks the cycle is then refused, because edit_graph keys faults by place (`EditGraphTool.cs:94-98`): "1 new fault: node 'base' input 'mask' ...", on a port that edit never touched. That is the round-1 failure, reached through a cycle.

**Severity:** Minor

**Outcome:** —

### T-128's and T-111's As built name the refactor commits by hashes no ref holds

**Where:** `docs/tasks/T-128-swatch-sphere-lit-as-carve-renders.md:66-70`; `docs/tasks/T-111-shared-engine-host-library.md:98`, `:170`

**What:** The new As built text names `cabbfad`, `ee8608e` and `01cb4b2`. Those are the commits from before the rebase onto `a494e1f`. `git branch -a --contains` finds no branch holding any of them, and none is an ancestor of `HEAD`. On the branch the same commits are `5ebd511`, `f601bdf` and `c73fae1`.

**Failure scenario:** The round-1 finding was that nothing in the tracker points at these commits. A reader who follows the As built to `ee8608e` gets "unknown revision" on any other clone, and on this one after gc. The answer meant to make the commits traceable, and it points at objects the history does not hold.

**Severity:** Minor

**Outcome:** —

## Findings: RV-68's answers on the scenarios and the whole diff, and the changes since round 1 (claude-opus-5-5)

I read every cited fix commit, all on the branch, against the code: `ExportMatch.cs`, `ExportStage.cs`, `ExportToolTests.cs`, `SwatchStage.cs`, `PlanksScenario.cs`, the scenario csproj, `tests.json` and CLAUDE.md's Layout. I ran `ExportToolTests` (17 passed) and built the solution (0 warnings). For the second rebase I ran `git range-diff 802e486..7f8537b a494e1f..c853671` and diffed M-21's `RenderPump`/`RenderQueue`/`RenderSystem` against M-27's move of the plumbing into `Carve.Hosting`. M-21's morph `Targets` and `MorphWeights` survive. `TryPeek` stays virtual for `RenderPumpTests`, and 83 render-pump and target unit tests pass. `tests.json` removes none of main's lines.
- The 1024 export check cannot fail on a shifted, mirrored or flattened picture: holds for base colour, occlusion and the normal, both rulings as approved. It does not hold for roughness in `ExportStage` (first finding below).
- `tiled-shift` caught by the rolled labels: holds
- The planks `mirror` break mirrors the camera: holds
- `scenario-patina` has no `requires: ["vulkan"]`: holds
- `patina-regression`'s inputs miss `samples/graphs/**`: holds
- Scenario Patina references `Patina.Graphs`: holds. There is no reference in the csproj and no `using` of it, and the texels come from the planks PNGs and patina's `export`.
- The branch is not on `origin/main` (no change needed): holds. The merge base is `a494e1f`, and `scenario-morph` and core-unit's inputs are in `tests.json`.
- `samples/graphs/**` is listed twice: holds
- scenario-patina has no vulkan and a duplicate input (whole diff): holds
- CLAUDE.md's Layout lags the code: holds
- Carve.Scenario.Patina references Patina.Graphs (whole diff): holds

### The scenario's 1024 check never applies the approved rule to roughness, though the answer and T-115 say every fault is shown failing in both places

**Where:** `scenarios/Carve.Scenario.Patina/ExportStage.cs:40`, `:43`; `docs/tasks/T-115-png-export-at-a-given-size.md:86`; `docs/reviews/RV-68-review-of-m-27.md:266`

**What:** The ruling covers base colour, roughness and occlusion. `ExportStage` exports only `base_colour`, `normal` and `occlusion` at 1024, so that it can also test `outputs`. It applies `ExportMatch` and its faults to those three roles only. Roughness at 1024 is checked only in the in-process `ExportToolTests.The_matching_uv_rule_fails_a_shifted_a_mirrored_and_a_flat_picture`. The RV-68 fix says "Each fault is made on the 1024 picture and must fail, in `ExportStage` and in the new unit test". T-115's As built says "each is made and shown failing in both places", and it lists roughness's 1.0 good figure. For roughness in the scenario, neither is true.

**Failure scenario:** patina, run as the real process, writes a wrong `roughness.png` at 1024, for example shifted or packed against a different channel at large sizes only. `scenario-patina` passes, because it never exports roughness at 1024. Only `patina-unit`'s harness test, on the same export code but not through the real process, would catch it. A reader of T-115 believes the scenario holds it.

**Severity:** Minor

**Outcome:** —

### ExportMatch's summary cites a one-stave shift fault that `Faults` does not make

**Where:** `scenarios/Carve.Scenario.Patina/ExportMatch.cs:7`

**What:** The class summary gives "a one-stave shift 35" among the rule's measured faults. `Faults` makes no one-stave shift for base colour or roughness, only a half-tile shift (in x and y), a u-mirror and a flat map. Neither check shows that fault failing.

**Failure scenario:** a later change loosens `Tolerance` and still fails the three made faults. A reader of the summary takes the one-stave shift as checked, but nothing shows it caught.

**Severity:** Nit

**Outcome:** —

## Summary

Two reviewers, both Opus 5.5, side by side. Round 1 (RV-68) reviewed against 802e486; since then the milestone was rebased onto `main` at a494e1f (M-21) and RV-68's 26 findings were answered. One reviewer checked the 15 answers on `Carve.Hosting`, `Patina.Graphs` and `Patina.Server` against the code; the other the 11 on the scenarios and the whole diff, the person's three rulings, and everything round 1 did not see (the second rebase, the fix commits as a whole).

23 of RV-68's 26 answers hold. 4 findings: 0 Blocker, 0 Major, 3 Minor, 1 Nit: the validator still skips the kind check on a node on a cycle; T-128's and T-111's As built cite pre-rebase commit hashes; the scenario's 1024 check never applies the rule to roughness; `ExportMatch`'s summary names a fault it does not make. The rebase lost nothing of M-21.
