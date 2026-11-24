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

### Hand-off

Model: Sonnet 5.5 (`claude-sonnet-5-5`). Stopped at the checkpoint on a stop-and-ask condition: the byte-identity check fails.

- Done and committed: `GraphBakes.Image` (src/Carve.Server/GraphBakes.cs; bakes the output as a base_colour slot, own cache key
  `+":image"`), render's `image` as `JsonElement` taking a path or `{graph, output, size}` (`RenderTool.cs`: `LoadGraphImage`,
  `RunUvs`, caption `image <file> <output> <size> px`, `baked` line), description text, `GraphImageToolTests` (unit refusals,
  first/second bake, regression through the process; all pass), `GraphImageStage.cs` + one line in `BakesStages.cs`, `insideOnly`
  on `CheckUvImage`, the three breaks in `BakesChecks.Break`'s doc, CLAUDE.md's Bakes row. `PartToolsListTests` still passes
  unchanged (the image sentences are outside the digest). `MaterialTools.cs` untouched (T-166's); the graph argument parser is a copy in `RenderTool.cs`.
- verify: server-unit PASS, scenario-materials PASS, scenario-bakes FAIL: the curvature, occlusion and steel-mask frames each
  differ from the bake-PNG frame in 303229 pixels (x 64..959, y 64..959, the whole unit-square window). Identical count for three different maps.
  Frames of that run are gone with the runner; not diagnosed further. Not turned into a tolerance.
- **Diagnosed** (second agent, Opus 5.5 `claude-opus-5-5`, from that run's artifacts via `cairn test artifacts scenario-bakes`; scripts measured, no frame opened):
  the three pairs differ in the same 303229 pixels, all of them where the look samples a texel **beyond the 8-texel padding**
  (outside `wrench.mask.all.png`'s cover; 586 of the 592 that a pixel-to-texel mapping puts inside the cover sit on its edge,
  the rest are overlay pixels there). Inside islands and padding: no pixel differs. There the bake PNG holds 0 (frame 0) and the
  graph output holds the node's neutral, as T-163's `MeshBakeSource` documents ("a texel no island or padding covers holds the
  node's neutral value"): curvature 0.5 (frame 64), occlusion 1 and mask 1 (frame 128), each half brightness as the look draws
  outside islands. 299647 pixels differ by 64/128/128; 3582 more are overlay pixels blended over that ground (deltas 1 to 36).
  The look's packing, sampling and padding are right; the two images differ by their own fill rules. Stopped as a spec question:
  the scope's byte identity does not hold against T-163's landed rule. Not changed: no tolerance, no narrowed comparison.
  The position frame ran in that run and raised no note; the breaks have not run.
- Left: the person's answer (compare islands and padding only, or change one fill rule, or else), then (old suspects: the graph's baked PNG vs `bake`'s PNG differ uniformly, e.g. channel packing or an alpha/sRGB
  difference, or the uvs path draws a graph-baked MaterialTexture differently from a loaded one), then position frame, breaks,
  the frame review, `### As built`, lint, finish. The position check and breaks have not run (the stage fails first at the identity checks only; position did not report).
