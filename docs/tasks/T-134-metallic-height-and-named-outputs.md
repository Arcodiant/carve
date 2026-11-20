---
description: >-
  Texture graphs gain metallic (packed into the glTF metallic-roughness image), height and named outputs, list parameters and format version 2's machinery, with export and the swatch sheet showing them
---
# T-134 — Metallic, height and named outputs

**Status:** Done

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

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`) in two agents: the first wrote the code up to its call checkpoint (its
hand-off is folded in here); the second fixed the label atlas, wrote the scenario stage, measured the metallic rule,
had it and the sheet approved, and finished the task.

**Built:**
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

- *The label atlas* (`Patina.Server/SwatchImages.cs`, `LabelAtlas`): it was 512 a side and held 16 labels (a title,
  two for each column, the sphere's), so a sheet could have 7 columns at most. A graph with every role and two named outputs
  threw `IndexOutOfRangeException`. It is now 1024 a side with 32 rows, enough for 15 columns. The rows sit where they
  did, and a label's width keeps its old cap (`LabelAtlas.LabelWidth`, 512 - Inset + Padding), so a sheet of 7 columns or
  fewer draws the same pixels.
- *scenario-patina* (`SwatchStage.cs`, `MetalPlateAsync`): define_graph's metal plate is exported at 64. There is no
  `metallic.png`. Its sheet (`swatch-plate-sheet`, 1544 x 538) has columns base_colour, roughness, metallic and edge.
  Each flat and tiled cell is checked exactly against the exported texels: roughness alone is `roughness.png` with B 0,
  metallic is that file's B spread grey, edge is `edge.png`. Its sphere (`swatch-plate-sphere`) is checked for the
  silhouette and against the sheet's sphere cell. Then the **metallic rule**, below.

**The metallic rule (approved by the person, round 1).** The spec's measure was a peak luma tinted lower at metallic 1.
It could not work: both spheres' peaks are 255, because carve's lights clip the highlight, and a grey base cannot show a
tint in hue. Measured on a grey-200 sphere at roughness 0.2 (`swatch-sphere-metallic-0`/`-1`), metallic 0 against
metallic 1 inside the silhouette:

- median luma 192.4 against 127.1, a step of 65.3;
- mean 191.0 against 134.0;
- p99 218.2 against 242.0;
- 261 against 1256 pixels at luma 255.

Metallic 1 loses its diffuse light and reflects the sky. The rule (`MetallicReads`): metallic 1's median is at least
`MetallicStep` = 40 lower (about 60 %, the margin T-128's `HighlightStep` uses), and more of its pixels are at 255. The
break is made on the scenario side: metallic 0 judged against itself must fail. The numbers go to `swatch-checks.tsv`.

**Checked.** `cairn task T-134 verify` passed: patina-unit, patina-regression and scenario-patina. In the first run,
scenario-patina job `J-20261120-043600-a719a7` gave the 10 planks frames (both sheets, all spheres, flat, tiled, seam,
the roughness spheres) and the 16 export and swatch-texel PNGs equal by SHA-256 to main's job `J-20261120-035840-c10d3a`.
So the planks' sheet and every committed PNG keep their bytes. `ListParamsAndVersionTests` rewrites every committed
`.texgraph` byte for byte. On 2026-11-19 the person approved the new sheet look (the plate's sheet and sphere) and the
metallic rule, from `G:\My Drive\artifacts\carve\M-28\T-134\round-1`. The scenario frames were opened only as
scaled copies.

**Rebased onto T-135 and T-136.** Three conflicts touched only lines that add, and both sides were kept:
define_graph's worked examples, `NodeCatalogue`'s node lines (with the `output` line as T-134 has it), and CLAUDE.md's
Patina lines. One fix went in a commit of its own. T-135's `CobblestonesGraphTests` took `OutputRoles.All` to mean the
cobblestones' four roles, but `All` now holds six, so the test names those four (`FourRoles`), as the planks' tests do.

**What T-137, T-138 and T-139 build on.**
- *List parameters*: `ParamSpec.Floats(name, minCount, maxCount, min, max, default...)` and `ParamSpec.Colours(name,
  minCount, maxCount, default...)`, read through `NodeParameters.Floats`/`Colours`. In the file they are a JSON array of
  numbers, or of `"#rrggbb"` strings. `[]` is either kind. A mixed array is a load error ("an array is all numbers or all
  strings"). Describe and Check give "a list of 2 to 8 numbers [a, b, ...], each from 0 to 1" and "a list of 0 to 4
  colours [\"#rrggbb\", ...]". A file with them stays version 1. T-137's `gradient_map` is their first user.
- *Version 2*: `TexGraphFile` reads versions 1 and 2 and writes the lowest version that holds the graph
  (`TexGraphFormat.VersionOf`).
  - To add a top-level field, subclass `TexGraphField` (`Name`, `Version`, `IsIn`, `Write`, `Read`) and add it to
    `TexGraphFormat.Current` with `.With(field)`. Fields are written after `seed` and before `nodes`. T-139 adds
    `parameters` this way, with a slot on `TextureGraph` for its value.
  - To add a node type of version 2, call `.WithNodeType(type, 2)`. T-140 does this for `graph` and `input`.
  - A version-1 file that uses either is a load error at the field or node.
  - The round trip is tested on a stand-in field and node type in `ListParamsAndVersionTests`.
- *Roles and columns*: `OutputRoles.Order` gives the role order, `base_colour, normal, roughness, metallic, occlusion,
  height`, then named outputs a to z. The sheet has one column per output the graph has, in that order, and a missing
  role has no column. A named output packs as `base_colour`: a grey output is spread to grey, a colour one is sRGB. The
  label atlas holds 15 columns. A sheet wider than that needs the atlas taller again (`LabelAtlas.Size`).
- *Engine needs*: none.
