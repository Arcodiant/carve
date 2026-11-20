---
description: >-
  render mode uvs takes image as {graph, output, size} and draws that output, evaluated on the mesh, under its layout, so mesh masks can be seen
---
# T-165 — A graph output under the UV layout look

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-163

**Verify:** server-unit, scenario-bakes, scenario-materials

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
