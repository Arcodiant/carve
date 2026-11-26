---
description: >-
  A Carve material names a texture graph output, graphs read the mesh's baked maps, and Carve re-bakes them when the mesh or UVs change
---
# M-29 — Graphs on a mesh

**Status:** Done

**Depends on:** M-27, M-24, M-28

**One-liner:** *A Carve material names a texture graph output, graphs read the mesh's baked maps, and Carve re-bakes them when the mesh or UVs change*

## What this milestone delivers

Carve uses the shared texture-graph package from [M-27](M-27-a-first-texture-graph.md) directly. A material can name a graph file and an output in place of a PNG, and the graph can read the mesh.

- `define_material` takes a graph path and output for any texture slot. The Carve file keeps it, relative to the file as texture paths are.
- Mesh-input nodes, as a Carve extension to the package: curvature, occlusion, an id mask per group or material, and position, from [M-24](M-24-baked-maps.md)'s bakes.
- `render` and `export` evaluate the graph on the mesh's UVs. An edit to the mesh or its UVs makes the next render or export bake again, never a stale texture.
- Errors: a missing graph file lists the graph files in its folder, as a missing texture does (T-81), and a graph error names the node.

Content: the barrel with edge wear on its iron hoops from curvature, and grime in its stave gaps from occlusion.

## Estimated size

About **6 tasks**:

1. A material slot names a graph output, with a new Carve file version.
2. Mesh-input nodes from M-24's bakes.
3. Evaluation in `render` and `export`, with caching and re-baking after edits.
4. Errors and `check` for graph materials.
5. The weathered barrel and its scenario.
6. Acceptance: fresh agents weather a model and recover from a planted wrong graph path.

## Why

[D-7](../design/D-7-texture-graphs-and-layered-surfaces.md): mesh-driven masks are where a layered material gets its value. M-24's bakes are those masks. A graph that reads them belongs in Carve, so it is re-baked when the mesh changes.

## Open at planning

- ~~Whether M-24 already writes its bakes in a form the nodes can read, or this milestone wraps its PNGs.~~ Answered
  at M-24's planning and held here: the nodes call `Carve.Core.Baking`'s API (T-120), never the PNGs (T-161).
- ~~Evaluation resolution per material, and how long a bake may take before `render` should warn.~~ Answered below: a
  size per slot, and a warning past 5 s.
- ~~Does M-28 need to land first, or are M-27's nodes enough for the content?~~ Answered below: M-28 lands first.

**Decided at planning (2026-11-20).**

By the person, each put with a recommendation:

- **M-28 lands first.** M-28 replaced the node API the mesh nodes are written against (tiles for bands, T-133) and
  added named outputs, `input` and `graph` nodes and exposed parameters; nine of its eleven tasks are done on its
  branch. This plan is written against that API. M-29 is started only once M-28 is on `main`.
- **Mesh inputs are node types, neutral in patina.** `mesh_curvature`, `mesh_occlusion`, `mesh_mask` and
  `mesh_position` live in a new engine-free project that both servers reference. carve feeds them real bakes; patina
  registers them with neutral stand-ins and says so on the swatch, so a mesh-reading graph can be authored, saved and
  swatched there. (patina refuses to save a graph with an unknown node type, and carve has no graph tools.) Declined:
  binding M-28's `input` nodes from the material, and mesh nodes known to carve alone.
- **A bake per mesh.** A material belongs to the model, but a graph that reads the mesh bakes into one mesh's UV
  layout, so each mesh that uses such a material gets its own bake. Declined: refusing the material on a second mesh.
- **Export names the copies `<material>@<mesh>`,** only when more than one mesh uses a mesh-reading material; one mesh
  keeps the plain name. `define_material` refuses `@` in a new name; a file that has one still opens.
- **A slot is a PNG path or `{graph, output, size}`,** per slot. `size` is a power of two from 64 to 4096 (what bakes
  and material textures both allow), default the graph's own. Declined: one graph and size per material.
- **Carve file version 10** is version 9 plus graph slots, written only for a model that has one (whatever else it
  has), the path relative to the file. Every other model is written byte for byte as before.
- **Graph files are read at each render and export,** nested files too, and a change in their content bakes again, so
  a `save` in patina shows on the next carve render with no other call. This differs from a PNG, which is read once,
  and the descriptions say so. A render can therefore fail later because a file was broken or removed, and `undo` does
  not bring back an older graph. Declined: holding the graph as PNG bytes are held, with or without a warning.
- **Bake time:** `render` and `export` always answer with `baked: [{material, mesh, slot, size, ms}]`, and warn when one
  material took over 5 s, naming the slot and node that cost most. Nothing is refused. The bake is kept in memory and
  never in the Carve file.
- **The `uvs` look draws a graph output** evaluated on the mesh, as sketched below, so a mask can be seen.
- **The content is the barrel with raised hoops,** built through the tools from `samples/barrel.carve`: wear on the
  hoop rims from curvature and grime on the staves beside the hoops from occlusion. The stave gaps stay the planks
  graph's own. Declined: cutting stave grooves, and the wrench as the content.

