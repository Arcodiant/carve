---
description: >-
  patina defines, saves and swatches graphs with mesh-input nodes on neutral stand-ins and says so, and samples/graphs gains weathered-iron and weathered-staves with their PNGs
---
# T-162 — patina knows the mesh nodes, and the weathered sample graphs

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-161

**Verify:** patina-unit, scenario-patina

## Goal

An agent authors a mesh-reading graph in patina: `define_graph`, `edit_graph`, `save`, `open`, `swatch`, `export` and
`check` all take the four mesh-input nodes, evaluated on their neutral stand-ins, and every answer that shows pixels
says so. The two graphs the weathered barrel uses are committed with it.

## Scope

- **`Patina.Server`** references `Carve.Graphs` and builds its registry with T-161's registration call and no source,
  so the mesh nodes are known and neutral (curvature 0.5, occlusion 1, mask 1, position `#808080`). The registry is
  made in one place; every tool that validates or evaluates uses it.
- **Saying so:**
  - `define_graph`'s node list (`NodeCatalogue`) describes the four types, their parameters in metres, and one sentence:
    in patina they are stand-ins, and the real maps come from the mesh when a carve material names the graph;
  - `get_graph` and `open` add a line when the graph has mesh nodes, naming them;
  - `swatch`'s title strip adds `mesh inputs neutral` and its text result says which nodes; `export` and `check` add the
    same note to their results. A graph with no mesh node answers byte for byte as before (a test holds the planks'
    sheet and results unchanged).
  - A worked example in `define_graph`'s description, run by its test: a `mesh_curvature` node driving a `blend` mask.
- **`tools/Patina.SampleGraphs`** evaluates with the same neutral registry and says so on the line it prints.
- **Content, the two weathered graphs** (`samples/graphs/`), each with its PNGs from the sample tool (neutral) kept
  equal by a test as `PlanksGraphTests` keeps the planks, and each run against `check`'s rules in a test:
  - `weathered-iron.texgraph`: dark iron (metallic 1, roughness about 0.45) with bright worn metal where
    `mesh_curvature` (width 0.02) is high. `levels` on the curvature gives the named output `wear` (0 on flat metal,
    1 on a sharp convex rim), broken up by a noise; `wear` blends the base colour toward bare steel and lowers
    roughness. Outputs `base_colour`, `roughness`, `metallic`, `wear`.
  - `weathered-staves.texgraph`: `planks.texgraph` used as a `graph` node (T-140), with grime where `mesh_occlusion`
    (32 rays, reach 0.15) is low. `levels` on one minus occlusion gives the named output `grime`; `grime` blends the
    planks' base colour toward a dark brown and raises roughness. Outputs `base_colour`, `roughness`, `normal`,
    `occlusion` (the planks'), `grime`.
  - With neutral inputs `wear` and `grime` are 0 on every texel, so both graphs' neutral PNGs show clean iron and the
    planks' own staves: a test holds `weathered-staves`' neutral `base_colour` equal to the planks' PNG byte for byte.
    `check`'s `one-value` rule will report `wear` and `grime`; the note above explains it, and the test expects exactly
    those two problems and no other.
  - Exposed parameters (T-139): `wear_amount` on the iron and `grime_amount` on the staves, each 0 to 1.
- **scenario-patina** gains one stage file and one line in `PatinaStages.cs`: `MeshNodesStage.cs` defines the iron
  graph through the tools, swatches it (the sheet and the sphere) and checks the title's note, that `wear` is black on
  every texel and that the base colour equals the graph's clean iron, then saves, opens and exports it and compares
  the PNGs with the committed ones. One break on the scenario side (the note removed from the expected title) shown
  failing.
- **Tests** (`patina-unit`): every tool with a mesh-node graph, the worked example as written, the notes, the unchanged
  planks answers, the sample graphs equal to their PNGs.
- The person looks at both graphs' PNGs and the swatch sheet before this lands (a review agent opens them first,
  cropped or scaled, and says what it sees).
- CLAUDE.md's rows for `src/Patina.Server`, `tools/Patina.SampleGraphs`, `samples/` and the Patina scenario; the
  README's patina section.

## Notes

- **Decided at planning (2026-11-20, by the person):** patina registers the mesh nodes with neutral stand-ins and says
  so on the swatch, so a mesh-reading graph can be authored, saved and swatched there.
- **The orchestrator's calls, each a stop if it proves wrong:** the two graphs' names, outputs and exposed parameters;
  that neutral inputs leave `wear` and `grime` at 0; `check` reporting `one-value` on them rather than being silenced.
- **Content comes with its mechanic:** these graphs are the mesh nodes' content and land in the wave after T-161, before
  anything draws them on a mesh. T-163's tests and T-167's scenario use them; their look on the barrel is T-167's to
  show and the person's to approve there, and a retune of a `levels` range is expected then.
- patina reaches `Carve.Core` through `Carve.Graphs`. Both are engine-free, and `ProductReferenceGuard` (no extension
  referenced) still holds; say in As built what patina's publish folder gained.
- A scenario references no product library (RV-68): the stage takes its texels from patina's own tools and the
  committed PNGs. Breaks live in the scenario, never in the product.
- Sonnet, with a separate agent for the frame review. Runs beside T-163; it owns `src/Patina.Server`,
  `tools/Patina.SampleGraphs`, `samples/graphs/` and the Patina scenario, and changes nothing in `src/Carve.Graphs`.
- **Write down for T-163 and T-167** (As built): the graphs' node ids and parameter values, the measured ranges of
  each neutral output, and the commands that remake the PNGs.
- **Stop and ask:** a planks PNG or an existing swatch frame that changes; a `check` threshold that would have to move.
