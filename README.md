# Carve

Carve is a 3D modelling tool for Claude agents, served over MCP. An agent
defines a mesh by coordinates and looks at it through offscreen renders.

## Use

Add this to the `.mcp.json` of the project whose agent should model (change the
path to where this repo is checked out):

```json
{
  "mcpServers": {
    "carve": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Carve.Server", "--no-launch-profile"]
    }
  }
}
```

It needs the [.NET 10 SDK](https://dotnet.microsoft.com/download) and a Vulkan
device (a GPU with a current driver). Every package, including the
[Empyrean](https://www.nuget.org/packages/Arcodiant.Empyrean) engine, comes from
nuget.org, so the first `dotnet run` restores everything. There is no window;
every look is an offscreen render. If no device is found the server stays up
and the `render` tool says so.

Each tool's description is complete enough to use the tool and stays under
Claude Code's cut of a tool description (4,096 characters; carve holds each to
3,686). What does not fit is in the `help` tool: `help {"tool":"render"}` gives
render's full reference (every look, caption and error), and likewise `export`
(what is written, the limits file), `bake`, `check`, `save`, `open` (the .carve
format and the part tree), `select` (every error), `define_material` and
`get_mesh` (where ids come from, the worked examples); `help {}` lists the nine
entries. A description ends with `More: help ...` when it
has one.

## Build and test

```
dotnet build Carve.slnx
dotnet test tests/Carve.Core.Tests --filter "Tier!=regression"
dotnet test tests/Carve.Server.Tests --filter "Tier!=regression"
```

Those two suites need no GPU and are what CI runs. `tests.json` lists every
suite, its tier, its command and the files it reads, and `cairn test list` prints
them with the device each needs and about how long it takes. By tier:

- unit, no GPU: `core-unit` and `server-unit` (the two above), `graphs-unit` (the
  graph library), `patina-unit` (patina's graphs and its server) and
  `weathering-unit` (the weathering acceptance project's round-free fact).
- regression, no GPU: `graphs-regression` and `patina-regression`.
- regression, on a Vulkan device: `server-regression`, `viewer-cube`, and the
  scenarios under `scenarios/`, each a run through the real server:
  `scenario-cube`, `-sky`, `-rig`, `-crate`, `-uvs`, `-shed`, `-export`,
  `-character`, `-soft`, `-materials`, `-planks`, `-cuts`, `-parts`, `-morph`,
  `-gatehouse`, `-orcward`, `-bakes`, `-weathered` and `-weathered-sizes` on
  carve, and `scenario-patina`,
  `scenario-patina-library-1` and `scenario-patina-library-2` on patina.

`scripts/verify.ps1` builds, runs every test project and the cube scenario.

## From an empty file to a game

The same `.mcp.json` entry above is all the setup. The calls, in order, with
the paths relative to the folder carve was started in (its working directory):

1. `define_mesh` the cube (the example in its description), then edit it with
   `move` and `extrude`.
2. `save` with `{"path":"crate.carve"}`. This writes `crate.carve`, the file to
   keep working in: the polygons and their ids exactly, not the undo history.
   Missing folders are created, and a file already there is replaced (the result
   says so).
3. In a later session, `open` with `{"path":"crate.carve"}`. It replaces the
   current mesh in one step (`undo` takes it back to the mesh before it; the first mesh a process has cannot be undone, so undo straight after an `open` that gave the process its first mesh is an error, while a process that had a mesh before it can undo it; only the last 100 changes are kept), and the ids from before
   the save still name the same vertices and faces. Carve remembers no current
   file, so the path is given every time. A path that is not there is an error
   that names the full path and lists the `.carve` files in that folder.
4. `export` with `{"path":"C:/path/to/game/Content/models/crate.glb"}`. The
   `.glb` is only for the game; it keeps triangles, not faces, so edit the
   `.carve` file and export again, adding `"overwrite":true`: export never
   replaces a file without it.
5. The game loads it with one line:

   ```csharp
   models.LoadModel(new ContentPath("models/crate.glb"))
   ```

The tool descriptions carry the same sequence, so an agent that has only the
tool list can follow it.

## Asking the mesh

An agent can question a mesh instead of reading every id out of `get_mesh`.

- `get_mesh` reads a large mesh in parts. `include` names the sections to answer, by the answer's own keys (`vertices`, `faces`, `edges`, `colours`, `groups`, `materials`, `face_materials`, `uvs`, `seams`, `targets`); `closed`, `bounds`, `warnings`, `parent` and `world` are always answered. `faces` (face ids or a group's name) narrows the per-face sections to those faces, and vertices and edges to the ones those faces use. On the wrench, whose whole answer is about 53,000 characters, `{"include":["groups"]}` then `{"faces":"steel","include":["faces","vertices"]}` each answer under 25,000; ask a few sections of one group at a time, since a group with every section, or every face with its uvs, can still pass that. With neither, `get_mesh` answers the whole mesh as before. `help {"tool":"get_mesh"}` gives its worked examples on the cube.

- `check` (no arguments) lists what is wrong: inside-out shells, coincident or unused vertices, duplicate faces, open edges (holes), flipped or non-manifold edges, bent faces, each with the element ids.
- `select` takes a `kind` (`vertices`, `edges` or `faces`) and a `where`, and returns the ids. `where` is comparisons on a property (`y > 3.55`, `min.y >= 3.6`, `length < 0.2`) or a flag (`open`, `unattached`, `non_planar`), joined by `and`, `or`, `not` and brackets; `all` and `none` also work. For example `edges` where `open`, or `faces` where `normal.y > 0.9`.
- The server keeps no selection: pass the ids of a `select` result to `move` or `extrude` yourself, and ask again after an edit.
- `render` with `labels` (`vertices`, `faces`, `both`) draws the ids on the image, and `highlight` marks named elements. With `views` (a list of the axis views `+x`, `-x`, `+y`, `-y`, `+z`, `-z`, or `front`, `back` and the rest) it returns one contact sheet, e.g. `{"views":["+x","-x","+y","-y","+z","-z"],"labels":"faces","mode":"normals"}`. `mode` also takes `wireframe`.

`samples/leaky-shed.carve` is a broken shed to try them on.

## Cutting

Each of these edits the mesh in place and appends new ids at the end; each example is on a fresh `define_mesh` unit cube unless it says otherwise. A cut is refused, with the point and its distance, when a knife point is off the surface.

- `plane_cut` slices through a plane; with `keep` it keeps one side and caps it (the default, `both`, only splits): `{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}`. Two cuts with tilted normals make a gable.
- `loop_cut` runs a ring of new edges round a band of faces, from one edge, at fractions along it: `{"edge":[0,3],"at":[0.5]}`.
- `knife` draws a path on a face, splitting it; a closed path cuts out a piece with its own id: `{"points":[[0.25,0.25,1],[0.75,0.25,1],[0.75,0.75,1],[0.25,0.75,1]],"closed":true}`.
- `merge_faces` joins connected faces into one, a non-planar result being a warning; after the `loop_cut` above, this puts the front's two halves back together, faces 1 and 7, into face 1: `{"faces":[1,7]}`.
- `delete` removes faces or vertices; their ids stay as `null` (tombstones) in `get_mesh`: `{"faces":[3]}`.
- `bridge` joins two open loops of vertices with a band of faces, which closes an opening left by `delete`; on `bridge`'s own example, two unit squares facing each other, this makes a box: `{"loops":[[0,1,2,3],[4,5,6,7]]}`.

A window through a wall is `knife` on the front and the back, `delete` of both pieces, then `bridge` of the two loops. `samples/gatehouse.carve` is a wall built by cuts alone: a cornice, a gable, a door and a see-through window.

## Several meshes, colours and smooth surfaces

A model holds named meshes. `define_mesh` with a `name` adds a mesh, or replaces the one of that name. Once there is more than one, the other tools take `mesh` to say which to work on, and `render` draws them all together. `define_mesh`'s own description builds a crate and a lid this way.

- `define_group` names a set of faces so that later calls can treat them as one: `{"name":"sides","faces":[0,1,4,5]}` (on the unit cube, its -Z, +Z, -X and +X faces). A face may be in several groups, and an extrude's walls join their face's groups.
- `set_colour` colours faces, or a group, with sRGB hex: `{"colour":"#C8503C","faces":[0,1]}`. Export writes one matte material per colour.
- `set_shading` makes a mesh `smooth` (or `flat`, the default): `{"shading":"smooth"}`. Under smooth, `mark_edges` keeps chosen edges creased: `{"edges":[[0,1]],"hard":true}`.
- `subdivide` rounds a mesh off by Catmull-Clark, 1 to 3 levels: `{"levels":1}`. Every id stays, new ones are appended, and hard edges stay sharp.
- `displace` roughens a mesh in its geometry: each vertex moves along its normal by `amount` x (h - `midlevel`), h read from a patina graph's greyscale output at the vertex's UVs (`{"source":{"graph":"rock.texgraph","output":"height"},"amount":0.1}`) or from seeded 3D noise at its position (`{"source":{"noise":{"seed":7,"scale":0.35,"octaves":5}},"amount":0.6}`). `axis` moves along one direction instead (terrain), and `vertices`, `faces` or `group` displace part of the mesh. Only positions change. `samples/boulder.carve` is the cube subdivided 3 levels, displaced by that noise and set smooth; `samples/boulder.png` is its front frame from scenario-cuts.

`render` and `export` draw the same normals, so a smooth mesh looks the same in the game.

## Parts and levels

The meshes of a model are the parts of a part tree, which places them as the game will. A named mesh is a top-level part at the origin until it is placed. `samples/rover.carve` is one: a chassis with a cab, a mast carrying a lamp, and four wheels under two empty axle parts, three of them instances of `Wheel_FL`. The examples are on it (`open` it first).

- `define_part` places a part, or makes one with no geometry of its own: an empty part (a pivot or a group, such as an axle) or an instance, which shows the geometry of the part it is `instance_of`. It takes `parent`, `translation`, `rotation` (a unit quaternion), `scale`, `envelope` and `moves`, and on a part that exists it changes only what is given. This turns the mast an eighth of a turn about +Y: `{"part":"Mast","rotation":[0,0.38268343,0,0.92387953]}`. Edits take `mesh` with a part's name and work in the part's own space, so placing a part never changes its ids.
- `get_parts` reads the tree back: each part's placement, vertex and face counts, kept levels, bounds in its own space and in the model, and warnings. An `envelope` is the box a part and everything under it should stay inside; leaving it is a warning in `get_parts` and `check`, never a refusal.
- `keep_level` keeps the parts' geometry as a numbered level, so a blockout (level 0) stays while the parts are refined: `{"level":1}`. Edits change only the live geometry. `get_mesh` with `level` reads a level back, and `render` with `level` draws each part at its newest kept level at or below it: `{"level":0}`.
- `render` with `part` draws that part and the parts under it, each where it is placed: `{"part":"Mast"}`.
- `export` keeps a glTF node only for a part marked `moves`: the static parts are merged into the root node, each moving part is a child node under its nearest moving ancestor, and moving instances share one glTF mesh. `export`'s description works through the rover.

## UVs

A face's UVs are one `[u, v]` per corner. They run as in glTF: (0, 0) is the texture's top-left, and v runs down. Every edit, `undo` and the `.carve` file carry them.

- `set_uvs` gives faces UVs by coordinates. On the unit cube this maps face 0 (corners `[0,3,2,1]`) to the whole texture: `{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}`.
- `project_uvs` projects faces: `planar` (one side of a thing), `box` (crates, rooms) or `cylindrical` (barrels, posts): `{"method":"box"}`, or `{"method":"planar","view":"+y","faces":[3],"into":[0,0,0.5,1]}` for the top into the left half.
- `unwrap` cuts the faces at their seams, flattens each island and packs them: `{}`. Mark the seams first with `mark_edges` and `seam`: `{"edges":[[0,3]],"seam":true}`. Each island must lie flat, so a closed box needs enough seams to unfold, and a cylinder needs one down its side and one round each rim. `unwrap`'s description works through a barrel.
- `render` with `mode` `checker` draws a checker at each face's UVs, with unmapped faces magenta. `mode` `uvs` draws the layout flat, with seams in yellow.
- `check` warns of unmapped, flipped, zero-area and overlapping UV faces.

`samples/barrel.carve` is the seamed, unwrapped barrel.

## Materials and textures

A material is a named PBR look shared by the whole model; any mesh's faces may use it. A face has either a colour or a material, and setting one clears the other.

1. Give the faces UVs (above): `project_uvs` `{"method":"box","faces":[1,3]}`, the unit cube's front and top.
2. `define_material` with `base_colour`, `metallic`, `roughness`, `emissive`, `occlusion`, `double_sided` and up to five PNG textures (`base_colour_texture`, `metallic_roughness_texture`, `normal_texture`, `emissive_texture`, `occlusion_texture`): `{"name":"crate","roughness":0.8,"base_colour_texture":"textures/crate.png"}`. Anything left out takes its default, so a bare name is matte white.
3. `set_material` puts it on faces or a group: `{"material":"crate","faces":[1,3]}`.

Any texture slot may instead name a texture graph's output, `{"graph":"graphs/iron.texgraph","output":"base_colour","size":1024}` (`size` a power of two from 64 to 4096, default the graph's own). `define_material` and `get_mesh` answer a graph slot as `{graph, output, size, reads_mesh}`: `reads_mesh` is true when the graph reads the mesh (a mesh node in it or a nested file), so each mesh that wears it bakes its own, false when it does not, and null when the graph file cannot be read now. A graph is read again at every render and export, where a PNG is read once; a model with one is saved as `.carve` version 10. `render` draws a graph material, evaluated on each mesh's UVs and bakes and kept until the graph files, the mesh or its UVs change; `export` bakes each graph slot from the graph files as they are then and writes it into the `.glb` as an ordinary PNG (a material that reads the mesh and is used by several meshes is written once per mesh, `<material>@<mesh>`), so the game loads it with no knowledge of graphs.

Texture and graph paths are relative to carve's working folder, or absolute; the `.carve` file keeps them relative to itself. A texture is drawn only on faces with UVs, and `check` and `set_material` warn of faces without them. `render` draws a material face with the engine's PBR shading under a fixed sky, so metal reads. `export` writes each material as a glTF metallic-roughness material with its PNGs embedded.

`samples/barrel-textured.carve` is the barrel with `staves`, `iron`, `end-grain` and `rune-stave` materials from `samples/textures/`, and `samples/barrel-planks.carve` is the same barrel with its staves from patina's planks graph. `samples/barrel-weathered.carve` is the barrel with raised hoops and two materials that read the mesh, `samples/graphs/weathered-iron.texgraph` on the hoops (worn bright on their rims, from `mesh_curvature`) and `samples/graphs/weathered-staves.texgraph` on the staves (grime beside the hoops, from `mesh_occlusion`); open it and `render` to see the bake, or `render` `{"mode":"uvs","image":{"graph":"samples/graphs/weathered-iron.texgraph","output":"wear"}}` (from the repo root) to see the wear map in the layout.

## Baked maps

`bake` writes PNG maps of one mesh, worked in its UV layout, for `define_material` to take as they come back. It changes nothing in the model (no undo step, nothing to save). The faces it bakes need UVs, and no two may share a texel.

- `maps` picks from `masks` (a grey mask for each face group and each material), `base_colour`, `curvature` (convex and concave edges, for wear in a texture graph), `occlusion`, `orm` (occlusion, roughness and metallic in one RGB file, as glTF reads them) and `normal` (the hard edges rounded over `bevel.width` metres, with no extra geometry). The default is `masks` and `base_colour`.
- `surface` gives a colour, roughness and metallic per group or material, a later entry winning a face two of them name. `occlusion` needs `occlusion.reach` in metres; `curvature` and `normal` need a `width` in their `curvature` and `bevel` blocks.
- `size` is a power of two from 64 to 4096 (default 1024) and `folder` defaults to `textures`, relative to carve's working folder. A bake always overwrites; the result gives each file's path in the form `define_material` takes, with its coverage and its range of values.
- Beyond the padding, where no island reaches, a mask and `occlusion` hold white and `curvature` 128, the values the mesh graph nodes read there, so a node and its bake are one image; `base_colour`, `orm` and `normal` hold black.

On `samples/wrench.carve` (`open` it first), the bake in `bake`'s own description, then a material from it:

1. `bake`: `{"mesh":"wrench","folder":"textures","size":512,"maps":["masks","base_colour","orm"],"surface":{"all":{"base_colour":"#80858A","roughness":0.4,"metallic":1},"rubber":{"base_colour":"#282A2E","roughness":0.88,"metallic":0},"red":{"base_colour":"#B02A22"}},"occlusion":{"rays":32,"reach":0.035}}`. It writes `textures/wrench.base_colour.png`, `textures/wrench.orm.png` and a mask for each group.
2. `define_material`: `{"name":"wrench","base_colour_texture":"textures/wrench.base_colour.png","metallic_roughness_texture":"textures/wrench.orm.png","occlusion_texture":"textures/wrench.orm.png"}`, the one ORM file in both slots.
3. `set_material`: `{"material":"wrench","group":"all"}`, the wrench's group of every face.

## Characters

A character is a model with a skeleton, bone weights and clips. `export` then writes a skinned `.glb`. The examples are one small figure: a mesh `body` and a box `hat` of 8 vertices on its head, with this skeleton.

- `define_skeleton` sets every bone, in order, each `{name, parent, position, rotation}` local to its parent, with parents before children:

  ```json
  {"bones":[{"name":"hips","parent":null,"position":[0,1,0],"rotation":[0,0,0,1]},{"name":"spine","parent":"hips","position":[0,0.3,0],"rotation":[0,0,0,1]},{"name":"head","parent":"spine","position":[0,0.4,0],"rotation":[0,0,0,1]}]}
  ```

  `get_skeleton` reads the bones back, with where each one rests in model space.
- `weigh` weights a mesh by distance to the bones: `{"mesh":"body","replace":true}`. `set_weights` overrides vertices, or a face group, by hand; this binds the whole hat to the head: `{"mesh":"hat","vertices":[0,1,2,3,4,5,6,7],"weights":{"head":1}}`. `get_weights` reads them back and lists any unweighted vertices.
- `define_clip` makes a named clip from rotation and translation keys per bone. Values are absolute and local to the parent, and each channel part has its own interpolation, `LINEAR` (the default) or `STEP`. A nod of the head, 20 degrees about X and back:

  ```json
  {"name":"nod","duration":1,"loop":true,"channels":{"head":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0.17364818,0,0,0.98480775]},{"time":1,"value":[0,0,0,1]}]}}}}
  ```

  `get_clips` reads clips back and warns of a looping clip that does not close. `delete_clip` removes one.
- `render` draws the model in a `pose`, or at a `clip` and `time`: `{"clip":"nod","time":0.5}`. `bones: true` draws the skeleton over it (`bone_labels: true` names the bones), and `weights` with a bone's name shows that bone's weights as heat, with unweighted vertices magenta.

`export` takes a `mode`:

- `model` (the default) writes the skinned mesh, its materials, the skeleton and one animation per clip. It refuses unweighted vertices unless given `allow_unweighted: true`.
- `clips` writes the skeleton and clips with no mesh. It is one animation file that every character sharing the skeleton can use, and the game loads it with `LoadAnimationSet`.
- `piece` with `meshes` writes chosen meshes alone, with the whole skeleton in the same order, as a costume part the game swaps at runtime on the body's pose: `{"path":"models/figure.hat.glb","mode":"piece","meshes":["hat"]}`.

`render_glb` draws an exported `.glb` through the engine's own loader and GPU skinning, at rest or at a `clip` and `time`: `{"path":"models/figure.glb","clip":"nod","time":0.25}`. A `render_glb` frame and a `render` frame of the same model, view, size, clip and time line up, so the two can be compared.

`export` also warns when a model passes a game's limits on triangles, submeshes or height, read from the first `carve-limits.json` found in the `.glb`'s folder or a folder above it (or the one `limits` names). `write_limits: true` creates the file with starting numbers to edit. `samples/gardener.carve` is a rigged, animated character, and `samples/gardener-soft.carve` is the same character subdivided and smoothed.

## Morph targets

A morph target is a mesh in another shape, the same vertices moved, that a game blends in by a weight from 0 to 1. One target name is one dial across the model, so meshes that move together use the same name.

- `define_target` makes one from absolute `positions`, or from a `.carve` `file` holding the same mesh edited (the same vertices and faces, moved). On `define_mesh`'s unit cube this raises the top and spreads it: `{"name":"Bulge","positions":{"2":[1.1,1.3,-0.1],"3":[-0.1,1.3,-0.1],"6":[1.1,1.3,1.1],"7":[-0.1,1.3,1.1]}}`. A name the mesh already has replaces that target.
- `get_mesh` lists a mesh's targets, and with `target` reads one back. `delete_target` removes one: `{"name":"Bulge"}`.
- `render` with `targets` draws the model at those weights, `{"targets":{"Bulge":1}}`, before any `pose` or `clip` skins it. `render_glb` takes the same `targets` for an exported file.
- `export` writes each target as a glTF morph target, and its result names them in glTF target order.

`samples/arm.carve` is a rigged arm with `Bulk` and `Lean` targets and a `Curl` clip; `samples/arm-lean.carve` is the arm's cage in the `Lean` shape, before subdividing, which is the file `Lean` was made from with `file` (the arm in `arm.carve` has since been subdivided, so the two no longer match).

## patina: texture graphs

`patina` is a second MCP server, beside carve, for texture graphs (D-7): an agent
defines a graph of nodes, edits it, reads it back, looks at it, measures it, and
saves it as a `.texgraph` file whose PNGs a carve material wears. Add it to the
same `.mcp.json`, next to carve's entry:

```json
{
  "mcpServers": {
    "carve": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Carve.Server", "--no-launch-profile"]
    },
    "patina": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Patina.Server", "--no-launch-profile"]
    }
  }
}
```

Its tools: `define_graph` (the whole graph, as a `.texgraph` body), `nodes` (the
node catalogue), `edit_graph` (a list of operations, all or nothing: `set_node`,
`remove_node`, `connect`, `disconnect`, `set_params`, `set`, and
`set_parameter`, `set_parameters`, `remove_parameter` for exposed parameters),
`get_graph`, `save`, `open`, `push_snapshot` / `pop_snapshot` to try a change
and go back, `swatch`, `export` and `check`. Every fault names its node and its
port or parameter. A graph with faults can be opened and fixed with
`edit_graph`, but not saved, swatched, exported or checked. Each tool's
description says what it takes and returns, with worked examples; this section
is the overview.

### Nodes

`nodes {}` lists every node type with its inputs, outputs, parameters (kind,
range, default) and a worked example; `nodes {"type":"tiles"}` gives one.

- Sources: `noise` (value noise), `worley` (cells), `fbm` (layered noise),
  `gradient`, `colour`, and the patterns `tiles` (tiles and bricks, with gaps,
  bevel and jitter), `stripes` and `shape` (a circle, square, ring, diamond or
  polygon in each cell of a grid).
- Filters: `levels`, `blend` (`mix`, `multiply`, `add`, `subtract`,
  `difference`, `min`, `max`; `difference` against a flat level draws a thin
  line where a noise crosses it), `warp` (domain warp), `blur`, `gradient_map`,
  `transform` (offset, tile, mirror, free rotation and scale), `distance` (from
  a mask's edge) and `height_to_normal`.
- Structure: `output` (one per role), `input` (an image input of a graph used
  as a node) and `graph` (another `.texgraph` used as one node).

### Outputs and export

An `output` node has a role: `base_colour`, `normal`, `roughness`, `metallic`,
`occlusion`, `height`, or a name of its own (a mask, written as `<name>.png`).
`export` writes them as `<folder>/<role>.png` at a size you name, a power of two
from 32 to 8192 (default the graph's own), ready for a carve material's texture
slots: `base_colour.png` for `base_colour_texture`, `normal.png` for
`normal_texture`, `roughness.png` for `metallic_roughness_texture`,
`occlusion.png` for `occlusion_texture`. `roughness.png` is a glTF
metallic-roughness image: roughness in green, the graph's `metallic` in blue (0
when it has none), so metallic has no file of its own. `height` and named
outputs are grey (or sRGB, for a colour named output) and have no slot. The same
graph at the same size always exports the same bytes on one runtime, and those
bytes are the ones .NET 10's zlib writes: another runtime version may write other
bytes for the same texels (the texels do not change).

### Exposed parameters and graphs as nodes

A graph may declare `parameters`, each driving one or more node parameters
(`"layout.gap"`) and taking the narrowest range of what it drives; `get_graph`
lists them, and `edit_graph`'s `set_parameters` changes them. `swatch`, `export`
and `check` take `parameters` for that call only, so one graph gives variants
without being changed.

A `graph` node uses another `.texgraph` by `path`, relative to the file of the
graph that uses it (a graph never saved: patina's working directory). Its other
parameters are the nested graph's exposed parameters by name; its inputs are
the nested graph's `input` nodes, so a reusable graph can be a filter; its
outputs are the nested graph's outputs (`wall.base_colour`). Graphs that use
these are format version 2; every other file is written as version 1.

### Looking: swatch

`swatch` shows the open graph as an image, at a `size` from 32 to 2048, with a
`view`:

- `sheet` (the default): a column per output, flat over tiled 3 x 3 to show
  seams, beside a sphere wearing the material;
- `flat` or `tiled`: one `output` alone;
- `sphere` or `cube`: the material on a lit sphere, or on a cube showing three
  faces and their edges, turned by `angle`.

`tiling` (1 to 8) repeats the maps on the sphere and cube, to judge a material
at a texel density nearer a game's. The sphere and cube are drawn with the
engine's PBR material, lit as carve's `render` lights a model, so patina needs a
Vulkan device for `sheet`, `sphere` and `cube`; `flat` and `tiled` need none.

### Measuring: check

`check` measures outputs as numbers rather than by eye, and lists each problem
with its share of texels, the worst texel and the fix in words. It reports and
never refuses a graph that evaluates. It looks at the open graph (at its size,
at most 2048, with `parameters` if given), or at PNG files given as `files`,
each `{path, role}`; role `metallic_roughness` is a packed `roughness.png` as
export writes it. The rules:

- `base-colour-range`: base colour too dark or too bright for game lighting
  (outside 20 to 245 sRGB, or under 120 for metal), in over 1% of texels.
- `metallic-not-binary`: metallic between 0.1 and 0.9 in over 5% of texels.
- `normal-not-unit`: normals not of unit length.
- `one-value`: a greyscale output of one value, a mask that masks nothing.
- `does-not-tile`: a seam where the texture wraps.

### Samples

`samples/graphs/` holds a graph per material, each with its exported PNGs and
its swatch sheet in a folder of its name: `planks`, `cobblestones`, `bricks`
(with mortar, tint and jitter exposed; `bricks/variants/grey/` is its grey stone
variant), `fabric`, `marble` (veins by `blend`'s `difference`), `painted-metal`,
`floor-tiles`, `grime` (a filter graph with an `input`) and `old-wall` (the
bricks and the grime used as `graph` nodes).
`weathered-iron` and `weathered-staves` read the mesh (`mesh_curvature`, `mesh_occlusion`,
`mesh_mask`, `mesh_position`): patina has no mesh, so it evaluates those nodes on neutral
stand-ins (curvature 0.5, occlusion 1, mask 1, position `#808080`) and says so in `get_graph`,
`open`, `swatch`, `export`, `check` and `save`, also for a graph that nests one (the node
named as expanded, `g/edges`); the real maps come from the mesh when a carve
material names the graph. Their PNGs are the neutral ones (`wear` and `grime` are black),
so `check` reports `one-value` on those two outputs and nothing else, its fix saying they may be
flat only because the mesh inputs are neutral here: to see the real map, draw it in carve with
`render` mode `uvs` and the graph as `image`, and if it is flat there too, widen its levels (any
output reading a mesh node, nested or not, is told the same).
`samples/graphs/faults/` holds two graphs that `check` finds faults in.

### A worked example

With patina started in the carve checkout (paths are relative to patina's
working directory, or absolute), grey stone bricks from the bricks sample:

1. `open` `{"path":"samples/graphs/bricks.texgraph"}`, then `get_graph` `{}`
   lists its parameters `jitter`, `mortar` and `tint`.
2. `swatch` `{"parameters":{"mortar":0.08,"tint":"#7a7a70"}}` shows the grey
   variant's sheet.
3. `check` `{"size":64,"parameters":{"mortar":0.08,"tint":"#7a7a70"}}` returns
   `"ok":true` and each output's statistics.
4. `export` `{"folder":"textures/bricks_grey","parameters":{"mortar":0.08,"tint":"#7a7a70"}}`
   writes `base_colour.png`, `normal.png`, `roughness.png` and `occlusion.png`,
   equal to those in `samples/graphs/bricks/variants/grey/`.
5. In carve, started in the same folder, with UVs on the faces (above):
   `define_material`
   `{"name":"bricks","base_colour_texture":"textures/bricks_grey/base_colour.png","normal_texture":"textures/bricks_grey/normal.png","metallic_roughness_texture":"textures/bricks_grey/roughness.png","occlusion_texture":"textures/bricks_grey/occlusion.png"}`,
   then `set_material` puts the bricks on them.

### What is slow or bounded

`export` at 8192 evaluates in tiles, one output at a time, so the process stays
under 2 GB (the planks' four outputs peaked at about 0.7 GB), but it is slow,
and most of that is evaluating the graph: the planks' four outputs took 28 to
31 s in a release build and 2.8 to 4.1 minutes in a debug build (168, 233 and
248 s on a busy machine), with each PNG 0.3 to 1 MB. The encode was timed apart
from the call, by a harness that times `PngWriter.Write` of each output to a
file: 0.6 to 1.1 s for all four in a release build, 1.4 s in a debug one, so it
is the encoder's cost, not a share measured inside `export`. A client with a
short call timeout should ask for one or two outputs at a time. `blur` and `distance` are
the slowest nodes at 8192 (about 2 minutes and about 20 s); the 10 s target for
each is T-159's, not yet met. `swatch` and `check` stop at 2048.

## License

Carve is released under the [MIT license](LICENSE).