By the orchestrator, recorded in the tasks' Notes, each a stop if it proves wrong:

- the project is `src/Carve.Graphs`; the four nodes' parameters, defaults and neutral values; position normalised per
  axis over the mesh's bounding box; mesh inputs and mesh-reading outputs padded 8 texels (T-161, T-163);
- a missing or broken graph does not refuse `open`: it warns, `check` lists it, and `render` and `export` fail on that
  material until it is fixed (T-160, T-166). Without this the acceptance agents, who have no file tools, could not
  recover;
- which outputs each slot takes, with `metallic_roughness_texture` taking the packed `roughness` output as patina's
  `roughness.png` is; the cache's key and its 512 MB cap (T-163);
- a warning, not a refusal, when export writes per-mesh copies (T-164);
- the two sample graphs, `weathered-iron` and `weathered-staves`, whose `wear` and `grime` are 0 on neutral inputs
  (T-162);
- the acceptance model is the wrench, and its plant is `graphs/steel-worn.texgraph` named where
  `graphs/worn-steel.texgraph` exists (T-168);
- nine tasks where the estimate was six: the per-mesh bake and the `uvs` look each added one, and patina's side one.

Checked at planning (2026-11-20):

- **M-28's branch** (`milestone/M-28` at `afd9ae1`): T-133 to T-141 are Done there; the node API, `InputNode`,
  `GraphValidator.Uses` and T-133's and T-140's As built were read. `GraphValidator` reads nested files from disk
  itself (`File.ReadAllText`), which is why holding a graph would have needed a new seam.
- **The barrel cannot show mesh-made wear or grime as it is** (a throwaway console in the temp folder, Debug, on
  `Carve.Core.Baking`): `samples/barrel.carve` at 512 and 1024 has occlusion exactly 1.000 on every texel (32 rays,
  reach 0.15) and curvature on every stave edge alike. With faces 0 to 7 and 24 to 31 extruded 0.015 and unwrapped
  (98 faces, 3 islands, fill 0.50, no overlap): hoop tops' curvature (width 0.02) has mean 0.628 and 95th percentile
  0.996; the staves' occlusion has minimum 0.500, 5th percentile 0.911 and mean 0.986; the hoop walls' 5th percentile
  is 0.283.
- **Times** (the same probe): at 1024, curvature 0.6 s, occlusion 3.5 s plain and 5.6 s with hoops; at 512, occlusion
  1.3 to 1.4 s. M-24 measured 7.4 to 8.8 s on the wrench at 1024. Tests and worked examples therefore use 256 or 512.
- **Under a plain `unwrap` the staves do not sit on the planks' grid:** a stave face is 0.115 to 0.130 wide in u with
  wavy borders, where the planks' staves are 1/8. T-167 settles the layout before any frame.

Not checked: how the weathered barrel looks (no graph was evaluated on it); a bake at 4096; any time in a Release
build; whether the preferred UV layout (staves projected as M-7 does, the rest unwrapped) is free of overlaps;
occlusion between parts a part tree moves (M-24 left it); what patina's publish folder gains from referencing
`Carve.Core` through `Carve.Graphs`.

**The slot, as agreed:**

```
define_material {
  name: "weathered-iron",
  base_colour_texture:        {graph: "graphs/iron.texgraph", output: "base_colour", size: 1024},
  metallic_roughness_texture: {graph: "graphs/iron.texgraph", output: "roughness"},
  normal_texture:             "textures/iron-normal.png"      // a PNG, as today
}
-> {replaced, material: {...,
      base_colour_texture: {graph: "<abs>/graphs/iron.texgraph", output: "base_colour",
                            size: 1024, reads_mesh: true},
      metallic_roughness_texture: {graph: ..., output: "roughness", size: 512, reads_mesh: true},
      normal_texture: {path: "<abs>/textures/iron-normal.png", width: 256, height: 256}},
    warnings: []}

in the .carve file (version 10), relative to the file:
  "base_colour_texture": {"graph": "graphs/iron.texgraph", "output": "base_colour", "size": 1024}
```

**The `uvs` look with a graph output, as agreed:**

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

**Order:**

