---
description: >-
  Review of T-185
---
# RV-86 — Review of T-185

**Status:** Open

**Reviews:** T-185

**Round:** 1

**Previous:** —

**Diff:** add1224c5f9f60d13bfc36691bb71cefba5c468c against origin/main

**Reviewer:** Claude Opus 5.5 (claude-opus-5-5), a code-review subagent reading the diff and its neighbours, with one break run in a scratch worktree

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### CLAUDE.md's layout table does not name the marble sample
- **Where:** CLAUDE.md, the layout table rows for `scenarios/Carve.Scenario.Patina` (the `LibraryStage.cs` clause, "every M-28 sample (cobblestones, bricks and its grey variant by `parameters`, fabric, painted-metal, floor-tiles, old-wall, grime)"), `tools/Patina.SampleGraphs` ("`bricks.texgraph` and `fabric.texgraph` (T-136) in the same way") and `tests/Patina.Graphs.Tests` ("the bricks and fabric samples held equal to their PNGs and to check's rules (`PatternSamplesTests.cs`)").
- **Severity:** Gap
- **What is wrong:** The change adds `marble` to `LibraryStage`'s list, to the samples `tools/Patina.SampleGraphs` writes, and to `PatternSamplesTests` (with a new `TheMarbleVeinsAreThinDarkLines`), but CLAUDE.md is not in the diff. All three rows still list the old samples, and the `LibraryStage` row calls its list "every M-28 sample", which marble (T-185, off-milestone) is not. The README was updated; the layout table was not. Inferred by reading CLAUDE.md against the diff.

**Outcome:** fixed: marble named in the three rows (the Patina scenario, `tools/Patina.SampleGraphs`, `tests/Patina.Graphs.Tests`)

### The README's Samples paragraph line was not rewrapped
- **Where:** README.md, `### Samples`, the line "variant), `fabric`, `marble` (veins by `blend`'s `difference`), `painted-metal`, `floor-tiles`, `grime` (a filter graph with".
- **Severity:** Nit
- **What is wrong:** The inserted text makes that line about 125 characters wide, in a paragraph wrapped at about 80 like the rest of the file, including the Filters bullet this same change rewrapped. Inferred by reading.

**Outcome:** fixed: the paragraph rewrapped at 80

## Summary

- `BlendNode`: `difference` is `MathF.Abs(a - b)` in `Combine`, so opacity, mask and colour per channel come through `Apply` like every other mode. The enum lists it, and the catalogue gets its mode list from the registry. No other code lists blend's modes; only older task files do, and they are history.
- Tool descriptions: the `blend` catalogue line is only in `nodes`' output (`NodeCatalogue.Text`/`Entry`). The descriptions use `Index` (the gists) and the `stripes` entry, so the 3,686-character limit in `NodeToolTests` is not affected.
- Tests, checked by a break in a scratch worktree (since removed). I set `"difference" => a - b` and ran `dotnet test tests/Patina.Graphs.Tests` filtered to the blend, pattern-sample, broken-copy, tiling and size-invariance tests. It failed `BlendDifferenceIsTheDistanceEitherWayAndALineAtALevel`, `EveryNodeIsCaughtOnABrokenCopy` and the three marble `TheCommittedPngEqualsTheGraph` rows. The greyscale table's `difference` row (a = 0.8, b = 0.3) equals `subtract` on its own and would not catch this, but the hand-worked fact and the broken-copy table do. Tiling and size-invariance rows were added as the Scope asks.
- Sample: `marble.texgraph` uses `difference` twice. It is in `LibraryStage` part 1, and its PNGs are held by `PatternSamplesTests`' rule tests (base-colour range, unit normals, no flat output, no metallic, no seam) and the new thin-veins test. The graph's levels and roughness read as the As-built notes say (veins rougher, sunk in the normal).
