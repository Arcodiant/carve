---
description: >-
  A Carve material names a texture graph output, graphs read the mesh's baked maps, and Carve re-bakes them when the mesh or UVs change
---
# M-29 — Graphs on a mesh

**Status:** Planned

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
| 5 | T-168 acceptance | Sonnet (inner sessions on the default, Opus) | T-167 | its round folders, prompt and checks |

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

## Retrospective