| Wave | Task | Model | Depends on | Owns (no sibling in its wave touches) |
|---|---|---|---|---|
| 1 | T-160 graph slots, file version 10 | Opus | — | `Material.cs`, `CarveFile.cs`, `MaterialTools.cs`, `GetMeshTool.cs`, save and open |
| 1 | T-161 mesh-input nodes | Opus | — | the new `src/Carve.Graphs` and `tests/Carve.Graphs.Tests`, suite `graphs-unit` |
| 2 | T-162 patina and the two sample graphs | Sonnet (+ frame review) | T-161 | `src/Patina.Server`, `tools/Patina.SampleGraphs`, `samples/graphs/`, the Patina scenario |
| 2 | T-163 the bake, the cache and render | Opus (two agents) | T-160, T-161 | new files in `src/Carve.Graphs`, `PbrMaterials.cs`, `RenderSystem.cs`'s materials, `GraphSlotProblem` |
| 3 | T-164 export and per-mesh copies | Sonnet | T-163 | `GlbExport.cs`, `SkinnedExport.cs`, `GlbParts.cs`, `ExportTool.cs` |
| 3 | T-165 a graph output in the `uvs` look | Sonnet (+ frame review) | T-163 | `RenderTool.cs`'s `uvs` path, `RenderQueue.cs`, `GraphImageStage.cs` in the Bakes scenario |
| 3 | T-166 errors and `check` | Sonnet | T-163 | `GraphSlotProblem`, `MaterialTools.cs`, `CheckTool.cs`, `MeshHealth.cs`, `OpenTool.cs` |
| 4 | T-167 the weathered barrel's scenario | Opus (three agents) | T-162, T-164, T-165, T-166 | `scenarios/Carve.Scenario.Weathered`, its load tests, script and sample |
| 4 | T-207 carve's descriptions under the cut (added 2026-11-24) | Opus to plan, then by its scope | T-165, T-166 | the description strings of carve's tools, any new tool, the length test |
| 5 | T-168 acceptance | Sonnet (inner sessions on the default, Opus) | T-167, T-207 | its round folders, prompt and checks |

Content comes with its mechanic: T-161's tests bake the wrench and the raised-hoop barrel; the two weathered graphs
land with patina's side in wave 2, beside the bake that draws them; T-163's and T-164's regression classes draw and
export the raised-hoop barrel with them; T-165 shows the wrench's maps through graphs. T-167 is where the person sees
it whole.

