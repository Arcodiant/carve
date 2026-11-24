---
description: >-
  define_material takes {graph, output, size} for any texture slot in place of a PNG path, kept in the Carve file as version 10 relative to the file, and read back by get_mesh, save and open
---
# T-160 — Graph slots in materials and the Carve file

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit

## Goal

A material's texture slot holds either a PNG, as today, or the name of a texture graph output: a `.texgraph` path, an
output and a size. `define_material` takes it, `get_mesh` reports it, and the Carve file keeps it through `save` and
`open` as version 10. Nothing is evaluated yet: T-163 draws it.

## Scope

- **`Carve.Core`** (`Material.cs`): a slot is exactly one of a PNG (`MaterialTexture`, unchanged) or a graph reference
  holding the graph file's absolute path, the output's name and the size. The Core holds no graph content and does not
  reference `Patina.Graphs`: graph files are read again at each render and export (the person, M-29's planning), so the
  reference is three plain values, equal by value. `Material.Textures`, `HasTexture`, `OcclusionHasNoEffect` and
  `Material.Problems` cover both kinds (an occlusion slot from a graph counts as an occlusion map). `Material.Problems`
  checks a graph reference's shape only: an absolute path ending `.texgraph`, an output matching `[a-z][a-z0-9_]*`, a
  size that is a power of two from 64 to 4096.
- **`define_material`** (`src/Carve.Server/MaterialTools.cs`): each of the five texture arguments takes a string (a PNG
  path, as now) or an object, as agreed:

  ```
  define_material {
    name: "weathered-iron",
    base_colour_texture:        {graph: "graphs/iron.texgraph", output: "base_colour", size: 1024},
    metallic_roughness_texture: {graph: "graphs/iron.texgraph", output: "roughness"},
    normal_texture:             "textures/iron-normal.png"      // a PNG, as today
  }
  -> {replaced, material: {...,
        base_colour_texture: {graph: "<abs>/graphs/iron.texgraph", output: "base_colour", size: 1024},
        metallic_roughness_texture: {graph: ..., output: "roughness", size: 512},
        normal_texture: {path: "<abs>/textures/iron-normal.png", width: 256, height: 256}},
      warnings: []}
  ```

  - `graph` resolves as a texture path does (relative to carve's working folder, or absolute), must end `.texgraph` and
    must exist. A missing file is refused with the `.texgraph` files in the folder it looked in listed, in the words of
    the missing-PNG error (`DefineMaterialTool.Missing`, generalised by extension, T-81).
  - `output` is required. `size` is optional: when left out it is the graph's own `size` (read from the file with
    `TexGraphFile.ReadFile`; `Carve.Server` gains a project reference to `Patina.Graphs` for this), clamped into 64 to
    4096, and the result shows the size taken. A size outside the powers of two from 64 to 4096 is refused, naming the
    argument and the range.
  - An object with a key other than `graph`, `output`, `size`, or without `graph` or `output`, is refused naming it.
  - This task reads the graph file only for its `size` and to refuse a file that is not a texture graph at all (a load
    error). Whether the output exists and what it carries is T-166's.
  - The `reads_mesh` field of the agreed sketch is added by T-163, which knows the node types.
  - An `emissive_texture` from a graph makes a default emissive `#FFFFFF`, as a PNG does.
  - **`@` is refused in a new material's name** (`Material.NameProblem`: "a material name must not contain '@' (an export
    names a material's per-mesh copies `<material>@<mesh>`)"), as `:` and `#` are. A file that already has such a name
    still opens; a test holds that.
  - The description gains the object form, the rule that a graph is read again at every render and export (unlike a
    PNG, which is read once), and a worked example run by its test. The example's graph is written by the test into
    the tool folder (T-162 owns `samples/graphs/`).
- **`get_mesh`** lists a graph slot as `define_material`'s result does.
- **The Carve file, version 10** (`CarveFile.cs`): version 9's content plus graph slots. A slot is written
  `"base_colour_texture": {"graph": "graphs/iron.texgraph", "output": "base_colour", "size": 1024}`, the path relative
  to the file's folder with `/` separators, as a texture path is; `size` is always written.
  - A model with at least one graph slot is written as version 10 whatever else it has (parts, levels, targets or
    none of them). Every model without one is written byte for byte as it is today: a test writes every committed
    sample and a model of each version 2 to 9 and compares bytes with `main`'s.
  - Reading resolves the path against the file's folder. **A graph file that is missing or broken does not refuse
    `open`**: the slot is kept and `open` warns, naming the material, the slot and the path (the orchestrator's call,
    below). A PNG keeps its rule (missing refuses).
  - `save` refuses a graph on another drive in the words of `CarveFile.TextureProblem`.
  - Round trip bit-exact: write, read, write gives the same bytes; a v10 fixture is committed in
    `tests/Carve.Core.Tests/Fixtures/`.
- **Until T-163 lands** (inside the milestone branch only): `render` and `export` of a model with a graph slot are
  refused with one line saying graph materials are not drawn yet. T-163 removes it. Nothing reaches `main` with it.
- **Tests**: Core unit tests of the slot, `Problems` and the file (every refusal, the round trip, byte identity of
  older versions, the `@` rule); `ToolHarness` unit tests of `define_material` (every refusal in its words, the worked
  example as written, `get_mesh`, `save` and `open`, the open warning for a missing graph, undo and redo of a define).
- CLAUDE.md: the architecture rule's version list gains "10 graph slots, version 9 with a slot naming a `.texgraph`
  output, T-160".

## Notes

- **Decided at planning (2026-11-20, by the person):** the object per slot with its own size, default the graph's own;
  the range 64 to 4096; file version 10 as version 9 plus graph slots, written only for a model that has one; `@` kept
  for export's per-mesh copies; graph files read at each render and export, not held as PNG bytes are.
- **The orchestrator's call, a stop if it proves wrong:** a missing or broken graph does not refuse `open` (it warns),
  because the graph is read at render time anyway and T-168's agents, who have no file tools, must be able to open a
  model with a wrong graph path and fix it with `define_material`.
- Design-heavy and it decides a format and a public Core API that T-163, T-164, T-165 and T-166 build on: Opus.
- **Write down for them** (As built): the Core types and how a slot is told apart, the helper that resolves and lists
  a missing `.texgraph`, the JSON shape in the tools and the file, and where the interim refusal is.
- **Pins this task will trip:** the tools/list digests and the unchanged-tools digest (`define_material`'s description
  changes), and any test listing file versions. Re-take a pin only where this task's own text changed it, and say which
  in As built; a frame or export hash that changes is a stop.
- Runs beside T-161, which adds a new project and touches none of these files. `Carve.slnx`, `tests.json` and
  CLAUDE.md conflicts only add: keep both sides.
- **Stop and ask:** a rule the spec does not settle (a slot kind the description cannot explain, a version question),
  or any check that would have to loosen.

### Hand-off

Ran on Opus 5.5 (claude-opus-5-5). Stopped at the 60-call checkpoint; code and tests committed, `verify` not yet run.

**Done.**
- Core (`Material.cs`): `abstract record MaterialSlot(string Path)`; `MaterialTexture : MaterialSlot` (otherwise unchanged) and
  `MaterialGraph(string Path, string Output, int Size) : MaterialSlot` (equal by value; `Extension`, `MinSize`,
  `SizeProblem`, `OutputProblem`, `Problem()`). A slot is told apart by type (`slot is MaterialGraph`). The five slot
  properties are `MaterialSlot?`; `Textures`, `HasTexture`, `OcclusionHasNoEffect` and `Problems` cover both; new
  `HasGraph`. `NameProblem(name, existing = false)` refuses '@' only for a new name; `Problems`, `FaceStyle.WithMaterial`
  and the file reader use `existing: true`, so a file with '@' still opens.
- `CarveFile.cs`: `GraphSlotsVersion = 10`, readable; written for any model with a graph slot, laid out as version 9
  (`HoldsParts(10)`, mesh keys V9). Slot written `{"graph": rel, "output", "size"}`; read by `ReadGraphSlot` (all three
  required, unknown key refused, path resolved against the folder, graph file not read). An object slot in v<10 is
  refused as "not a string". `TextureProblem` says "graph" for a graph on another drive.
- Server: `Carve.Server` references `Patina.Graphs`. `define_material` takes a string or an object per slot
  (`LoadGraph`; `ReadGraph(fullPath, out graph)` is the shared read: missing / load error / IO, validation faults ignored;
  `Missing(target, extension)` generalised). Size default the graph's own clamped to 64..4096. Description, schema
  (`anyOf` string or object) and a second worked example. `MaterialJson.Of` reports `{graph, output, size}`;
  `MaterialJson.BrokenGraphs` gives open's `material_warnings`; `MaterialJson.GraphsNotDrawn` is the interim refusal,
  called in `RenderTool` (after the part check) and `ExportTool` (after the no-mesh check) — T-163 removes both calls.
  get_mesh, save and open descriptions gained a sentence each. `SaveTool.MissingTextures` and the glb/PBR builders
  take PNG slots only.
- Tests: `tests/Carve.Core.Tests/GraphSlotFileTests.cs` (slot, Problems, '@', v10 write/read/round trip, the fixture
  `Fixtures/graph-slots-v10.carve`, other drive, malformed slots, v9 refusal, every sample byte for byte, and v2..v9
  models pinned by SHA-256 taken from main's writer with T-160's Core changes stashed — the same hashes with them);
  `tests/Carve.Server.Tests/GraphSlotToolTests.cs` (worked example, mix and emissive, every refusal, missing listing,
  load error, '@', undo, save/open, open warnings, render/export refusal). Version-list pins updated (9 -> "9 and 10",
  the "unknown version" probes moved from 10 to 11) in `CarveFileTests` and `SaveOpenToolTests`; `.Png` casts in the
  materials, planks and export tests. Both new classes pass; core-unit passed in full before the server work.

**Left.**
- `PartToolsListTests.Before` (tools/list digest) must be re-taken: changed by define_material's, get_mesh's, save's
  and open's own text and define_material's schema, all this task's.
- Check every tool description stays under 4,096 characters (define_material, open and save all grew).
- Run `cairn task T-160 verify`; then CLAUDE.md's version list ("10 graph slots, version 9 with a slot naming a
  `.texgraph` output, T-160") and README (define_material's slot form, version 10) — not yet edited; As built; lint; finish.

**Surprising.** carve has no redo, so the spec's "undo and redo of a define" is tested as undo of a define and of a
replace. The graph's size when left out is clamped but a graph with a size outside 64..4096 was not tried.
