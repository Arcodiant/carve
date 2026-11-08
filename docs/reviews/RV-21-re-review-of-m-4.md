---
description: >-
  Re-review of M-4
---
# RV-21 — Re-review of M-4

**Status:** Open

**Reviews:** M-4

**Round:** 2

**Previous:** RV-20

**Diff:** —

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

### M-4's Retrospective and T-25's As built still list as unverified what the fixes now cover (Minor)

The fixes changed what is tested but not the lists that say what is not. `docs/milestones/M-4-ask-the-mesh.md` is not in `git diff 52dbb20..HEAD` at all, so its *Never verified* list now says five false things. Line 208: "T-27's `move` error check looks only for the digits 3 and 9" (it now asserts `[3, 9]` and `is not an edge`, ShedScenario.cs:98). Line 208 also files the stray vertex under "seen to fail only behind another", though RV-20 showed it cannot fail and T-27's As built now says "not covered". Line 211: "No look was rendered at 1024" (`RenderTests.The_looks_are_drawn_at_every_size` renders one at 256, 512 and 1024). Line 212: "`highlight` ... on a sheet's pixels, is untested" (`What_applies_to_a_sheet_applies_to_every_tile` now has a `highlight` case). Line 214: "the reverted `move` text matches round 2 as run" (the text in MoveTool.cs:16 is now the facts-only sentence of the ruling, and no session has run on it either). In the same way, `docs/tasks/T-25-wireframe-normals-and-highlight-looks.md:165` still says `A_look_follows_the_mesh_through_an_edit` was "not seen to fail", while RV-20's answer says the rewritten test fails under the `Look.Shaded, Highlight.None` break. That leaves the record of what M-4 left unproven out of date.

**Outcome:** —

### `render`'s description does not say that a sheet the mesh keeps changing under ends in a tool error (Nit)

`RenderTool.cs:127` says only "if an edit lands while the tiles are drawn, the sheet is drawn again". After three tries `SheetVersions.cs:23` returns the tool error "The mesh changed while the sheet was drawn, on each of 3 tries ...", and T-26's As built ("up to three tries, then a tool error") says so too. Neither the sentence nor the description's list of tool errors (`RenderTool.cs:136-141`) mentions it. An agent whose sheet races a second client's edits gets an error the interface text does not say can happen. A clause like "up to three times, then a tool error" would fix it.

**Outcome:** —

### Two places still say a negative volume means inside-out, against the threshold ruling (Nit)

The ruling made `inside-out` apply only below -1e-6 x (bounds diagonal)^3. T-23's Scope and Notes, `check`'s description and `MeshHealth.IsInsideOut` all carry it. But the `MeshShell` doc comment at `src/Carve.Core/MeshHealth.cs:11` still says "<paramref name="SignedVolume"/> ... negative means inside-out", and the M-4 plan's *Health check* bullet (`docs/milestones/M-4-ask-the-mesh.md:44`) still defines `inside-out` as "a closed, consistently wound shell with negative signed volume", with no amendment marker like the one T-23's Scope has. RV-20's own flat sheet is a counter-example: `SignedVolume` -2.27e-7, and it is not inside-out.

**Outcome:** —

## Summary

Round 2, on `git diff 52dbb20..HEAD` (a2a1240, e335a94, 41b21fa and the tracker commits). I checked each of RV-20's 23 answers against the code. I read the new code in full: `LookHolder`, `SheetVersions`, the `ServedVersion` stamp from `RenderPump` through `RenderSystem.Render` into `RenderedFrame.Version`, `views` with nulls and `zoom: 1`, `IsInsideOut`, the `-0` key, the parser's same-position rule and the `)` case, `Shape.Parse`/`SameAsFile`, `NotLine`/`CheckNotLineAlong` and `BehindSlope`. Then I grepped every changed wording across descriptions, README, D-1, task Scopes and As builts, the milestone and pinned tests. The Core and Server unit suites pass (376 and 219, `Tier!=regression`), and no GPU suite was run.

A throwaway harness in %TEMP% on the real `Carve.Core` gave these results. (1) Every parser case I tried lists both errors at one character, and `)`, `x > )` and `x > 1 )` each still give one error. No new duplicate appeared. (2) For RV-20's flat sheet over 4,000 random offsets up to 1e5 m, `inside-out` was reported 57 times. Every one of those also carried a non-planar warning, so the quad was no longer flat once its corners were rounded to float, and `check`'s "a flat two-sided sheet ... is not" holds for sheets that really are flat.

Under each named break, by reading: the rewritten `A_look_follows_the_mesh_through_an_edit` fails, because `after` would be shaded and not equal to the fresh wireframe. `The_looks_are_drawn_at_every_size` fails if no lines reach a camera, because the wireframe fill and the background are both dark. The `LookHolder` unit tests pin the camera-only, destroy-once and failed-make sequences. `SheetVersions` returns at once for a single frame, and the store's version only rises (undo included), so equal stamps mean one mesh. The three rulings are carried out as ruled. `move`'s text is word for word the facts-only sentence in MoveTool.cs, T-28 and D-1. The threshold reads the same in code, T-23 and `check`, apart from the two places in the third finding. Floats compared with -0 equal to 0 read the same in code, T-23 and `check`, and there is a test.

The skipped stray vertex: its reason holds. The only labelled frames are `front` (512 and 1024), and the axes sheet has `labels: none`. Render frames the face bounds 0 to 4 and a target must lie inside them, so (6, 0, 0) is only in frame on a view along x. There it falls on the labelled corner vertex (4, 0, 0) or (0, 0, 0). No existing frame could test it, and a new one would need a direction just off the x axis.

So 23 of RV-20's 23 answers hold, the skip included, and no new defect was found in the new code. 3 findings remain, all about text that was not brought up to date: 1 Minor, 2 Nit.