Shared lines that only add (`Carve.slnx`, `tests.json`, CLAUDE.md's Layout, `BakesStages.cs`, `PatinaStages.cs`) are
settled by keeping both sides. T-162 can start as soon as T-161 lands and T-163 need not wait for it; T-163 switches
its regression class to T-162's graphs once they land, and should expect a hand-off there.

Frames: T-162, T-165 and T-167 draw new frames. Each plans its frame review as its own agent, and the person approves
each set before it lands. Nothing here adds sound or motion, so there is no listening or real-time pass. The milestone
is reached from a plain launch of `carve` and `patina`, through their MCP tools. No engine need was found.

## Required tasks

- [ ] [T-160](../tasks/T-160-graph-slots-in-materials-and-the-carve-file.md) — define_material takes {graph, output, size} for any texture slot in place of a PNG path, kept in the Carve file as version 10 relative to the file, and read back by get_mesh, save and open
- [ ] [T-161](../tasks/T-161-mesh-input-graph-nodes.md) — Carve.Graphs, a new engine-free project: mesh_curvature, mesh_occlusion, mesh_mask and mesh_position node types fed from Carve.Core.Baking on a mesh's UV layout, neutral where there is no mesh
- [ ] [T-162](../tasks/T-162-patina-knows-the-mesh-nodes-and-the-weathered-sample-graphs.md) — patina defines, saves and swatches graphs with mesh-input nodes on neutral stand-ins and says so, and samples/graphs gains weathered-iron and weathered-staves with their PNGs
- [ ] [T-163](../tasks/T-163-graph-materials-evaluated-and-cached-for-render.md) — render draws a material whose slots name graph outputs, evaluated on each mesh's UVs and bakes, re-read and re-baked when the graph files, the mesh or its UVs change, and reports what it baked and how long it took
- [ ] [T-164](../tasks/T-164-graph-materials-in-export.md) — export embeds each graph slot's evaluated PNG in the .glb, with one <material>@<mesh> copy per mesh when a mesh-reading material is used by several, and render_glb agrees with render
- [ ] [T-165](../tasks/T-165-a-graph-output-under-the-uv-layout-look.md) — render mode uvs takes image as {graph, output, size} and draws that output, evaluated on the mesh, under its layout, so mesh masks can be seen
- [ ] [T-166](../tasks/T-166-graph-material-errors-and-check.md) — A missing graph file lists the .texgraph files in its folder, a graph fault names its node, an unknown output lists the outputs, and check warns of every graph material that cannot be baked
- [ ] [T-167](../tasks/T-167-the-weathered-barrel-and-its-scenario.md) — samples/barrel-weathered.carve: the barrel with raised hoops, edge wear on the hoop rims from curvature and grime beside the hoops from occlusion, in suite scenario-weathered with re-bakes after mesh, UV and graph edits
- [ ] [T-168](../tasks/T-168-graphs-on-a-mesh-acceptance-run.md) — Fresh agents weather the wrench with carve and patina from the tool descriptions alone, and recover from a planted wrong graph path
- [ ] [T-207](../tasks/T-207-carve-s-tool-descriptions-under-claude-code-s-cut.md) — Every carve tool description fits Claude Code's 4,096-character cut, held by a test, with the detail that was cut still reachable by an agent
- [ ] [B-10](../bugs/B-10-get-mesh-on-a-large-mesh-answers-more-than-an-agent-can-read.md) — get_mesh takes include and faces to answer part of a mesh, so a model like the wrench can be read by an agent whose client limits a tool result
- [ ] [B-11](../bugs/B-11-the-1024-bake-tests-time-out-under-load.md) — The server-regression tests that bake both weathered materials at 1024 through the real process wait longer than 30 s for that call, so they pass on a loaded machine
- [ ] [T-217](../tasks/T-217-rv-82-the-bake-s-key-validation-of-mesh-parameters-and-the-graphs-suites.md) — The bake cache never serves one slot's bytes to another, a mesh node's bad parameter is a fault when the graph is validated (in carve and in patina) and not a failure at the first bake, and the suites that hold `Carve.Graphs` are honest about their tier and their inputs.
- [ ] [T-218](../tasks/T-218-rv-82-the-server-s-render-export-and-error-findings.md) — `render` never returns a frame with empty graph slots, a per-mesh copy never replaces a material the model has, `export` can be cancelled, a bake that fails on the mesh says what to do about the mesh, and the piece-export fix has its test.
- [ ] [T-219](../tasks/T-219-rv-82-patina-s-findings.md) — patina tells an agent the truth about a mesh-reading graph: one that nests a mesh-reading graph carries the "mesh inputs neutral" note everywhere and is not called broken by `check`, an evaluation that fails says why, and the checks of the note and of the worked examples can fail.
- [ ] [T-220](../tasks/T-220-rv-82-the-descriptions-and-docs-findings.md) — What carve's descriptions, `help` entries, README and CLAUDE.md say is complete and true: `reads_mesh` is explained, `render` says how `image` takes a graph, no description denies a tool that exists, and the counts and tests around `help` are right.
- [ ] [T-221](../tasks/T-221-rv-82-the-acceptance-and-scenario-findings.md) — The acceptance crevice check measures the set the person ruled and the record says what it measures; the weathered scenario checks the cache hit it claims; the round record's figures are right.
- [ ] [T-226](../tasks/T-226-rv-83-the-render-pump-the-bake-key-and-export-s-copies.md) — No order of events draws a model with empty graph slots and calls it baked; a slot that refuses an output refuses it from the cache too; export never renames a material the user named; and the render retry is held by a test of `render` itself.
- [ ] [T-227](../tasks/T-227-rv-83-patina-s-check-text-and-the-title-break.md) — patina's `check` says only what is known about a flat output that reads a mesh node, and the scenario's break for the title note fails on two titles the product really drew.
- [ ] [T-228](../tasks/T-228-rv-83-the-crevice-test-s-suite-and-the-records.md) — The test that holds the acceptance crevice set runs in a suite, and the records and docs the review found wrong say what is true.

## Decided while it ran

By the person (2026-11-24), each put with a recommendation:

- **`bake` writes each map's neutral value outside the islands and their padding** (curvature 0.5, occlusion 1, a mask
  1), as the mesh nodes do, so that T-165's whole-frame check of a graph output against the matching bake holds as
  written. The bake held 0 there. This moves M-24's bake PNGs and `scenario-bakes`' frames in those texels. Declined:
  comparing islands and padding only (the orchestrator's recommendation), and making the mesh nodes fill 0. Not asked:
  what base colour, the normal and the ORM hold there.
- **carve's over-long tool descriptions are cut down inside M-29, before T-168,** as the new member T-207. Declined:
  filing it off-milestone, and leaving them.
- **carve gains a `help` tool** (T-207): `help {tool}` returns what was moved out of that tool's description, as
  patina's `nodes` does. Declined: another name, and cutting by shorter writing alone.
- **T-167's thresholds,** all five as proposed from the measured numbers (rim over hoop top by 8 luma, stave middle over
  stave beside a hoop by 7, `wear` by 0.2, `grime` by 0.15 with 0.02 elsewhere, 2 luma after `wear_amount` 0); then, in
  stage 3, D-6's rule on the stave and cap pixels with threshold (1) on the hoops after the unwrap, M-7's relations in
  place of `ColourRule` for the clean colours, and the stave close-up darker by 4 luma after the hoop is moved.
- **T-168's rounds run on `--model claude-opus-5-5`,** pinned, where the task said the default.
- **T-168's margins:** hard-edge wear by 30 luma and the drive crevice by 10; a refused tool call passes and is
  recorded; the grip is held to what its faces wear in the saved file.
- **`get_mesh` gains `include` and `faces`** (B-10, found by round 1), fixed in M-29 before round 2. Declined: a
  summary by default past a size, and filing it off-milestone.
