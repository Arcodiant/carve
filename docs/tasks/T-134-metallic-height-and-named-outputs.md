---
description: >-
  Texture graphs gain metallic (packed into the glTF metallic-roughness image), height and named outputs, list parameters and format version 2's machinery, with export and the swatch sheet showing them
---
# T-134 — Metallic, height and named outputs

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-133

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

A graph can say how metallic a material is, give its height, and give extra images by name. `export` writes them and
`swatch`'s sheet shows them. The `.texgraph` format also gains what the later M-28 tasks need from it: list parameters,
and a version 2 that a file is written in only when it needs it.

## Scope

- **Roles** (`Imaging/OutputPacking.cs`, `Nodes/OutputNode.cs`), in this order: `base_colour`, `normal`, `roughness`,
  `metallic`, `occlusion`, `height`:
  - `metallic`, greyscale, is packed into `roughness.png`'s blue channel, glTF's metallic-roughness image: R 255,
    G roughness, B metallic. Without a metallic output, B stays 0, so every committed PNG keeps its bytes. A graph with
    `metallic` and no `roughness` is a fault naming the output node ("metallic is packed with roughness, so the graph
    needs a roughness output too"). The packing table in `OutputPacking`'s comment and `export`'s description say so.
  - `height`, greyscale, is written as `height.png`, 255 times the height in R, G and B. Carve does not use it yet.
- **Named outputs**: an `output` node with a `name` (`[a-z][a-z0-9_]*`, not a role's name) in place of a `role`, either
  kind, written as `<name>.png` (greyscale spread to grey, colour as sRGB). Giving both `role` and `name`, or two
  outputs the same role or name, is a fault. These become a nested graph's extra ports in T-140, and the masks M-30 will
  layer with. `export`'s `outputs` argument and `swatch`'s `output` take a name as they take a role.
- **The swatch sheet** (approved at planning, below): a column per output, the roles in role order and then the named
  outputs a to z, each flat over tiled 3 x 3, with the sphere to the right. The sphere wears base colour, normal,
  roughness with metallic, and occlusion. Height and named outputs are shown on the sheet but not worn. `metallic`,
  `height` and greyscale named outputs are drawn grey. A role the graph lacks has no column, as now, so the planks'
  sheet keeps its bytes.
- **The sphere wears metallic**: `swatch`'s sphere passes the packed metallic-roughness image as carve's renders do
  (T-128's lights). A scenario-patina check: a graph with metallic 1 and base colour grey 200 is drawn on the sphere
  with its highlight tinted by the base colour, against metallic 0 drawn white. Measure it, put the threshold to the
  person with the frames, and write it in the stage.
- **List parameters** (`TextureGraph.cs`'s `ParamValue`, `TexGraphFile.cs`, `ParamSpec`): `ParamKind.Floats` (numbers,
  a count range, each in a range) and `ParamKind.Colours` (`"#rrggbb"` strings, a count range). They are read back and
  written bit-exact, and `Describe()` and `Check()` give a range in words as the other kinds do. T-137's `gradient_map`
  is their first user. A file with them is still version 1, since an older reader would only fail on the node's
  type.
- **Version 2's machinery**: `TexGraphFile` reads versions 1 and 2, and writes the lowest version that holds the graph,
  as the Carve file does. Version 2 is reserved for T-139's `parameters` block and T-140's `graph` and `input` nodes. This
  task builds the switch and its round-trip tests on a stand-in top-level field held only in the tests (no version 2
  feature ships here). Every committed `.texgraph` is written byte for byte as before.
- **Tests (unit)**: the packing of each new role and a named output at chosen texels; the faults above; the round trip of
  both list kinds and of version 1 and 2; and the sheet's column order and its cell positions for a graph with every
  role and two named outputs, worked from the texels. The planks' PNGs and sheet are unchanged (byte-equal).
- **Content**: `define_graph`'s, `export`'s and `swatch`'s descriptions name the new roles and named outputs, each with
  a worked example that the description tests run as written. The examples' graph is small: a grey plate with
  `metallic` from a `gradient` and a named `edge` output. scenario-patina's swatch stage draws it as one sheet and one
  sphere, every pixel worked from the texels as `SwatchStage.cs` does.

## Notes

- **Decided at planning (2026-11-19, by the person):** the roles `metallic` and `height`, and named outputs. The packing
  of metallic into `roughness.png`'s B channel keeps M-27's binding (`barrel-planks.carve` names `roughness.png` as its
  `metallic_roughness_texture`).
- **The sheet, as approved at planning:**

  ```
  +--------------------------------------------------------------------------+
  | painted-metal, 256 px, seed 5                                            |
  +--------+--------+--------+--------+--------+--------+--------+-----------+
  |base_col|normal  |roughnes|metallic|occlusio|height  |wear    |           |
  |  flat  |  flat  |  flat  |  flat  |  flat  |  flat  |  flat  |  sphere   |
  |        |        |        | (grey) |        | (grey) | (grey) |  wears    |
  +--------+--------+--------+--------+--------+--------+--------+  base,    |
  | tiled  | tiled  | tiled  | tiled  | tiled  | tiled  | tiled  |  normal,  |
  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  | 3 x 3  |  rough+   |
  |        |        |        |        |        |        |        |  metal,   |
  +--------+--------+--------+--------+--------+--------+--------+  occl.    |
    roles in order: base_colour normal roughness metallic occlusion height,
    then named outputs a..z; a missing role has no column (as now)
    cells 256 px; 7 columns + sphere 512 = 2310 px wide
  ```

- Decides formats others build on (the roles, list parameters, the version switch): Opus. The frame review of the
  sheet and sphere is a second agent, and the person approves the frames before it lands.
- **Write down for the tasks that depend on it** (As built): the list kinds' file shape and messages; how a version 2
  feature is added (the hook T-139 and T-140 fill in); the role order and the column rule.
- Owns `TexGraphFile.cs`, `TextureGraph.cs`, `ParamSpec`, `OutputPacking.cs`, `OutputNode.cs` and the swatch sheet in
  wave 2. T-135 and T-136 run beside it and add node files and registry lines only; they may use the roles that already
  exist.
- **Stop and ask:** any committed PNG or frame that changes; a sphere threshold; metallic packed anywhere else.
- Breaks for the frame checks are made on the scenario side, never by a switch in the product (M-27's ruling on T-114).

### Hand-off

Written by Claude Opus 5.5 (`claude-opus-5-5`), first agent, at its call checkpoint (about 58 tool calls). Not verified
yet: `cairn task T-134 verify` has not been run; the patina-unit tests were run by hand (below).

**Done (committed):**
- *Roles* (`Imaging/OutputPacking.cs`): `OutputRole` is `BaseColour, Normal, Roughness, Metallic, Occlusion, Height`
  (role order); `OutputRoles.Order` (roles in role order, then named a to z), `FileOf` (`metallic` -> `roughness.png`),
  `PackedAs` (a named output packs as `base_colour`: grey spread, colour sRGB); `PackTile` packs metallic and height grey;
  `PackMetallicTile` writes B only. Packing table in the class comment.
- *Named outputs* (`Nodes/OutputNode.cs`, `GraphValidator.cs`): parameter `name` (`ParamSpec.Identifier`, optional, no
  default); `OutputNode.GivenName`. Faults: role and name both (on `name`), a name that is a role's, two outputs one
  name, metallic with no roughness (on the metallic output's `role`, message as the spec). `GraphAnalysis.Outputs` is
  keyed by output name (role's name or own name).
- *Evaluator*: `Outputs` (sheet order), `Named`, `Has`, `KindOf`, `Evaluate(string output, ...)`; `Evaluate(OutputRole)` kept.
- *`OutputImage`*: keyed by output name; `Rgba8` is the output alone (roughness B 0, metallic grey: the sheet's cells);
  `FileRgba8`/`Make` are the file (roughness with metallic in B; metallic throws).
- *List parameters*: `ParamKind.Floats`/`Colours` (`ParamSpec.Floats(name, minCount, maxCount, min, max, default...)`,
  `ParamSpec.Colours(name, minCount, maxCount, default...)`), `ParamValueKind.Texts` (`ParamValue.OfTexts`), an empty
  `[]` reads as numbers and either kind takes it; `NodeParameters.Floats/Colours/Has`. A mixed array is a load error
  ("an array is all numbers or all strings"). Describe: "a list of 2 to 8 numbers [a, b, ...], each from 0 to 1", "a
  list of 0 to 4 colours [\"#rrggbb\", ...]". Files with them stay version 1.
- *Version 2's machinery* (`TexGraphFile.cs`): `TexGraphFile.LatestVersion = 2`; versions 1 and 2 read; `TexGraphField`
  (abstract: `Name`, `Version`, `IsIn`, `Write`, `Read(ref Utf8JsonReader)` returning a graph transform) and
  `TexGraphFormat` (`Current` is empty; `With(field)`, `WithNodeType(type, version)`, `VersionOf(graph)`); `Write`/`Read`
  take an optional format. Fields are written after `seed`, before `nodes`. A file of a lower version using a later
  field or node type is a load error at it. T-139 adds its `parameters` field to `TexGraphFormat.Current` (and a slot on
  `TextureGraph` for its value); T-140 calls `WithNodeType("graph", 2)` and `("input", 2)` there.
- *Tools*: export (`outputs` takes names; metallic alone refused; metallic packed into roughness.png; lines for height
  and named outputs say "no Carve slot"), swatch (`output` takes names; columns = `evaluator.Outputs`; the sphere wears
  the packed roughness and sets the material's metallic factor 1 when the graph has metallic, `SwatchSpec.Metallic`),
  define_graph's text and `NodeCatalogue` (an optional parameter is listed "optional, no default"); the metal plate
  worked example (`DefineGraphTool.WorkedMetal`, `ExportTool.WorkedMetal`, `SwatchTool.WorkedEdge`).
  `tools/Patina.SampleGraphs` writes each output's file (no metallic file).
- *Tests*: `RolesAndNamedOutputsTests`, `ListParamsAndVersionTests` (stand-in field and node type held there; every
  committed `.texgraph` rewritten byte for byte), `MetalPlateTests` (server); existing tests' targets moved only where
  the spec moves them (version 3 is now the unknown one; the six roles in the no-output message; "shiny" is now a
  valid name, so the export refusal names the missing output; the planks' four roles named in the tests).
  Patina.Graphs.Tests: 140 pass. Patina.Server.Tests unit: 104 pass, 1 fails (below).

**Surprising, and left:** the sheet's label atlas (`LabelAtlas`, 512 px, 32 px pitch) holds 16 labels, a title, two a
column and the sphere's, so 7 columns at most. A graph with every role and two named outputs (8 columns, the spec's
unit test) throws `IndexOutOfRangeException` in `SwatchSheet.Compose`
(`MetalPlateTests.The_sheet_has_a_column_per_output_...`). Fix in the atlas (taller, or two labels a row) without
moving the planks' label pixels, and check the planks' sheet hash is unchanged; `SwatchRendering` draws the atlas.

**Left:** the atlas fix; scenario-patina's swatch stage (metal plate sheet and sphere worked from texels as
`SwatchStage.cs` does; the metallic-1 vs metallic-0 grey-200 highlight check, threshold measured and put to the
person); `cairn task T-134 verify`; compare the planks' sheet and committed PNGs and scenario frames by hash (fetch by
job id); the person's approval of the new sheet look (frames under `G:\My Drive\artifacts\carve\M-28\T-134\round-1`);
CLAUDE.md's Layout lines (Patina.Graphs: roles, list params, format versions); `### As built`; lint and finish.
No images have been opened.
