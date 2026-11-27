---
description: >-
  A Patina node that layers a detail normal map over a base one by reoriented normal mapping, so a baked bevel normal and a noise bump live in one normal texture
---
# T-224 — normal_blend: combine two tangent-space normal maps in a Patina graph

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** [T-240](T-240-image-read-a-png-into-a-patina-graph.md)

## Goal

One normal texture can carry both a mesh's rounded edges (from `bake`'s `normal` map) and a surface's bumps (from `height_to_normal`). Today a material gets one or the other.

## Scope

- A new Patina node `normal_blend` in `src/Patina.Graphs/Nodes/`, registered in `NodeRegistry` and listed in the server's node catalogue and the node-library README (T-180 keeps that README current).
  - Inputs `base` and `detail`, both colour tangent-space normals in -1..1, the form `height_to_normal` outputs. Either may be a PNG read through an input node, such as a `bake` normal map.
  - Parameter `strength` 0..1 (default 1) scales the detail's tilt (it lerps detail toward (0, 0, 1) and renormalises) before blending.
  - The method is reoriented normal mapping (Barré-Brisson and Hill): the detail is rotated into the base's frame. The output has unit length and passes the existing `NormalNotUnitRule`.
- Tests in `tests/Patina.Graphs.Tests`:
  - A flat base (0, 0, 1) returns the detail unchanged.
  - A flat detail returns the base.
  - Hand-worked texels for a tilted base with a tilted detail.
  - `strength` 0 returns the base.
  - The output is unit length everywhere.
  - A table of broken copies (inputs swapped, renormalisation skipped) is each caught, as T-135 and T-136 did for their nodes.
- A sample graph: a noise `height_to_normal` blended over a baked bevel normal PNG, and its swatch.

## Notes

- Raised on 2026-11-25 in a discussion about rough surfaces, alongside [T-223](T-223-displace-move-vertices-along-their-normals-by-a-graph-output-or-seeded-noise.md) and [T-225](T-225-bake-a-detailed-mesh-s-normals-onto-a-simpler-one.md). The person agreed to reserve it.
- Reoriented normal mapping was chosen over "UDN" (adding the xy components), which flattens strong detail on a tilted base. The bevel normal is exactly the tilted-base case.
- A height map as its own material slot (parallax or displacement) was set aside: glTF has no standard slot for one, so height stays an in-between step.
- 2026-11-27, at start: no graph could read a PNG (an `input` passes only what an outer `graph` node wires to it, or a flat default). The person chose a task of its own, [T-240](T-240-image-read-a-png-into-a-patina-graph.md) (the `image` node), landed first (v0.20.1); this task then took it as a dependency.

### As built

- **`normal_blend`** (`src/Patina.Graphs/Nodes/NormalBlendNode.cs`, registered in `NodeRegistry` after `height_to_normal`, format version 1): inputs `base` and `detail`, colour normals in -1..1; parameter `strength` 0..1, default 1. `NormalBlendNode.Blend(base, detail, strength)` is the per-texel call: the detail lerped toward (0, 0, 1) by `strength`, then RNM (t = base + (0, 0, 1), u = detail with x and y negated, r = t dot(t, u) / t.z - u), renormalised. The base is renormalised first (RV-97: a base short of unit length, from a `blend` of normals, a `blur` or a fractional shift, turned the detail too far: 80.1 degrees for 73.7 at length 0.5); the result once at the end, which, r being linear in u, also renormalises the lerp. A base facing into the surface (z = -1, no frame) gives itself; a zero-length base counts as flat, so it gives the detail; a zero-length result gives (0, 0, 1).
- **For later tasks**: read a baked normal with `{"type":"image","params":{"kind":"normal","path":"..."}}` (T-240), lay detail over it with `normal_blend` (`base` the bake), send the result to an `output` of role `normal`. At the graph's size the image is read texel for texel; T-240's bilinear read below half size can drop a 1-texel bevel line, so bake at the graph's size or larger by at most 2x.
- **Catalogue and README**: `nodes` gives its line, gist and worked example (`WorkedNormalBlend`: an fbm bump over a tile layout's bevels, 6 nodes), run as written by `NodeToolTests`. Its gist pushed define_graph's description, which lists every gist, to 3,697 with T-240's `image`; three gists were shortened (`normal_blend` "bump on a normal", `gradient_map` "colour a grey by stops", `graph` ".texgraph as a node") and every description is under 3,686 again. README: `normal_blend` among the filters, and `bevelled-slabs` among the samples with how its bake was made.
- **Sample `samples/graphs/bevelled-slabs.texgraph`** (version 2, for `image`): `bakes/slabs.normal.png`, carve's `bake` `normal` at 256, padding 8, `bevel.width` 0.02, of `bakes/slabs.carve` (a 4 x 4 grid of 0.23 m slabs, 0.04 m high, only their tops UV-mapped, each to one quarter-by-quarter cell, so the bake covers the whole square and tiles like a floor). Read as `image` kind `normal`, moved an eighth of the image by `transform` (a whole 32 texels, exact) so the wrap falls mid-slab, and an fbm bump (8 x 8 cells, `height_to_normal` strength 0.08) laid over it at strength 0.8; a grey stone base colour and roughness. Its PNGs come from `tools/Patina.SampleGraphs`; `swatch.png` is `library-bevelled-slabs-sheet.png` from a scenario-patina-library-2 run, which now opens, checks, exports and judges it (part 2, 85 s of its 300 s on the run that made it).
- **Decisions the Scope left open**: the sample bakes slabs rather than the wrench, because a wrench atlas leaves black (invalid) normals outside its islands and does not tile. Two 2 x 2 tries failed the seam tests honestly (one line of bevel per axis on the wrap is in the top 1% of steps; the older mean-step seam test cannot tell a slab edge on the wrap from a seam), hence the 4 x 4 grid and the eighth's offset. `strength` scales by lerping toward flat (as the Scope says), not by scaling x and y.
- **Tests**: `NormalBlendTests` (flat base gives the detail and flat detail the base, through a graph; strength 0 gives the base; hand-worked RNM texels for a tilted base with a tilted detail, the doubled tilt, strength 0.5, and a short base under a flat and under a tilted detail; unit length and `NormalNotUnitRule` clean at three strengths; tile sizes 16 and 32 equal to one tile; broken copies: inputs swapped, UDN in place of RNM, detail's xy not negated, strength ignored, not renormalised, base not renormalised first, each caught). `PatternSamplesTests` holds the sample's PNGs equal to the graph, its base colour range, unit normals, no flat output, no seam, and `TheSlabsKeepTheBevelAndTheBump` (99.7% of the bake's strip texels keep its tilt's sign in x and in y, 98% required; 99% of the flat middle carries the bump, 60% required). `CheckSamplesTests` checks it clean. `BevelNormalTests.The_bevelled_slabs_bake_is_what_bake_makes_of_its_model` (core-unit, whose inputs now include `samples/graphs/bakes/*`) holds the committed bake byte for byte equal to `Bake.BevelNormal` of `slabs.carve`, so a change to the bevel bake is told to remake it. `GraphFaultTests`' list of types names `normal_blend`.
- **Not verified**: the sample was not put on a carve mesh and rendered (it is a patina sample; the bake's own render path is T-125's). The `swatch.png` frames were judged by a reviewer agent from the scenario run, not by the person.