- **T-168's crevice set** (2026-11-25): first the drive's texels within 0.004 m of the edges of its two ends, darker
  than open steel by 10 luma, after round 2 failed on the whole-drive set an agent had chosen (the distance was chosen
  with round 2's number in view, and the person was told so; declined: 0.012 m, and a margin of 20). Then, on the
  review's finding that the code counted a socket face and that 41% of the set lay on the drive's hidden ends, **the
  drive's four visible side faces only,** same distance and margin (T-221). Declined: the first set with the socket
  face removed.
- **The taught wear example breaks its mask with noise** (T-168, between the rounds), after the person's note that
  round 1's wear "reads like an edge highlight". Recipes and guidance to agents as such are T-208, off-milestone.
- **The 1024 bake tests wait 120 s for that call** (B-11), after the milestone's verify timed out on one. Declined:
  baking at 512 there, and re-running until it passes.

By the person, on the review's findings (2026-11-25 and 2026-11-26), each put with a recommendation:

- **A per-mesh copy whose name the model already has is refused** by `render` and `export`, naming both, and `check`
  warns (T-218). Declined: picking a free name.
- **Bake memory beyond the cache's cap is filed off-milestone** (T-222). Declined: fixing it in M-29.
- **patina's `check` keeps `one-value` for a flat output that reads a mesh node, with a true reason** (T-219), and,
  after round 2 found the first wording untrue, **says only what is known:** it may be flat only because its mesh
  inputs are neutral in patina, how to draw the real map in carve, and to widen its levels if it is flat there too
  (T-227). Declined: withholding `one-value`, and going back to the old text.
- **The empty-slots fault is fixed a third time and that fix re-reviewed** (T-226), after two rounds. Declined:
  filing it as a bug and shipping.
- **The Patina scenario's title break is made of two titles the product drew** (T-227), after two rounds. Declined:
  accepting the break as it was, and checking the answer's text in place of the frame.
- **A render that returns stale graph slots is filed as a bug** (B-12, off-milestone), found in round 3. Declined: a
  fourth fix to the render path inside M-29.
- **What the rebase's review found about `displace` is filed** (T-236, off-milestone). Declined: fixing its wording
  now, with another review round.

By the orchestrator:

- **Export's interim refusal of a graph material stayed until T-164,** T-163 narrowing its wording to export.
- **T-166 also makes the slow-bake warning name the slot and node that cost most:** T-163's named a node that cost
  0.0 s when a shared map had been charged to an earlier slot.

## Retrospective

**As planned.**

- **The slice runs end to end.** A material names `{graph, output, size}` in any texture slot and the Carve file keeps
  it (version 10); four mesh-input nodes read `Carve.Core.Baking`; `render` and `export` bake each slot on each mesh's
  UVs, cache it by content and bake again when the graph files, the mesh or its UVs change; the `uvs` look draws a
  graph output on the mesh; a missing graph lists the graphs in its folder and a fault names its node. The content is
  `samples/barrel-weathered.carve`, built through the tools.
- **The waves held:** T-160 and T-161, then T-162 and T-163, then T-164, T-165 and T-166 side by side, then T-167,
  then T-168. No two members collided on a file; the tools/list digest pin was the one shared line that needed care,
  and the brief's rule (keep both descriptions, retake it from the result) settled it each time.
- **The planning calls that held:** the project `src/Carve.Graphs`; the nodes' parameters, defaults and neutral values;
  8 texels of padding; `open` warning and not refusing on a missing graph; the slot table; the cache's 512 MB cap (its key
  did not hold: the review found it left out the slot); a warning for per-mesh copies; the wrench and its plant for the acceptance.
- **Planning's probe held:** the preferred UV layout for the barrel can be had through the tools (every stave face
  exactly 1/8 wide), grime reaches about 6% of stave texels in a thin line by each hoop, and both sample materials
  warn at 1024 and not at 512 (1.5 and 1.8 s at 512, 6.6 and 6.3 s at 1024, Debug, with other jobs on the runner).
- **Acceptance:** two rounds on Opus 5.5 with no file tools. Both recovered from the planted path through `check`'s
  warning, weathered the steel, left the grip clean, showed the mask, saved and exported, with `render_glb` equal to
  `render`. Both pass all six checks under the final rules, and the person approved both looks.
- **`cairn milestone M-29 verify` (2026-11-25, at 677d089):** 12 of 12 members done and merged; 29 suites pass, 12
  ran and 17 carried; the one failing check is `review`, which is written after the person's go.

**Not as planned.**

- **Twelve members where nine were planned, and 36 agents where the plan's table implies about 14.**

  | Member | Agents | Models | Why more than planned |
  |---|---|---|---|
  | T-160 | 2 | Opus | hand-off at the call count |
  | T-161 | 2 | Opus | hand-off at the call count |
  | T-162 | 2 | Sonnet | build, then the frame review (as planned) |
  | T-163 | 3 | Opus | planned as two; the second ran to 128 calls and still handed off |
  | T-164 | 1 | Sonnet | as planned |
  | T-165 | 5 | Sonnet, then Opus x4 | a failed check, its diagnosis, the person's ruling on `bake`, a rebase, the review |
  | T-166 | 2 | Sonnet | hand-off at the call count |
  | T-167 | 5 | Opus | planned as three; two stops for the person's thresholds and three questions |
  | T-207 (added) | 5 | Opus to plan, Sonnet x4 | three builders in turn and a close |
  | T-168 | 6 | Sonnet; rounds on Opus 5.5 | planned as two; three stops for rulings and one for B-10 |
  | B-10 (added) | 2 | Opus | hand-off with `verify` failing |
  | B-11 (added) | 1 | Sonnet | |

