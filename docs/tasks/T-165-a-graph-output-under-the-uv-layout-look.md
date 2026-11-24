---
description: >-
  render mode uvs takes image as {graph, output, size} and draws that output, evaluated on the mesh, under its layout, so mesh masks can be seen
---
# T-165 — A graph output under the UV layout look

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-163

**Verify:** core-unit, graphs-unit, server-unit, scenario-bakes, scenario-materials

## Goal

An agent sees a graph output where it lands on a mesh, a named mask included: `render mode=uvs` takes `image` as a
graph reference and draws that output, evaluated on the mesh with its real curvature, occlusion, masks and position,
under the layout.

## Scope

- **`render`'s `image`** (`RenderTool.cs`, `RunUvs`; `RenderQueue`, `RenderSystem`'s `_uvImage`) takes a PNG path, as
  now, or `{graph, output, size}` with T-160's rules for each field (`size` defaults to the graph's own, 64 to 4096).
  The output is baked for the mesh the look shows, through T-163's call and cache, and drawn by T-121's path
  unchanged: nearest sampling, each pixel a texel, the image as it is inside the islands and at half brightness
  outside, white edges and yellow seams. As agreed:

  ```
  render {mode:"uvs", mesh:"barrel",
          image:{graph:"graphs/weathered-iron.texgraph", output:"wear", size:512}}
  +------------------------------------------------+
  |................................................|  outside islands: the output
  |..+--------------------------------------+......|  at half brightness, so the
  |..|#### hoop  ===rim wear (bright)=== ###|......|  padding shows as a fringe
  |..|                                      |......|
  |..|   staves: wear 0 (black)             |......|  inside islands: the output
  |..|                                      |......|  as evaluated on THIS mesh
  |..|#### hoop  ===rim wear (bright)=== ###|......|  (curvature, occlusion, masks
  |..+--------------------------------------+......|   are real, not stand-ins)
  |..+-------+....+-------+........................|
  |..|  cap  |....|  cap  |........................|  white edges, yellow seams,
  |..+-------+....+-------+........................|  as today
  +------------------------------------------------+
   caption: weathered-iron.texgraph wear, 512 px, mesh barrel
  ```

  - Any output may be named: a role or a named output. A grey output is drawn grey; `roughness` is drawn as its own
    grey values, not the packed image; `normal` as its colours.
  - The caption names the graph file, the output and the size. The text result carries `baked`, as T-163's does.
  - Refusals, each naming `image`: with a mode other than `uvs` (the existing words); an object with an unknown or
    missing key; and whatever T-163's `GraphSlotProblem` reports.
  - The description's `image` sentences (`ImageLookText`, `ImageArgumentText`) gain the object form and a worked
    example run by its test.
- **A stage in scenario-bakes**, one file and one line in `BakesStages.cs`: `GraphImageStage.cs`, on the wrench.
  - A graph of one `mesh_curvature` node (width 0.003) into a named output, written by the stage into the run folder.
    Its `uvs` frame at 1024 is **byte-identical** to the curvature stage's frame of `wrench.curvature.png` baked at
    1024 with padding 8, but for the caption strip: the node and the bake are one computation.
  - The same for `mesh_occlusion` (32 rays, reach 0.035) against the occlusion stage's frame, and `mesh_mask`
    (`group: "steel"`) against the surface stage's steel mask frame.
  - `mesh_position` has no bake to equal: its frame is checked by T-121's block rule against texels the stage works
    from `get_mesh`'s positions and UVs, and the red channel must rise along the wrench's x.
  - Breaks on the scenario side, added to `BakesChecks.Break`'s table and shown failing: the width doubled in the
    graph, the mask of `rubber` in place of `steel`, position's x and z swapped in the prediction.
  - If a frame is not byte-identical, report the count and place of differing pixels and stop: do not turn the check
    into a tolerance.
- **Tests** (`server-unit`): every refusal, the worked example, `baked` on first use and none on the second.
- The person approves the new frames (a review agent opens them first, the crops scaled up, and says what it sees).
- CLAUDE.md's Bakes scenario row.

## Notes

- **Decided at planning (2026-11-20, by the person):** the `uvs` look draws a graph output evaluated on the mesh, as
  sketched above.
- **The orchestrator's calls, each a stop if it proves wrong:** byte identity with M-24's bake frames as the stage's
  check; `roughness` drawn unpacked in this look.
- Sonnet, with a separate agent for the frame review. Runs beside T-164 and T-166: it owns `RenderTool.cs`'s `uvs`
  path, `RenderQueue.cs`, the `uvs` image in `RenderSystem.cs` and the new stage, and adds one line to
  `BakesStages.cs`.
