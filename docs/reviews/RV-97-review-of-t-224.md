---
description: >-
  Review of T-224
---
# RV-97 — Review of T-224

**Status:** Answered

**Reviews:** T-224

**Round:** 1

**Previous:** —

**Diff:** 5ef5090393e8645201d2829e984986fdd8c2d16f against origin/main

**Reviewer:** claude-opus-5-5 (two fresh agents, and a third on the frames)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: normal_blend (node, unit tests, catalogue)

### define_graph's tool description is now over the 3,686-character limit

`src/Patina.Server/NodeCatalogue.cs:54` adds the gist "a bump over a normal", and `NodeCatalogue.cs:154` joins every gist into define_graph's description, which gains about 37 characters. On this branch (rebased onto T-240's `image`), `NodeToolTests.Every_description_is_under_the_limit` fails: "define_graph 3697". It breaks CLAUDE.md's description rule, fails the suite at `finish`, and makes As built's "every description stays under 3,686" untrue.

**Outcome:** fixed

Fix: Three gists shortened (`normal_blend` "bump on a normal", `gradient_map` "colour a grey by stops", `graph` ".texgraph as a node"); NodeToolTests passes; As built says so.

### A base that is not unit length is used as is, so the detail is turned by the wrong angle

`src/Patina.Graphs/Nodes/NormalBlendNode.cs:31-34`: RNM needs a unit base. t = base + (0, 0, 1) of a short base is not a scaled copy of the right t, and the final renormalisation fixes the length, not the direction. With detail (0.6, 0, 0.8), where the right tilt is 73.7 degrees, bases of length 0.9, 0.7 and 0.5 give 74.8, 77.3 and 80.1 degrees. Short bases come from a `blend` of normals, a `blur`, or a `transform` or `warp` at a fractional offset. CheckByHand tries a short base only under a flat detail, where the error does not show.

**Outcome:** fixed

Fix: The base is renormalised first; CheckByHand adds 0.5 (0.6, 0, 0.8) under (0.6, 0, 0.8), expecting (0.96, 0, 0.28), and the broken copies add "base not renormalised first".

## Findings: the bevelled-slabs sample and its docs

### TheSlabsKeepTheBevelAndTheBump holds only half the bevel: the y tilt is never checked

`tests/Patina.Graphs.Tests/PatternSamplesTests.cs`, `TheSlabsKeepTheBevelAndTheBump`: a strip texel is one where the bake's |bx| > 0.3, and only nx's sign is compared. A green flip on the way through (`image` decoding the DirectX convention, `normal_blend` negating the base's y) remade into the PNGs passes every test. A probe measured 6128/6144 y-strip texels keeping the bake's y sign, against 16/6144 with green flipped.

**Outcome:** fixed

Fix: The same loop counts the y strips (|by| > 0.3) and holds ny's sign at 98%, measured 6128/6144.

### Nothing ties bakes/slabs.normal.png to carve's bake of bakes/slabs.carve

README and As built present the PNG as carve's `bake` `normal` at 256, padding 8, `bevel.width` 0.02, of `slabs.carve`. It is exactly that today (a probe: 0 differing bytes), but no test checks it, and the sample-wide .carve tests read only `samples/*.carve`. A later change to `Bake.BevelNormal` leaves the sample stale with every test green.

**Outcome:** fixed

Fix: `BevelNormalTests.The_bevelled_slabs_bake_is_what_bake_makes_of_its_model` bakes `slabs.carve` as BakeTool does and compares the bytes; core-unit's inputs in tests.json now include `samples/graphs/bakes/*`.

## Findings: the frames (swatch sheet, spheres at 0 and 90)

No defects. The bevels are raised slab edges in the right glTF sense (no inversion or mirroring), the eighth's offset is right (correlation 0.66 with the shifted bake, 0.00 unshifted), and there is no seam in the tiled cell. Noted, and not defects: the base colour's large mottle (4 cells) repeats visibly at tiling 3, and the bump reads faintly on the lit sphere next to its strength in the normal map.

## Summary

Four findings, all fixed: one over-long description (from the rebase onto T-240), one real defect in the node (a short base), and two test gaps on the sample. The frames had none.