- **`bake` changed** (T-165). The spec's "the node and the bake are one computation, byte-identical" did not hold
  beyond the padding, where a mesh node holds its neutral value and `bake` held 0. The person chose to make `bake`
  write the neutral value there. Six of M-24's `uvs` frames moved in those texels and one written-down number changed
  (0 to 255); no `render`, `render_glb` or export frame moved.
- **T-207, carve's descriptions under Claude Code's cut,** was added on the person's ruling after four members each
  found a description already past 4,096 characters. Seven were over (`render` 19,122) and five within 10%. Every
  one is now 3,686 or less, held by a test with no exemption, and a new `help` tool serves what left them, in nine entries (get_mesh's is B-10's).
- **B-10, `get_mesh` on a large mesh.** Round 1's agent could not read the wrench's 53,645-character answer and tried
  `Bash`. `get_mesh` gained `include` and `faces`; round 2's agent read the wrench with `include` and no refused call.
- **B-11, the 1024 bake tests.** Three times a test that bakes both materials at 1024 through the real process timed
  out at 30 s on a loaded machine, the last in this milestone's own verify. Those calls now wait 120 s.
- **The acceptance checks were ruled four times after numbers were seen:** the margins after round 1; the grip
  check rebuilt to measure what the grip's faces wear (as written it measured a material where it is not drawn); the
  crevice set narrowed to the drive's ends after round 2 failed on the whole drive (the distance, 0.004 m, chosen with
  round 2's number in view, and the person told so before ruling); and, in the review, narrowed again to the drive's
  four visible side faces.
- **Wear reads as an edge highlight.** The person said so of round 1, and both frame reviewers said the same of the
  barrel ("regular, like a picture frame"). The worked example taught for `mesh_curvature` was a bare curvature band;
  it now breaks the band with noise, and round 2's wear came and went along the edges. The sample `weathered-iron` was
  not retuned. Recipes and guidance to agents as such are T-208.
- **T-167's stage 3 was ruled on, then narrowed.** The iron graph's noise lives in UV space, so a re-unwrap moves the
  wear and D-6's rule cannot hold on the whole frame. The person ruled D-6 on stave and cap pixels and threshold (1)
  on the hoops; to keep the caps' UVs still the step then unwrapped the hoops and their walls only, so D-6 there holds
  by construction and proves little. The person was told with the frames.
- **Rules broken by agents.** Four ran past the 60-call checkpoint (T-163's second to 128, B-10's first to 88); one
  ran `dotnet test` by hand; one re-ran a suite after a second failure where its brief said to stop; one committed
  `test-artifacts/` and untracked it. Every `finish` left its worktree folder behind (the agent's shell was in it).
- **Runs that hung or flaked:** a `verify` killed at 30 minutes (T-162), an MSBuild node crash cached against a
  commit (T-166), a `cairn test run` that printed nothing for 30 minutes (T-165), and `server-regression` timing out
  on extension-loading tests under load (B-10), beside B-11's.
- **Evidence did not go where two tasks' Notes said** (the container's own `artifacts` folder): a write there was
  refused, so evidence stayed in each worktree's ignored `artifacts/` and review sets went to the `carve/M-29` folder
  of the shared artifacts drive.

**Never verified.**