- A scenario references no product library, and breaks live in the scenario, never in the product.
- The wrench has no concave edge and no jaw opening (M-24's retrospective); write no check that needs one.
- **Write down for T-167** (As built): the argument's shape, the caption, and how its frames were checked.
- **Stop and ask:** an existing `uvs` frame that changes; a byte-identity check that fails.

### Hand-off

Model: Opus 5.5 (`claude-opus-5-5`), third agent, after Sonnet 5.5 built it and Opus 5.5 diagnosed it. Stopped at the
checkpoint: code committed and `verify` passing, the frame review left.

- **The person's ruling (2026-11-24):** `bake` writes each map's neutral value in the texels no island or padding covers,
  so the node and the bake are one computation over the whole image. Declined: comparing islands and padding only, and
  making the mesh nodes fill 0. Done: `Bake.FillUncovered` (`src/Carve.Core/Baking/BakePadding.cs`), used by
  `MeshBakeSource.Finish` and by `BakeTool` for masks, `curvature` and `occlusion`, each with its node's `Neutral` constant
  (`MeshMaskNode`, `MeshCurvatureNode`, `MeshOcclusionNode`; not written twice); the fill comes before `Pad`, so padding
  is unchanged. One sentence in `bake`'s description and in README's Baked maps. A core-unit test of the fill.
- **Not asked, left as they are:** `base_colour`, `orm` and `normal` hold black (0, 0, 0) beyond the padding, as before.
  The ORM's R (occlusion) is now 0 there while `occlusion.png` is 255: they disagree beyond the padding (inside islands
  and padding they agree). Not decided.
- **Numbers written down that moved, only because of those texels:** `BakeToolTests` (the 64 x 64 box's left and right
  masks): a texel beyond both islands' padding was predicted 0 in both masks, now 255 in both.
- **Measured against a run of `origin/milestone/M-29`** (jobs J-20261124-085554-4d6a68 and -085556-0bb89a, frames
  compared by hash, none opened): changed, all `uvs` frames: `wrench-uvs-curvature`, `wrench-uvs-steel`,
  `wrench-orm-uvs-occlusion` (303229 pixels each), `lblock-uvs-curvature` (500186), and the crops cut from them,
  `wrench-uvs-curvature-jaw-4x` and `wrench-uvs-steel-padding-4x` (`lblock-uvs-curvature-inner-4x` did not move). No
  `render`, `render_glb` or export frame moved. In the PNGs, each wrench map changed in 396290 texels, exactly the texels
  `wrench.mask.all.png` holds 0 in on the base run (beyond the padding), 0 to 128 for curvature, 0 to 255 for masks and
  occlusion; `lblock.curvature.png` in 653450 texels, all 0 to 128. Every changed pixel samples a changed texel by the
  block rule's pixel-to-texel mapping, but for 592 (wrench) and 420 (L-block) whose centres lie within 0.01 texel of a
  texel boundary next to a changed texel (one at 0.14, an edge-overlay pixel). New frames: `wrench-uvs-graph-curvature`,
  `-graph-occlusion`, `-graph-steel`, `-graph-position` and the bake-PNG frames they equal, `wrench-uvs-curvature-png`,
  `-occlusion-png`, `-steel-png`, all in `frames/` of a `scenario-bakes` run (`cairn test artifacts scenario-bakes`).
- **Checked:** `verify` passes (below); the three identity checks and the position frame pass; each break fails as it
  should, on its own check only (ad-hoc runs with `CARVE_BAKES_BREAK`): `graph-width` "graph image curvature: 169958
  pixels differ", `graph-mask` "graph image steel: 362288 pixels differ", `graph-pos` 22256 pixels breaking the block
  rule on `wrench-uvs-graph-position`. The position frame is held to the block rule inside islands only (`insideOnly`),
  as the first agent wrote it: the padding's texels are not predicted from `get_mesh`.
- **Run time:** `scenario-bakes` 50.7 s on `origin/milestone/M-29`, 68.4 s with this member, one run each on one runner:
  the new stage adds about 18 s.
- **Descriptions** (characters, `origin/milestone/M-29` then this member): `render` 19122 to 19954, `bake` 8410 to
  8684; both were over 4,096 before. `PartToolsListTests`' digest did not move.
- **Left, before the frame review (from the coordinator, after this `verify`):** T-166 has landed on
  `origin/milestone/M-29`. Rebase (expect `PartToolsListTests.Before` to conflict: keep both sides' descriptions,
  rebuild, retake the digest). T-166 words a graph problem `materials['<name>'].<slot>: <cause>`, so the look's
  `GraphSlotProblem.Of(<graph file>, "image", e)` in `RenderTool.cs` would read `materials['<file>'].image: ...`, wrong for
  an image that belongs to no material: add an `Of` overload with no material, naming the `image` argument and the cause
  (T-166's `### As built` has the table of causes), with a `server-unit` test holding the whole message; then `verify`.
- **Left:** the frame review (open the new graph frames and the moved bake frames, crops scaled up; the person approves
  the set through `mcp__waystone__review`, files to `G:\My Drive\artifacts\carve\M-29\T-165\round-N`), then
  `### As built` (fold this section in; T-167's notes: the argument's shape, the caption, how the frames were checked),
  `cairn lint`, `finish`.