- **`help`.** Neither acceptance agent called it, so nothing shows an agent finding what left a description. The
  cut itself was not re-probed (4,096 is T-170's figure on Claude Code 2.1.329), nor whether parameter descriptions
  are cut.
- **The acceptance is two rounds on one model and one prompt.** Sonnet, or any second model, was not run.
- **B-11 on a loaded machine:** the load cannot be made on demand; its runs passed on a quiet one.
- **Times** on an idle machine, in a Release build, or of a bake at 4096.
- **The cache's 512 MB cap:** no test evicts.
- **A graph material on a part tree, a kept level or a skinned export;** occlusion between parts a part tree moves.
  (A piece export, and cancelling an export mid-bake, were settled in the review: T-218.)
- **A mesh node inside a nested graph file, in carve:** patina's side was verified in the review, found wrong and
  fixed (T-219); no test bakes one in carve.
- **`get_mesh`'s narrowed sizes** on any model but the wrench.
- **Frames by eye:** T-164's `render_glb` frames; the 256 and 1024 frames of the barrel beyond the front and rim
  close-up; most of the acceptance sessions' own images; a swatch of `weathered-staves`.
- **The breaks of T-167** in the sizes suite.
- **The README's examples** are run by no test, as before.

**Known and left.**

- **Beyond the padding, `bake`'s maps disagree:** occlusion, curvature and masks hold their neutral value; base
  colour, the normal and the ORM hold 0, so the ORM's occlusion channel reads 0 where `occlusion.png` reads 255.
  Nothing samples those texels. Put to the person and not ruled; left.
- **The padding fringe no longer shows around white islands** in the steel mask and occlusion `uvs` frames (neutral 1
  draws as the padding's grey). The person saw it in T-165's review and approved.
- **Two `get_mesh` asks on the wrench are still over 25,000 characters** (every face with faces and uvs; a group with
  every section). The description and README say to ask a few sections of one group at a time.
- **B-10's default-answer test names the path of the worktree its digests were taken in,** to undo absolute texture
  paths in one model's answer.
- **Five descriptions sit within 20 characters of the 3,686 budget.** The next sentence added to one must move
  something into `help`.
- **The slow-bake warning** is drawn by both sample materials at 1024.
- **The hoops carry 32 thin wedge walls** from `extrude`; the reviewer read them as joints.
- **Carve file version 10 is now graph slots.** T-145 (M-32) is titled "Carve file version 10: weight tracks in
  clips" and needs its version planned again.
- **`render` can answer with an error in place of a frame:** if edits land between its bake and its frame on each
  of 3 tries, it says so and asks to be called again (T-218). The fault it replaces, a frame drawn with empty graph
  slots, took three fixes (T-163's shortcut, T-218, T-226).
- **A graph file rewritten and restored during one bake** can be cached under the wrong bytes (T-229, off-milestone).
- **A render can return stale graph slots** when an undo or a second render straddles a graph save (B-12,
  off-milestone); it does not last past the next render.
- **A test reaches an internal of `Carve.Hosting` by reflection** to mark the engine ready (T-219's `swatch` note
  test), for want of a public seam; it carries a `shortcut:` comment.
- **Member worktree folders** under `branches/task` and `branches/bug`, unregistered from git and not deleted.
- **T-208** (recipes and guidance for agents), reserved off-milestone at the person's word.

**The test report** (`cairn test report --since 2d`, 2026-11-25): `server-regression` cost most (4,768 slot-seconds,
38% carried, 3 failures, the flakes above); `scenario-weathered` shows 10 failures, all break runs that are meant to
fail; `server-unit` and `graphs-unit` show 3 jobs each that never ran (the build crash and its cache). Filed: B-11.
Not filed: the hung `cairn` runs and the cached failed build are cairn's own, and are named here for the person to
pass on.

**For the next plan.**

- **Measure every tool description against the cut at planning,** in both servers, and plan the budget for each
  member that adds a sentence. M-28's retrospective said to check carve's; it was not done, and it cost a member.
- **Call a tool the way a fresh agent will, on the acceptance model, at planning:** `get_mesh {}` on the wrench would
  have shown B-10 before round 1.
- **A check that says "byte-identical" names the region** it holds over, and planning probes it. T-165's did not hold
  beyond the padding, and T-167's D-6 step could not hold after a re-unwrap.
- **Fix an acceptance check's sets and statistics before round 1,** with a reference that passes and negatives that
  fail, so a ruling after a round is about a margin and not about what is measured.
- **Plan what an agent is taught, not only what it can call.** The worked example decided how round 1's wear looked.
  T-208 is where recipes get planned; M-30's wear and dirt layers should come with them.
- **Count on stops, not only hand-offs.** A member whose thresholds are the person's needs an agent before and after
  each ruling: T-167 and T-168 each took twice their planned agents for that reason.
- **Give a regression test that bakes at 1024 its own limit from the start,** or keep such bakes in a scenario suite.
- **Name evidence paths the agents can write:** the worktree's `artifacts/` and the shared artifacts drive, never
  the container root.
- **Have each agent leave its worktree before `finish`** (a `cd` to the milestone's worktree first), so the folder
  can be removed.

**After the person's go (2026-11-25 to 2026-11-26).**

- **No rebase at first:** `origin/main` had not moved since the milestone forked.
- **Refactor pass (Opus 5.5):** three cuts, no behaviour or test changed: one `{graph, output, size}` parser shared by
  `define_material` and `render`; `open`'s missing-file error through the shared helper; one unbound node registry for
  reading graph slots. Named and left as taste: five small duplications. Not read: the scenarios and new tests.
- **Review round 1, RV-82:** five Opus 5.5 reviewers, split by area (the Core and `Carve.Graphs`; the server's bake,
  render and export; the descriptions, `help` and `get_mesh`; patina and the samples; the scenarios, the acceptance
  and the whole diff). 36 findings: 6 Major, 18 Minor, 12 Nit.
  - **The Majors:** the bake cache's key left out the slot, so two slots of one graph output shared a bake (found
    from both sides); patina said a mesh node with a bad parameter evaluates, then failed with a generic error; a
    graph nesting a mesh-reading graph got no "mesh inputs neutral" note and `check` called its mask broken; the
    acceptance crevice check counted a socket face as drive; `graphs-unit` lacked sample files among its inputs.
  - **What held:** no sentence was lost from the trimmed descriptions; the sample PNGs equal their graphs; the
    thresholds and margins were coded as ruled; no scenario references a product library.
  - **Answers:** five fix members, T-217 to T-221, eleven agents (T-217 two, T-218 four, T-219 three on Opus 5.5;
    T-220 and T-221 one each on Sonnet 5.5). 35 fixed, 1 filed (bake memory, T-222).
- **Review round 2, RV-83:** two Opus 5.5 reviewers checking the answers against the code. 31 of 36 held. 15 findings:
  3 Major, 7 Minor, 5 Nit.
  - **Did not hold:** a model could still be drawn with empty graph slots (the pump read the bake twice); a user's
    `o@mesh` material was still renamed on export; the Patina scenario's title break still proved only arithmetic;
    `check`'s new text for a mesh-reading output was untrue; one clause of `CLAUDE.md`.
  - **New from the fixes:** with the slot in the key, a slot that refuses its output was served another slot's bake.
  - **Answers:** T-226 (Opus 5.5), T-227 (Opus 5.5), T-228 (Sonnet 5.5), one agent each, and the orchestrator for the
    milestone file. 14 fixed, 1 filed (T-229).
- **Review round 3, RV-84:** one Opus 5.5 reviewer. All 15 answers held, and the pump's third fix held for the fault
  it was for: with the old code put back, its tests fail. 2 findings: a frame with stale, not empty, graph slots can
  come back when an undo or a second render straddles a graph save (filed as B-12), and a commit hash in an As built
  (fixed: As builts now name a commit by its subject, which a rebase does not change).
- **`main` moved during the review:** T-223 (`displace`, a `marble` sample, `blend`'s `difference` mode) and releases
  to 0.18.2, 26 commits. **Rebase (two Opus 5.5 agents):** seven conflicts, none of intent, each resolved keeping both
  sides; one fix, a doubled project reference. `displace`'s description was already under the limit and the tools/list
  digest held. Frames: every frame the milestone had before the rebase is byte-identical after it, and every frame of
  `main` is too, except 15 bake frames that M-29's own neutral fill moved.
- **Review of the rebase, RV-90:** one Opus 5.5 reviewer. The rebased tree equals a clean merge apart from the
  conflicts' resolutions and the fix. 3 findings (1 Minor, 2 Nit), all about `displace` given a mesh-reading graph:
  it refuses safely but says the graph is faulty and to fix it in patina. Filed as T-236.
- **The person's rulings on the findings** are under "Decided while it ran". Two findings came to the person because
  they survived two rounds (the empty slots, the title break); both were fixed a further time and held.
- **Agents:** 26 after the go (one refactor, nine reviewers, fourteen fixers, two for the rebase), 62 in all.
- **Not as planned after the go:**
  - **The render path took three fixes** for one fault, and its neighbour (stale slots) is left as B-12. The first fix
    marked a frame; the second round found the mark could lie; the third made it one read.
  - **A fix made a new fault:** putting the slot in the cache's key let a refusing slot take another's bake.
  - **Two answers ruled by the person were implemented untruly at first** (`check`'s text; the crevice set's code
    before the review), and were caught only by a reviewer reading the code against the ruling.
  - **A brief was wrong:** it told fix agents to put the commit and test name on a review's Outcome line, which
    `cairn lint` refuses; three agents were corrected by message.
  - **Checkpoints were overrun again:** eight of the fourteen fix agents ran past 60 calls.
  - **Flakes:** a native crash at engine start-up in `scenario-patina` (once, passed on re-run), and a runner that
    failed every suite with "build 'solution' already ran at this commit" (once). The earlier ones did not recur.
- **Left, beside "Known and left" above:** T-222 (bake memory), T-229 (a file rewritten and restored during a bake),
  B-12 (stale slots), T-236 (`displace` and a mesh-reading graph), T-208 (recipes); scratch copies under the temp
  folder; the unregistered member worktree folders; the local milestone branch, diverged from its remote by the
  rebase.
- **Next time:**
  - **Check `main` before each review round, not only the first.** It moved between rounds 1 and 3 and cost a rebase
    and a fourth review. M-27's retrospective said to check before reserving the review; that was done once.
  - **Give a concurrency fix a reviewer's read before it is called fixed.** Each round's fix was right for the order
    of events its finding named and wrong for the next one.
  - **Have a ruling's implementer quote the ruling in a test**, so the text or the set ruled is what is held.
  - **Say in every fix brief that an Outcome line is a bare `fixed`,** and that an As built names a commit by subject.
  - **Round 1's split by area and round 2's split by fix member both worked;** most findings were where the briefs
    pointed.
